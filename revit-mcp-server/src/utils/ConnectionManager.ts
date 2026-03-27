import { RevitSocketClient } from "./SocketClient.js";

/**
 * Persistent singleton connection manager for Revit TCP communication.
 *
 * Instead of creating a new TCP socket per MCP tool call,
 * this maintains a single persistent connection that auto-reconnects
 * on failure. This eliminates TCP handshake overhead when the AI
 * chains multiple tool calls in sequence.
 */

let _client: RevitSocketClient | null = null;
let _connecting: Promise<void> | null = null;

function getOrCreateClient(): RevitSocketClient {
    if (!_client || _client.rawSocket.destroyed) {
        _client = new RevitSocketClient("localhost", 8080);
    }
    return _client;
}

async function ensureConnected(client: RevitSocketClient): Promise<void> {
    if (client.isConnected) return;

    // If already connecting, wait for the same promise (avoid double-connect)
    if (_connecting) {
        await _connecting;
        return;
    }

    _connecting = new Promise<void>((resolve, reject) => {
        let settled = false;

        const cleanup = () => {
            client.rawSocket.removeListener("connect", onConnect);
            client.rawSocket.removeListener("error", onError);
            clearTimeout(timer);
            _connecting = null;
        };

        const onConnect = () => {
            if (settled) return;
            settled = true;
            cleanup();
            resolve();
        };

        const onError = (err: Error) => {
            if (settled) return;
            settled = true;
            cleanup();
            reject(new Error(`Failed to connect to Revit plugin: ${err.message}. Is the MCP plugin running in Revit?`));
        };

        client.rawSocket.on("connect", onConnect);
        client.rawSocket.on("error", onError);

        const timer = setTimeout(() => {
            if (settled) return;
            settled = true;
            cleanup();
            client.disconnect();
            reject(new Error("Connection to Revit timed out (5s). Is the MCP plugin running in Revit?"));
        }, 5000);

        try {
            client.connect();
        } catch (err) {
            if (!settled) {
                settled = true;
                cleanup();
                reject(err instanceof Error ? err : new Error(String(err)));
            }
        }
    });

    await _connecting;
}

/**
 * Execute an operation with a persistent connection to Revit.
 * Auto-reconnects if the connection was lost.
 * Does NOT disconnect after each call — the socket stays alive.
 */
export async function withRevitConnection<T>(
    operation: (client: RevitSocketClient) => Promise<T>
): Promise<T> {
    const client = getOrCreateClient();

    try {
        await ensureConnected(client);
        return await operation(client);
    } catch (err) {
        // If the connection was lost mid-operation, reset and let next call reconnect
        if (!client.isConnected) {
            _client = null;
        }
        throw err;
    }
}
