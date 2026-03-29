/**
 * SQLite Client — local storage for Revit data snapshots.
 * Uses better-sqlite3 for fast synchronous operations.
 * Stores timestamped snapshots for delta-sync comparison.
 */

import Database from "better-sqlite3";
import path from "path";
import fs from "fs";

let db: Database.Database | null = null;

function getDatabase(): Database.Database {
    if (!db) {
        const dbDir = process.env.SQLITE_DB_DIR || ".";
        const dbPath = path.resolve(dbDir, "revit-data.db");

        // Ensure directory exists
        const dir = path.dirname(dbPath);
        if (!fs.existsSync(dir)) {
            fs.mkdirSync(dir, { recursive: true });
        }

        db = new Database(dbPath);

        // Enable WAL mode for better concurrent read performance
        db.pragma("journal_mode = WAL");

        // Create the snapshots metadata table
        db.exec(`
            CREATE TABLE IF NOT EXISTS sync_snapshots (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                table_name TEXT NOT NULL,
                category TEXT NOT NULL,
                element_count INTEGER NOT NULL,
                created_at TEXT NOT NULL DEFAULT (datetime('now')),
                metadata TEXT
            )
        `);
    }
    return db;
}

export interface SnapshotResult {
    tableName: string;
    rowsInserted: number;
    snapshotId: number;
    dbPath: string;
}

/**
 * Save a snapshot of Revit data to SQLite.
 * Creates a table named after the category (e.g., "doors", "walls")
 * and inserts all elements. Each snapshot is timestamped.
 */
export function saveSnapshot(
    data: Record<string, unknown>[],
    category: string,
    tableName?: string
): SnapshotResult {
    if (!data || data.length === 0) {
        throw new Error("No data to save");
    }

    const database = getDatabase();
    const safeName = (tableName || category).toLowerCase().replace(/[^a-z0-9_]/g, "_");

    // Collect all keys
    const keys = new Set<string>();
    for (const item of data) {
        for (const key of Object.keys(item)) {
            keys.add(key);
        }
    }
    const columns = Array.from(keys);

    // Create table if not exists (all columns as TEXT for flexibility)
    const colDefs = columns.map((c) => `"${c}" TEXT`).join(", ");
    database.exec(
        `CREATE TABLE IF NOT EXISTS "${safeName}" (
            _snapshot_id INTEGER,
            _created_at TEXT DEFAULT (datetime('now')),
            ${colDefs}
        )`
    );

    // Get next snapshot ID
    const snapshotRow = database
        .prepare(`SELECT COALESCE(MAX(_snapshot_id), 0) + 1 as next_id FROM "${safeName}"`)
        .get() as { next_id: number };
    const snapshotId = snapshotRow.next_id;

    // Insert data in a transaction for speed
    const placeholders = columns.map(() => "?").join(", ");
    const insertStmt = database.prepare(
        `INSERT INTO "${safeName}" (_snapshot_id, ${columns.map((c) => `"${c}"`).join(", ")}) VALUES (?, ${placeholders})`
    );

    const insertMany = database.transaction((items: Record<string, unknown>[]) => {
        for (const item of items) {
            const values = columns.map((c) => {
                const val = item[c];
                if (val === null || val === undefined) return null;
                if (typeof val === "object") return JSON.stringify(val);
                return String(val);
            });
            insertStmt.run(snapshotId, ...values);
        }
    });

    insertMany(data);

    // Record snapshot metadata
    database
        .prepare(
            `INSERT INTO sync_snapshots (table_name, category, element_count) VALUES (?, ?, ?)`
        )
        .run(safeName, category, data.length);

    return {
        tableName: safeName,
        rowsInserted: data.length,
        snapshotId,
        dbPath: (db as Database.Database & { name?: string })?.name || "revit-data.db",
    };
}

/**
 * Get all snapshots for a table.
 */
export function getSnapshots(
    tableName: string
): { id: number; category: string; element_count: number; created_at: string }[] {
    const database = getDatabase();
    const safeName = tableName.toLowerCase().replace(/[^a-z0-9_]/g, "_");
    return database
        .prepare(
            `SELECT id, category, element_count, created_at FROM sync_snapshots WHERE table_name = ? ORDER BY created_at DESC`
        )
        .all(safeName) as { id: number; category: string; element_count: number; created_at: string }[];
}

/**
 * Get data from a specific snapshot.
 */
export function getSnapshotData(
    tableName: string,
    snapshotId?: number
): Record<string, unknown>[] {
    const database = getDatabase();
    const safeName = tableName.toLowerCase().replace(/[^a-z0-9_]/g, "_");

    let query: string;
    let params: unknown[];

    if (snapshotId) {
        query = `SELECT * FROM "${safeName}" WHERE _snapshot_id = ?`;
        params = [snapshotId];
    } else {
        // Get latest snapshot
        query = `SELECT * FROM "${safeName}" WHERE _snapshot_id = (SELECT MAX(_snapshot_id) FROM "${safeName}")`;
        params = [];
    }

    const rows = database.prepare(query).all(...params) as Record<string, unknown>[];

    // Remove internal columns from output
    return rows.map((row) => {
        const clean = { ...row };
        delete clean._snapshot_id;
        delete clean._created_at;
        return clean;
    });
}

/**
 * Close the database connection.
 */
export function closeDatabase(): void {
    if (db) {
        db.close();
        db = null;
    }
}
