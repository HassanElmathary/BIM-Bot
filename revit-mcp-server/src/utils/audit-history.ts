/**
 * Lightweight JSON store for audit history — tracks compliance scores
 * across runs to enable trend tracking and delta reporting.
 * Stored at ~/.bimbot/audit_history.json
 */

import fs from "fs";
import path from "path";
import os from "os";

export interface AuditHistoryEntry {
    date: string;
    projectName: string;
    standardName: string;
    complianceScore: number;
    totalViolations: number;
    totalEvaluations: number;
    errors: number;
    warnings: number;
}

const HISTORY_DIR = path.join(os.homedir(), ".bimbot");
const HISTORY_FILE = path.join(HISTORY_DIR, "audit_history.json");

export function loadHistory(): AuditHistoryEntry[] {
    try {
        if (!fs.existsSync(HISTORY_FILE)) return [];
        const raw = fs.readFileSync(HISTORY_FILE, "utf8");
        const parsed = JSON.parse(raw);
        return Array.isArray(parsed) ? parsed : [];
    } catch {
        return [];
    }
}

export function appendHistory(entry: AuditHistoryEntry): void {
    try {
        if (!fs.existsSync(HISTORY_DIR)) {
            fs.mkdirSync(HISTORY_DIR, { recursive: true });
        }
        const history = loadHistory();
        history.push(entry);

        // Keep last 500 entries to avoid unbounded growth
        const trimmed = history.slice(-500);
        fs.writeFileSync(HISTORY_FILE, JSON.stringify(trimmed, null, 2), "utf8");
    } catch (err) {
        console.error("Failed to save audit history:", err);
    }
}

/**
 * Get the most recent previous score for a given project name.
 * Returns null if no previous entry exists.
 */
export function getPreviousScore(projectName: string): number | null {
    const history = loadHistory();
    // Find the most recent entry for this project (excluding the very last one
    // which might be the current run if called after appendHistory)
    for (let i = history.length - 1; i >= 0; i--) {
        if (history[i].projectName === projectName) {
            return history[i].complianceScore;
        }
    }
    return null;
}
