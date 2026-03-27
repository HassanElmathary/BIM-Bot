import * as net from "net";

export interface RevitCommand {
    jsonrpc: string;
    method: string;
    params: Record<string, unknown>;
    id: string;
}

/**
 * JSON-RPC 2.0 TCP client for communicating with the Revit plugin's SocketService.
 *
 * BUG FIXES:
 * - Buffer can contain multiple concatenated JSON messages (e.g. fast sequential commands).
 *   Now uses brace-counting to find message boundaries.
 * - Timeout now properly cleans up by resolving/rejecting once, preventing double-fire.
 * - Reconnect after socket close now creates a new socket instance (old one is destroyed).
 */
export class RevitSocketClient {
    private host: string;
    private port: number;
    private socket: net.Socket;
    private connected: boolean = false;
    private responseCallbacks: Map<string, { resolve: (data: unknown) => void; reject: (err: Error) => void }> = new Map();
    private buffer: string = "";

    constructor(host: string = "localhost", port: number = 8080) {
        this.host = host;
        this.port = port;
        this.socket = new net.Socket();
        this.setupListeners();
    }

    get isConnected(): boolean {
        return this.connected;
    }

    get rawSocket(): net.Socket {
        return this.socket;
    }

    private setupListeners(): void {
        this.socket.on("connect", () => {
            this.connected = true;
        });

        this.socket.on("data", (data) => {
            this.buffer += data.toString();
            this.processBuffer();
        });

        this.socket.on("close", () => {
            this.connected = false;
            // Reject any pending callbacks — connection was lost
            for (const [id, cb] of this.responseCallbacks) {
                cb.reject(new Error("Connection to Revit closed while waiting for response"));
            }
            this.responseCallbacks.clear();
        });

        this.socket.on("error", (error) => {
            console.error("Revit socket error:", error.message);
            this.connected = false;
        });
    }

    /**
     * Process all complete JSON messages in the buffer using brace-counting.
     * This correctly handles cases where:
     * 1. Multiple JSON messages arrive in a single TCP packet
     * 2. A single JSON message is split across multiple TCP packets
     */
    private processBuffer(): void {
        while (this.buffer.length > 0) {
            // Skip whitespace/newlines between messages
            const trimmed = this.buffer.trimStart();
            if (trimmed.length === 0) {
                this.buffer = "";
                break;
            }
            if (trimmed[0] !== "{") {
                // Strip leading non-JSON characters (shouldn't happen, but defensive)
                this.buffer = trimmed;
            }

            const endIdx = this.findJsonEnd(this.buffer);
            if (endIdx === -1) break; // Incomplete JSON, wait for more data

            const jsonStr = this.buffer.substring(0, endIdx);
            this.buffer = this.buffer.substring(endIdx).trimStart();

            this.handleResponse(jsonStr);
        }
    }

    /**
     * Find the end of a complete JSON object by counting braces.
     * Returns the index after the closing brace, or -1 if incomplete.
     */
    private findJsonEnd(data: string): number {
        let depth = 0;
        let inString = false;
        let escaped = false;

        for (let i = 0; i < data.length; i++) {
            const c = data[i];

            if (escaped) {
                escaped = false;
                continue;
            }

            if (c === "\\" && inString) {
                escaped = true;
                continue;
            }

            if (c === '"') {
                inString = !inString;
                continue;
            }

            if (inString) continue;

            if (c === "{") depth++;
            else if (c === "}") {
                depth--;
                if (depth === 0) return i + 1;
            }
        }

        return -1; // Incomplete
    }

    private handleResponse(data: string): void {
        try {
            const response = JSON.parse(data);
            const id = String(response.id || "default");
            const cb = this.responseCallbacks.get(id);
            if (cb) {
                this.responseCallbacks.delete(id);
                if (response.error) {
                    cb.reject(new Error(response.error.message || "Unknown Revit error"));
                } else {
                    cb.resolve(response.result);
                }
            }
        } catch (error) {
            console.error("Error parsing Revit response:", error);
        }
    }

    connect(): void {
        if (this.connected) return;

        // If the existing socket was destroyed/ended, create a new one
        if (this.socket.destroyed) {
            this.socket = new net.Socket();
            this.setupListeners();
        }

        this.socket.connect(this.port, this.host);
    }

    disconnect(): void {
        this.socket.destroy(); // destroy is safer than end — ensures immediate cleanup
        this.connected = false;
    }

    private generateId(): string {
        return crypto.randomUUID();
    }

    sendCommand(method: string, params: Record<string, unknown> = {}): Promise<unknown> {
        return new Promise((resolve, reject) => {
            try {
                if (!this.connected) {
                    reject(new Error("Not connected to Revit. Call connect() first or use withRevitConnection()"));
                    return;
                }

                const id = this.generateId();
                const command: RevitCommand = {
                    jsonrpc: "2.0",
                    method,
                    params,
                    id,
                };

                // Set up response callback with timeout
                const timeoutId = setTimeout(() => {
                    if (this.responseCallbacks.has(id)) {
                        this.responseCallbacks.delete(id);
                        reject(new Error(`Command timed out (300s): ${method}`));
                    }
                }, 300000);

                this.responseCallbacks.set(id, {
                    resolve: (data) => {
                        clearTimeout(timeoutId);
                        resolve(data);
                    },
                    reject: (err) => {
                        clearTimeout(timeoutId);
                        reject(err);
                    },
                });

                this.socket.write(JSON.stringify(command));
            } catch (error) {
                reject(error);
            }
        });
    }
}
