import { RevitSocketClient } from "./SocketClient.js";
import { resolveNavisEndpoint } from "./navisEndpoint.js";

// ── ADDITIVE-ONLY: Navisworks connection manager ──
// Mirrors ConnectionManager.ts (Revit) without modifying it.
// Maintains its own singleton socket aimed at the Navisworks plugin (default 8091).

let _navisClient: RevitSocketClient | null = null;
let _navisConnecting: Promise<void> | null = null;

const MAX_RETRIES = 5;
const INITIAL_DELAY_MS = 1000;

function newNavisClient(): RevitSocketClient {
    const { host, port } = resolveNavisEndpoint();
    return new RevitSocketClient(host, port);
}

function getOrCreateNavisClient(): RevitSocketClient {
    if (!_navisClient || _navisClient.rawSocket.destroyed) {
        _navisClient = newNavisClient();
    }
    return _navisClient;
}

function sleep(ms: number): Promise<void> {
    return new Promise((resolve) => setTimeout(resolve, ms));
}

async function connectWithRetry(client: RevitSocketClient): Promise<void> {
    let lastError: Error | null = null;
    for (let attempt = 1; attempt <= MAX_RETRIES; attempt++) {
        try {
            await attemptConnect(client);
            return;
        } catch (err) {
            lastError = err instanceof Error ? err : new Error(String(err));
            if (attempt < MAX_RETRIES) {
                const delay = INITIAL_DELAY_MS * Math.pow(2, attempt - 1);
                console.error(
                    `[Navis] Connection attempt ${attempt}/${MAX_RETRIES} failed: ${lastError.message}. Retrying in ${delay / 1000}s...`
                );
                await sleep(delay);
                if (client.rawSocket.destroyed || !client.isConnected) {
                    _navisClient = newNavisClient();
                    client = _navisClient;
                }
            }
        }
    }

    const ep = resolveNavisEndpoint();
    const where =
        ep.source === "handshake"
            ? `${ep.host}:${ep.port} (from the Navisworks plugin handshake file)`
            : ep.source === "env"
                ? `${ep.host}:${ep.port} (from BIMBOT_NAVIS_HOST/BIMBOT_NAVIS_PORT)`
                : `${ep.host}:${ep.port} (default — the Navisworks plugin has not published a port, so its service is probably not running)`;

    throw new Error(
        `Could not connect to Navisworks at ${where} after ${MAX_RETRIES} attempts. ` +
        `Last error: ${lastError?.message ?? "unknown"}. ` +
        `Check that Navisworks Manage is open with a model loaded, and that the BIM-Bot ribbon button reads "BIM-Bot ON".`
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

async function ensureNavisConnected(client: RevitSocketClient): Promise<void> {
    if (client.isConnected) return;
    if (_navisConnecting) {
        await _navisConnecting;
        return;
    }
    _navisConnecting = connectWithRetry(client).finally(() => {
        _navisConnecting = null;
    });
    await _navisConnecting;
}

/**
 * Execute an operation with a persistent connection to Navisworks.
 * Auto-reconnects with exponential backoff. Does NOT affect the Revit connection.
 */
export async function withNavisConnection<T>(
    operation: (client: RevitSocketClient) => Promise<T>
): Promise<T> {
    let client = getOrCreateNavisClient();
    try {
        await ensureNavisConnected(client);
        client = getOrCreateNavisClient();
        return await operation(client);
    } catch (err) {
        if (!client.isConnected || client.rawSocket.destroyed) {
            console.error("[Navis] Connection lost mid-operation — reconnecting and retrying...");
            _navisClient = null;
            client = getOrCreateNavisClient();
            try {
                await ensureNavisConnected(client);
                client = getOrCreateNavisClient();
                return await operation(client);
            } catch (retryErr) {
                _navisClient = null;
                throw retryErr;
            }
        }
        throw err;
    }
}
