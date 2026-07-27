/**
 * Excel Client — writes Revit element data to .xlsx files.
 * Uses the `exceljs` package for full Excel support.
 */

import ExcelJS from "exceljs";
import path from "path";
import os from "os";

export interface ExcelExportOptions {
    filePath?: string;
    sheetName?: string;
    autoWidth?: boolean;
    /** Append a bold TOTAL row summing numeric columns (default: false) */
    totalsRow?: boolean;
}

/**
 * Export an array of JSON objects to an Excel (.xlsx) file.
 * Each key becomes a column header; each object becomes a row.
 */
export async function exportToExcel(
    data: Record<string, unknown>[],
    options: ExcelExportOptions = {}
): Promise<string> {
    if (!data || data.length === 0) {
        throw new Error("No data to export");
    }

    const sheetName = options.sheetName || "Revit Data";
    const filePath =
        options.filePath ||
        path.join(
            os.homedir(),
            "Desktop",
            `revit-export-${Date.now()}.xlsx`
        );

    const workbook = new ExcelJS.Workbook();
    workbook.creator = "BIM-Bot Data Bridge";
    workbook.created = new Date();

    const worksheet = workbook.addWorksheet(sheetName);

    // Collect all unique keys across all objects for headers
    const headerSet = new Set<string>();
    for (const row of data) {
        for (const key of Object.keys(row)) {
            headerSet.add(key);
        }
    }
    const headers = Array.from(headerSet);

    // Set up columns
    worksheet.columns = headers.map((h) => ({
        header: h,
        key: h,
        width: options.autoWidth !== false ? Math.max(h.length + 4, 12) : 15,
    }));

    // Style header row
    const headerRow = worksheet.getRow(1);
    headerRow.font = { bold: true, color: { argb: "FFFFFFFF" } };
    headerRow.fill = {
        type: "pattern",
        pattern: "solid",
        fgColor: { argb: "FF2E5090" }, // Dark blue
    };
    headerRow.alignment = { vertical: "middle", horizontal: "center" };

    // Add data rows
    for (const item of data) {
        const rowValues: Record<string, unknown> = {};
        for (const h of headers) {
            const val = item[h];
            // Flatten nested objects/arrays to strings
            rowValues[h] =
                val !== null && val !== undefined
                    ? typeof val === "object"
                        ? JSON.stringify(val)
                        : val
                    : "";
        }
        worksheet.addRow(rowValues);
    }

    // Optional TOTAL row summing numeric columns (skips the Id column)
    if (options.totalsRow) {
        const totalValues: Record<string, unknown> = {};
        headers.forEach((h, i) => {
            if (i === 0) {
                totalValues[h] = `TOTAL (${data.length} rows)`;
                return;
            }
            if (h.toLowerCase() === "id") return;
            const nums = data
                .map((row) => row[h])
                .filter((v): v is number => typeof v === "number");
            if (nums.length > 0) {
                totalValues[h] = Math.round(nums.reduce((a, b) => a + b, 0) * 1000) / 1000;
            }
        });
        worksheet.addRow(totalValues);
    }

    // Auto-fit column widths based on content (if enabled)
    if (options.autoWidth !== false) {
        for (const col of worksheet.columns) {
            let maxLen = (col.header as string)?.length || 10;
            col.eachCell?.({ includeEmpty: false }, (cell) => {
                const cellLen = cell.value ? String(cell.value).length : 0;
                if (cellLen > maxLen) maxLen = cellLen;
            });
            col.width = Math.min(maxLen + 2, 50);
        }
    }

    // Add table borders
    worksheet.eachRow((row, rowNumber) => {
        row.eachCell((cell) => {
            cell.border = {
                top: { style: "thin" },
                left: { style: "thin" },
                bottom: { style: "thin" },
                right: { style: "thin" },
            };
        });
        // Alternate row colors for readability
        if (rowNumber > 1 && rowNumber % 2 === 0) {
            row.fill = {
                type: "pattern",
                pattern: "solid",
                fgColor: { argb: "FFF2F6FA" },
            };
        }
    });

    // Style the TOTAL row last so the border/banding pass doesn't override it
    if (options.totalsRow) {
        const totalRow = worksheet.getRow(worksheet.rowCount);
        totalRow.font = { bold: true };
        totalRow.eachCell((cell) => {
            cell.border = { ...cell.border, top: { style: "double" } };
            cell.fill = {
                type: "pattern",
                pattern: "solid",
                fgColor: { argb: "FFE8EDF5" },
            };
        });
    }

    // Write to file
    await workbook.xlsx.writeFile(filePath);

    return filePath;
}
