import { RevitSocketClient } from "./SocketClient.js";

/**
 * Persistent singleton connection manager for Revit TCP communication.
 *
 * Instead of creating a new TCP socket per MCP tool call,
 * this maintains a single persistent connection that auto-reconnects
 * on failure. This eliminates TCP handshake overhead when the AI
 * chains multiple tool calls in sequence.
 *
 * Includes automatic retry with exponential backoff when Revit
 * is not yet ready (e.g., still starting up).
 */

let _client: RevitSocketClient | null = null;
let _connecting: Promise<void> | null = null;

const MAX_RETRIES = 5;
const INITIAL_DELAY_MS = 1000; // 1s → 2s → 4s → 8s → 16s

function getOrCreateClient(): RevitSocketClient {
    if (!_client || _client.rawSocket.destroyed) {
        _client = new RevitSocketClient("localhost", 8080);
    }
    return _client;
}

async function connectWithRetry(client: RevitSocketClient): Promise<void> {
    let lastError: Error | null = null;

    for (let attempt = 1; attempt <= MAX_RETRIES; attempt++) {
        try {
            await attemptConnect(client);
            if (attempt > 1) {
                console.error(`✓ Connected to Revit on attempt ${attempt}`);
            }
            return; // Success
        } catch (err) {
            lastError = err instanceof Error ? err : new Error(String(err));

            if (attempt < MAX_RETRIES) {
                const delay = INITIAL_DELAY_MS * Math.pow(2, attempt - 1);
                console.error(
                    `Connection attempt ${attempt}/${MAX_RETRIES} failed: ${lastError.message}. ` +
                    `Retrying in ${delay / 1000}s...`
                );
                await sleep(delay);

                // Create a fresh socket for each retry (old one may be in bad state)
                if (client.rawSocket.destroyed || !client.isConnected) {
                    _client = new RevitSocketClient("localhost", 8080);
                    client = _client;
                }
            }
        }
    }

    throw new Error(
        `Could not connect to Revit after ${MAX_RETRIES} attempts. ` +
        `Last error: ${lastError?.message ?? "unknown"}. ` +
        `Make sure Revit is open with the BIM-Bot plugin loaded — ` +
        `the service auto-starts when Revit opens.`
    );
}

function attemptConnect(client: RevitSocketClient): Promise<void> {
    return new Promise<void>((resolve, reject) => {
        let settled = false;

        const cleanup = () => {
            client.rawSocket.removeListener("connect", onConnect);
            client.rawSocket.removeListener("error", onError);
            clearTimeout(timer);
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
            reject(new Error(`TCP connection failed: ${err.message}`));
        };

        client.rawSocket.on("connect", onConnect);
        client.rawSocket.on("error", onError);

        const timer = setTimeout(() => {
            if (settled) return;
            settled = true;
            cleanup();
            client.disconnect();
            reject(new Error("Connection timed out (5s)"));
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
}

async function ensureConnected(client: RevitSocketClient): Promise<void> {
    if (client.isConnected) return;

    // If already connecting, wait for the same promise (avoid double-connect)
    if (_connecting) {
        await _connecting;
        return;
    }

    _connecting = connectWithRetry(client).finally(() => {
        _connecting = null;
    });

    await _connecting;
}

function sleep(ms: number): Promise<void> {
    return new Promise((resolve) => setTimeout(resolve, ms));
}

/**
 * Execute an operation with a persistent connection to Revit.
 * Auto-reconnects with exponential backoff if the connection was lost.
 * Does NOT disconnect after each call — the socket stays alive.
 * If the operation fails due to a dead connection, retries once with a fresh socket.
 */
export async function withRevitConnection<T>(
    operation: (client: RevitSocketClient) => Promise<T>
): Promise<T> {
    let client = getOrCreateClient();

    try {
        await ensureConnected(client);
        return await operation(client);
    } catch (err) {
        // If the connection died mid-operation, reset and retry once
        if (!client.isConnected || client.rawSocket.destroyed) {
            console.error("Connection lost mid-operation — reconnecting and retrying...");
            _client = null;
            client = getOrCreateClient();

            try {
                await ensureConnected(client);
                return await operation(client);
            } catch (retryErr) {
                _client = null;
                throw retryErr;
            }
        }
        throw err;
    }
}
