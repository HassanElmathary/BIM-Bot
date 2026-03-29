import * as fs from "fs";
import * as path from "path";
import * as http from "http";
import * as url from "url";
import { fileURLToPath } from "url";
import open from "open";

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

interface TokenData {
    access_token: string;
    refresh_token?: string;
    expires_at: number;
    token_type: string;
    scope: string;
}

/** Shape of Google's OAuth 2.0 token endpoint response */
interface GoogleTokenResponse {
    access_token: string;
    refresh_token?: string;
    expires_in: number;
    token_type: string;
    scope: string;
}

interface GoogleOAuthConfig {
    clientId: string;
    clientSecret: string;
    redirectPort: number;
    scopes: string[];
}

const TOKEN_FILE = path.join(__dirname, "..", "..", "config", "google-tokens.json");
const CONFIG_FILE = path.join(__dirname, "..", "..", "config", "oauth-config.json");

/**
 * Google OAuth 2.0 handler for desktop applications.
 * Uses loopback redirect for the OAuth flow.
 */
export class GoogleAuth {
    private config: GoogleOAuthConfig;
    private tokens: TokenData | null = null;

    constructor(config?: Partial<GoogleOAuthConfig>) {
        this.config = {
            clientId: config?.clientId || process.env.GOOGLE_CLIENT_ID || "",
            clientSecret: config?.clientSecret || process.env.GOOGLE_CLIENT_SECRET || "",
            redirectPort: config?.redirectPort || 3847,
            scopes: config?.scopes || [
                "https://www.googleapis.com/auth/spreadsheets",
                "https://www.googleapis.com/auth/drive.readonly",
                "https://www.googleapis.com/auth/userinfo.email",
                "https://www.googleapis.com/auth/userinfo.profile",
            ],
        };

        this.loadTokens();
    }

    private loadTokens(): void {
        try {
            if (fs.existsSync(TOKEN_FILE)) {
                const data = fs.readFileSync(TOKEN_FILE, "utf-8");
                this.tokens = JSON.parse(data);
            }
        } catch {
            this.tokens = null;
        }
    }

    private saveTokens(): void {
        try {
            const dir = path.dirname(TOKEN_FILE);
            if (!fs.existsSync(dir)) {
                fs.mkdirSync(dir, { recursive: true });
            }
            fs.writeFileSync(TOKEN_FILE, JSON.stringify(this.tokens, null, 2));
        } catch (error) {
            console.error("Failed to save tokens:", error);
        }
    }

    get isAuthenticated(): boolean {
        return this.tokens !== null && this.tokens.expires_at > Date.now();
    }

    async getAccessToken(): Promise<string> {
        if (!this.tokens) {
            throw new Error("Not authenticated. Please sign in with Google first.");
        }

        // Refresh if expired
        if (this.tokens.expires_at <= Date.now() + 60000) {
            await this.refreshToken();
        }

        return this.tokens.access_token;
    }

    /**
     * Start the OAuth 2.0 authorization flow.
     * Opens the user's browser for Google sign-in.
     */
    async authorize(): Promise<void> {
        return new Promise((resolve, reject) => {
            const redirectUri = `http://localhost:${this.config.redirectPort}/callback`;

            const authUrl = new URL("https://accounts.google.com/o/oauth2/v2/auth");
            authUrl.searchParams.set("client_id", this.config.clientId);
            authUrl.searchParams.set("redirect_uri", redirectUri);
            authUrl.searchParams.set("response_type", "code");
            authUrl.searchParams.set("scope", this.config.scopes.join(" "));
            authUrl.searchParams.set("access_type", "offline");
            authUrl.searchParams.set("prompt", "consent");

            const server = http.createServer(async (req, res) => {
                try {
                    const reqUrl = new URL(req.url || "", `http://localhost:${this.config.redirectPort}`);

                    if (reqUrl.pathname === "/callback") {
                        const code = reqUrl.searchParams.get("code");
                        const error = reqUrl.searchParams.get("error");

                        if (error) {
                            res.writeHead(400, { "Content-Type": "text/html" });
                            res.end(`<html><body><h1>Authentication Failed</h1><p>${error}</p><script>setTimeout(()=>window.close(),3000)</script></body></html>`);
                            server.close();
                            reject(new Error(`OAuth error: ${error}`));
                            return;
                        }

                        if (code) {
                            // Exchange code for tokens
                            await this.exchangeCode(code, redirectUri);

                            res.writeHead(200, { "Content-Type": "text/html" });
                            res.end(`
                <html>
                <body style="font-family: 'Segoe UI', sans-serif; display: flex; align-items: center; justify-content: center; height: 100vh; margin: 0; background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);">
                  <div style="text-align: center; background: white; padding: 40px; border-radius: 16px; box-shadow: 0 20px 60px rgba(0,0,0,0.3);">
                    <div style="font-size: 64px; margin-bottom: 16px;">✅</div>
                    <h1 style="color: #333; margin: 0 0 8px 0;">Signed In Successfully!</h1>
                    <p style="color: #666;">Revit MCP is now connected to your Google account.</p>
                    <p style="color: #999; font-size: 14px;">You can close this window.</p>
                  </div>
                  <script>setTimeout(()=>window.close(),5000)</script>
                </body>
                </html>
              `);
                            server.close();
                            resolve();
                        }
                    }
                } catch (err) {
                    server.close();
                    reject(err);
                }
            });

            server.listen(this.config.redirectPort, () => {
                console.error(`Opening browser for Google sign-in...`);
                open(authUrl.toString()).catch(() => {
                    console.error(`Please open this URL in your browser:\n${authUrl.toString()}`);
                });
            });

            // Timeout after 5 minutes
            setTimeout(() => {
                server.close();
                reject(new Error("Authentication timed out (5 minutes)"));
            }, 300000);
        });
    }

    private async exchangeCode(code: string, redirectUri: string): Promise<void> {
        const tokenUrl = "https://oauth2.googleapis.com/token";
        const body = new URLSearchParams({
            code,
            client_id: this.config.clientId,
            client_secret: this.config.clientSecret,
            redirect_uri: redirectUri,
            grant_type: "authorization_code",
        });

        const response = await fetch(tokenUrl, {
            method: "POST",
            headers: { "Content-Type": "application/x-www-form-urlencoded" },
            body: body.toString(),
        });

        if (!response.ok) {
            const error = await response.text();
            throw new Error(`Token exchange failed: ${error}`);
        }

        const data = await response.json() as GoogleTokenResponse;
        this.tokens = {
            access_token: data.access_token,
            refresh_token: data.refresh_token,
            expires_at: Date.now() + (data.expires_in * 1000),
            token_type: data.token_type,
            scope: data.scope,
        };

        this.saveTokens();
    }

    private async refreshToken(): Promise<void> {
        if (!this.tokens?.refresh_token) {
            throw new Error("No refresh token available. Please sign in again.");
        }

        const tokenUrl = "https://oauth2.googleapis.com/token";
        const body = new URLSearchParams({
            refresh_token: this.tokens.refresh_token,
            client_id: this.config.clientId,
            client_secret: this.config.clientSecret,
            grant_type: "refresh_token",
        });

        const response = await fetch(tokenUrl, {
            method: "POST",
            headers: { "Content-Type": "application/x-www-form-urlencoded" },
            body: body.toString(),
        });

        if (!response.ok) {
            this.tokens = null;
            this.saveTokens();
            throw new Error("Token refresh failed. Please sign in again.");
        }

        const data = await response.json() as GoogleTokenResponse;
        this.tokens = {
            ...this.tokens,
            access_token: data.access_token,
            expires_at: Date.now() + (data.expires_in * 1000),
        };

        this.saveTokens();
    }

    /**
     * Sign out — remove stored tokens.
     */
    signOut(): void {
        this.tokens = null;
        try {
            if (fs.existsSync(TOKEN_FILE)) {
                fs.unlinkSync(TOKEN_FILE);
            }
        } catch {
            // ignore
        }
    }

    /**
     * Save OAuth config to disk for the installer to use.
     */
    static saveConfig(config: GoogleOAuthConfig): void {
        const dir = path.dirname(CONFIG_FILE);
        if (!fs.existsSync(dir)) {
            fs.mkdirSync(dir, { recursive: true });
        }
        fs.writeFileSync(CONFIG_FILE, JSON.stringify(config, null, 2));
    }

    /**
     * Load OAuth config from disk.
     */
    static loadConfig(): GoogleOAuthConfig | null {
        try {
            if (fs.existsSync(CONFIG_FILE)) {
                return JSON.parse(fs.readFileSync(CONFIG_FILE, "utf-8"));
            }
        } catch {
            // ignore
        }
        return null;
    }
}
