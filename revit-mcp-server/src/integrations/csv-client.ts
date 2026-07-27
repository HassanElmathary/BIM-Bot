/**
 * CSV Client — writes Revit element data to .csv files.
 * UTF-8 with BOM so Excel opens it with correct encoding; CRLF line ends.
 */

import fs from "fs";
import path from "path";
import os from "os";

export interface CsvExportOptions {
    filePath?: string;
    /** Append a TOTAL row summing numeric columns (default: false) */
    totalsRow?: boolean;
}

function escapeCsv(value: unknown): string {
    if (value === null || value === undefined) return "";
    const s = typeof value === "object" ? JSON.stringify(value) : String(value);
    return /[",\r\n]/.test(s) ? `"${s.replace(/"/g, '""')}"` : s;
}

export function exportToCsv(
    data: Record<string, unknown>[],
    options: CsvExportOptions = {}
): string {
    if (!data || data.length === 0) {
        throw new Error("No data to export");
    }

    const filePath =
        options.filePath ||
        path.join(os.homedir(), "Desktop", `revit-export-${Date.now()}.csv`);

    const headerSet = new Set<string>();
    for (const row of data) {
        for (const key of Object.keys(row)) headerSet.add(key);
    }
    const headers = Array.from(headerSet);

    const lines: string[] = [headers.map(escapeCsv).join(",")];
    for (const row of data) {
        lines.push(headers.map((h) => escapeCsv(row[h])).join(","));
    }

    if (options.totalsRow) {
        const totals = headers.map((h, i) => {
            if (i === 0) return escapeCsv(`TOTAL (${data.length} rows)`);
            if (h.toLowerCase() === "id") return "";
            const nums = data
                .map((row) => row[h])
                .filter((v): v is number => typeof v === "number");
            if (nums.length === 0) return "";
            return escapeCsv(Math.round(nums.reduce((a, b) => a + b, 0) * 1000) / 1000);
        });
        lines.push(totals.join(","));
    }

    fs.writeFileSync(filePath, "﻿" + lines.join("\r\n") + "\r\n", "utf8");
    return filePath;
}
