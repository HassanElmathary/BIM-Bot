/**
 * Google Sheets Client — pushes Revit element data to Google Sheets.
 * Supports two auth modes:
 *   1. OAuth 2.0 (preferred) — user signs in with Google account
 *   2. Service Account (fallback) — uses a JSON key file
 */

import { google, sheets_v4, drive_v3 } from "googleapis";
import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

let sheetsClient: sheets_v4.Sheets | null = null;
let driveClient: drive_v3.Drive | null = null;
let authMode: "oauth" | "service_account" | null = null;

const TOKEN_FILE = path.join(__dirname, "..", "..", "config", "google-tokens.json");

/**
 * Try to create an OAuth2 client from saved tokens.
 */
function getOAuth2Client(): InstanceType<typeof google.auth.OAuth2> | null {
    try {
        if (!fs.existsSync(TOKEN_FILE)) return null;

        const tokens = JSON.parse(fs.readFileSync(TOKEN_FILE, "utf-8"));
        if (!tokens?.access_token) return null;

        const clientId = process.env.GOOGLE_CLIENT_ID || "";
        const clientSecret = process.env.GOOGLE_CLIENT_SECRET || "";
        if (!clientId || clientId === "your_oauth_client_id.apps.googleusercontent.com") return null;

        const oauth2 = new google.auth.OAuth2(clientId, clientSecret);
        oauth2.setCredentials({
            access_token: tokens.access_token,
            refresh_token: tokens.refresh_token,
            expiry_date: tokens.expires_at,
            token_type: tokens.token_type,
        });

        return oauth2;
    } catch {
        return null;
    }
}

async function getSheetsClient(): Promise<sheets_v4.Sheets> {
    if (sheetsClient) return sheetsClient;

    // Try OAuth first
    const oauth2 = getOAuth2Client();
    if (oauth2) {
        sheetsClient = google.sheets({ version: "v4", auth: oauth2 });
        driveClient = google.drive({ version: "v3", auth: oauth2 });
        authMode = "oauth";
        return sheetsClient;
    }

    // Fallback to Service Account
    const credPath =
        process.env.GOOGLE_SHEETS_CREDENTIALS_PATH || "./credentials.json";
    const resolvedPath = path.resolve(credPath);

    if (!fs.existsSync(resolvedPath)) {
        throw new Error(
            `Google Sheets not connected.\n` +
            `Option 1: Sign in with Google using the Integrations panel.\n` +
            `Option 2: Set up a Service Account and set GOOGLE_SHEETS_CREDENTIALS_PATH in .env`
        );
    }

    const auth = new google.auth.GoogleAuth({
        keyFile: resolvedPath,
        scopes: [
            "https://www.googleapis.com/auth/spreadsheets",
            "https://www.googleapis.com/auth/drive.readonly",
        ],
    });

    sheetsClient = google.sheets({ version: "v4", auth });
    driveClient = google.drive({ version: "v3", auth });
    authMode = "service_account";
    return sheetsClient;
}

async function getDriveClient(): Promise<drive_v3.Drive> {
    if (driveClient) return driveClient;
    await getSheetsClient(); // This will also init driveClient
    if (!driveClient) throw new Error("Drive client not initialized");
    return driveClient;
}

/**
 * Reset clients (e.g., after sign-in/sign-out).
 */
export function resetSheetsClient(): void {
    sheetsClient = null;
    driveClient = null;
    authMode = null;
}

/**
 * Get current auth mode.
 */
export function getAuthMode(): string {
    return authMode || "none";
}

/**
 * Check if OAuth tokens exist and are valid-ish.
 */
export function isOAuthConnected(): boolean {
    try {
        if (!fs.existsSync(TOKEN_FILE)) return false;
        const tokens = JSON.parse(fs.readFileSync(TOKEN_FILE, "utf-8"));
        return !!(tokens?.access_token && tokens?.expires_at > Date.now());
    } catch {
        return false;
    }
}

// ── Spreadsheet types ──

export interface SpreadsheetInfo {
    id: string;
    name: string;
    modifiedTime: string;
    url: string;
}

export interface SheetsUpdateOptions {
    spreadsheetId: string;
    range?: string; // Default: "Sheet1!A1"
    clearFirst?: boolean; // Clear existing data before writing
}

export interface SheetsUpdateResult {
    updatedRange: string;
    updatedRows: number;
    updatedColumns: number;
    spreadsheetUrl: string;
}

// ── Functions ──

/**
 * List the user's Google Sheets spreadsheets.
 */
export async function listSpreadsheets(maxResults: number = 20): Promise<SpreadsheetInfo[]> {
    const drive = await getDriveClient();

    const response = await drive.files.list({
        q: "mimeType='application/vnd.google-apps.spreadsheet' and trashed=false",
        fields: "files(id, name, modifiedTime, webViewLink)",
        orderBy: "modifiedTime desc",
        pageSize: maxResults,
    });

    const files = response.data.files || [];
    return files.map((f) => ({
        id: f.id || "",
        name: f.name || "Untitled",
        modifiedTime: f.modifiedTime || "",
        url: f.webViewLink || `https://docs.google.com/spreadsheets/d/${f.id}`,
    }));
}

/**
 * Write Revit element data to a Google Sheet.
 * First row = headers, subsequent rows = data.
 */
export async function updateSheets(
    data: Record<string, unknown>[],
    options: SheetsUpdateOptions
): Promise<SheetsUpdateResult> {
    if (!data || data.length === 0) {
        throw new Error("No data to export");
    }

    const sheets = await getSheetsClient();
    const range = options.range || "Sheet1!A1";

    // Collect all unique keys as headers
    const headerSet = new Set<string>();
    for (const row of data) {
        for (const key of Object.keys(row)) {
            headerSet.add(key);
        }
    }
    const headers = Array.from(headerSet);

    // Build 2D values array: [headers, ...rows]
    const values: (string | number | boolean)[][] = [];
    values.push(headers);

    for (const item of data) {
        const row: (string | number | boolean)[] = [];
        for (const h of headers) {
            const val = item[h];
            if (val === null || val === undefined) {
                row.push("");
            } else if (typeof val === "object") {
                row.push(JSON.stringify(val));
            } else {
                row.push(val as string | number | boolean);
            }
        }
        values.push(row);
    }

    // Clear existing data if requested
    if (options.clearFirst) {
        try {
            await sheets.spreadsheets.values.clear({
                spreadsheetId: options.spreadsheetId,
                range: range.split("!")[0], // Clear entire sheet
            });
        } catch {
            // Ignore clear errors (sheet might not exist yet)
        }
    }

    // Write data
    const response = await sheets.spreadsheets.values.update({
        spreadsheetId: options.spreadsheetId,
        range,
        valueInputOption: "USER_ENTERED",
        requestBody: { values },
    });

    return {
        updatedRange: response.data.updatedRange || range,
        updatedRows: response.data.updatedRows || values.length,
        updatedColumns: response.data.updatedColumns || headers.length,
        spreadsheetUrl: `https://docs.google.com/spreadsheets/d/${options.spreadsheetId}`,
    };
}

/**
 * Read data from a Google Sheet.
 */
export async function readSheets(
    spreadsheetId: string,
    range: string = "Sheet1"
): Promise<unknown[][]> {
    const sheets = await getSheetsClient();

    const response = await sheets.spreadsheets.values.get({
        spreadsheetId,
        range,
    });

    return (response.data.values as unknown[][]) || [];
}
