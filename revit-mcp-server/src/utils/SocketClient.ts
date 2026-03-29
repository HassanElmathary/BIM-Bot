import * as net from "net";

export interface RevitCommand {
    jsonrpc: string;
    method: string;
    params: Record<string, unknown>;
    id: string;
}

const CONTENT_LENGTH_HEADER = "Content-Length: ";

/**
 * JSON-RPC 2.0 TCP client for communicating with the Revit plugin's SocketService.
 *
 * Message framing: Length-prefixed protocol.
 *   Send:    "Content-Length: {byteCount}\n{json}"
 *   Receive: "Content-Length: {byteCount}\n{json}" (preferred)
 *            OR raw JSON with brace-counting (backward compat)
 */
export class RevitSocketClient {
    private host: string;
    private port: number;
    private socket: net.Socket;
    private connected: boolean = false;
    private responseCallbacks: Map<string, { resolve: (data: unknown) => void; reject: (err: Error) => void }> = new Map();
    private buffer: Buffer = Buffer.alloc(0);

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
            // Append raw bytes to buffer (not string — preserves multi-byte chars)
            this.buffer = Buffer.concat([this.buffer, data]);
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
     * Process all complete messages in the buffer.
     * Supports:
     *   1. Length-prefixed: "Content-Length: N\n{json}" (preferred, robust)
     *   2. Brace-counting: raw JSON objects (backward compat fallback)
     */
    private processBuffer(): void {
        while (this.buffer.length > 0) {
            // Skip leading whitespace bytes
            let startOffset = 0;
            while (startOffset < this.buffer.length && (this.buffer[startOffset] === 0x20 || this.buffer[startOffset] === 0x0A || this.buffer[startOffset] === 0x0D || this.buffer[startOffset] === 0x09)) {
                startOffset++;
            }
            if (startOffset > 0) {
                this.buffer = this.buffer.subarray(startOffset);
            }
            if (this.buffer.length === 0) break;

            const bufferStr = this.buffer.toString("utf8");

            // ── Length-prefixed framing (preferred) ──
            if (bufferStr.startsWith(CONTENT_LENGTH_HEADER)) {
                const newlineIdx = bufferStr.indexOf("\n");
                if (newlineIdx < 0) break; // Incomplete header — wait for more data

                const lengthStr = bufferStr.substring(CONTENT_LENGTH_HEADER.length, newlineIdx).trim();
                const contentLength = parseInt(lengthStr, 10);
                if (isNaN(contentLength) || contentLength <= 0) {
                    console.error(`Invalid Content-Length: '${lengthStr}'`);
                    this.buffer = Buffer.alloc(0);
                    break;
                }

                // Calculate byte offset of payload start
                const headerBytes = Buffer.byteLength(bufferStr.substring(0, newlineIdx + 1), "utf8");
                const totalNeeded = headerBytes + contentLength;

                if (this.buffer.length < totalNeeded) break; // Incomplete payload — wait for more data

                // Extract exactly contentLength bytes of payload
                const jsonStr = this.buffer.subarray(headerBytes, headerBytes + contentLength).toString("utf8");
                this.buffer = this.buffer.subarray(totalNeeded);

                this.handleResponse(jsonStr);
                continue;
            }

            // ── Brace-counting fallback (backward compatibility) ──
            if (bufferStr[0] === "{") {
                const endIdx = this.findJsonEnd(bufferStr);
                if (endIdx === -1) break; // Incomplete JSON, wait for more data

                const jsonStr = bufferStr.substring(0, endIdx);
                const consumedBytes = Buffer.byteLength(jsonStr, "utf8");
                this.buffer = this.buffer.subarray(consumedBytes);

                this.handleResponse(jsonStr);
                continue;
            }

            // Unknown data — skip until we find '{' or 'Content-Length'
            const nextBrace = bufferStr.indexOf("{");
            const nextHeader = bufferStr.indexOf(CONTENT_LENGTH_HEADER);
            let nextValid = -1;
            if (nextBrace >= 0 && nextHeader >= 0) nextValid = Math.min(nextBrace, nextHeader);
            else if (nextBrace >= 0) nextValid = nextBrace;
            else if (nextHeader >= 0) nextValid = nextHeader;

            if (nextValid > 0) {
                const skipBytes = Buffer.byteLength(bufferStr.substring(0, nextValid), "utf8");
                this.buffer = this.buffer.subarray(skipBytes);
                continue;
            }

            this.buffer = Buffer.alloc(0);
            break;
        }
    }

    /**
     * Find the end of a complete JSON object by counting braces.
     * Returns the index after the closing brace, or -1 if incomplete.
     * Kept for backward compatibility with servers that don't use length-prefixed framing.
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

                // Send using length-prefixed framing
                const jsonPayload = JSON.stringify(command);
                const payloadBytes = Buffer.from(jsonPayload, "utf8");
                const header = `Content-Length: ${payloadBytes.length}\n`;

                this.socket.write(header);
                this.socket.write(payloadBytes);
            } catch (error) {
                reject(error);
            }
        });
    }
}
