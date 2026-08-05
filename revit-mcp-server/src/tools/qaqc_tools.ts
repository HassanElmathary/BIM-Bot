import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { z } from "zod";
import fs from "fs";
import path from "path";
import os from "os";
import { withRevitConnection } from "../utils/ConnectionManager.js";
import { DEFAULT_STANDARD, AuditStandards } from "../standards/default-standard.js";
import { renderAuditReportHtml, AuditResult } from "../utils/audit-report.js";
import { appendHistory, getPreviousScore } from "../utils/audit-history.js";

/**
 * QA/QC Tools — model quality control and compliance checking
 */
export function registerQAQCTools(server: McpServer) {

    // 0. Automated standards audit with HTML report
    server.tool(
        "audit_model_standards",
        "Audit the active model against a JSON standard set (naming, required parameters, " +
        "line weights, view templates, health, workset assignments, phase compliance, shared parameters). " +
        "Returns a 0-100% compliance score and violation list with BEP clause references. " +
        "Pass fix=true to auto-apply safe fixes in one transaction. " +
        "Pass exportBaseline=true to export the current model's config as a reusable standard. " +
        "Uses the built-in BIM-Bot standard unless `standards`, `standardsPath`, or `standardsUrl` is given.",
        {
            standardsPath: z.string().optional().describe(
                "Path to a JSON standards file (same shape as the built-in default)"
            ),
            standardsUrl: z.string().url().optional().describe(
                "URL to load a standards JSON file from (e.g., raw GitHub URL). Fetched with 10s timeout."
            ),
            standards: z.record(z.unknown()).optional().describe(
                "Inline standards object; overrides standardsPath and standardsUrl"
            ),
            fix: z.boolean().optional().describe(
                "Auto-apply safe fixes: fill defaultValue parameters, assign views.defaultTemplate, set line weights, fix worksets, apply phase filters (default: false)"
            ),
            exportBaseline: z.boolean().optional().describe(
                "If true, export the current model's configuration as a reusable standard JSON instead of auditing"
            ),
            outputFormat: z.enum(["html", "json", "pdf"]).optional().describe(
                "html writes a styled report file (default); json returns raw results inline; pdf opens HTML report in browser for Print → Save as PDF"
            ),
            filePath: z.string().optional().describe(
                "Report output path. Default: Desktop/bimbot-audit-<timestamp>.html"
            ),
        },
        async (args) => {
            try {
                // ── Resolve standards ──
                let standards: AuditStandards = DEFAULT_STANDARD;
                if (args.standards) {
                    standards = args.standards as unknown as AuditStandards;
                } else if (args.standardsPath) {
                    standards = JSON.parse(fs.readFileSync(args.standardsPath, "utf8"));
                } else if (args.standardsUrl) {
                    try {
                        const controller = new AbortController();
                        const timeout = setTimeout(() => controller.abort(), 10000);
                        const resp = await fetch(args.standardsUrl, { signal: controller.signal });
                        clearTimeout(timeout);
                        if (!resp.ok) throw new Error(`HTTP ${resp.status}`);
                        standards = (await resp.json()) as AuditStandards;
                    } catch (urlErr) {
                        return { content: [{ type: "text" as const, text:
                            `Failed to fetch standards from URL: ${urlErr instanceof Error ? urlErr.message : String(urlErr)}. ` +
                            `Falling back to built-in default standard is not automatic — please fix the URL or use standardsPath/standards instead.` }] };
                    }
                }
                if (!standards?.rules) {
                    return { content: [{ type: "text" as const, text:
                        "Invalid standards: expected an object with a `rules` section (naming / requiredParameters / lineWeights / views / health / worksetAssignments / phaseCompliance / sharedParameters)." }] };
                }

                // ── Send to plugin ──
                const result = (await withRevitConnection(async (client) =>
                    client.sendCommand("audit_model_standards", {
                        standards,
                        fix: args.fix === true,
                        exportBaseline: args.exportBaseline === true,
                    })
                )) as AuditResult & { standard?: unknown; tip?: string; message?: string };

                // ── Baseline export mode ──
                if (args.exportBaseline) {
                    return { content: [{ type: "text" as const, text:
                        JSON.stringify(result, null, 2) }] };
                }

                // ── History tracking & score delta ──
                const previousScore = getPreviousScore(result.projectName);
                const scoreDelta = previousScore != null && result.complianceScore != null
                    ? Math.round((result.complianceScore - previousScore) * 10) / 10
                    : null;
                result.scoreDelta = scoreDelta;

                if (result.complianceScore != null) {
                    appendHistory({
                        date: result.auditDate,
                        projectName: result.projectName,
                        standardName: result.standardName,
                        complianceScore: result.complianceScore,
                        totalViolations: result.totalViolations,
                        totalEvaluations: result.totalEvaluations ?? 0,
                        errors: result.errors,
                        warnings: result.warnings,
                    });
                }

                // ── Build headline ──
                const scoreStr = result.complianceScore != null ? ` · Score: ${result.complianceScore.toFixed(1)}%` : "";
                const deltaStr = scoreDelta != null ? ` (${scoreDelta >= 0 ? "+" : ""}${scoreDelta.toFixed(1)}% vs last)` : "";
                const headline =
                    `${result.totalViolations === 0 ? "✅ Fully compliant" : `❌ ${result.totalViolations} violations (${result.errors} errors, ${result.warnings} warnings)`}` +
                    ` — '${result.projectName}' vs '${result.standardName}'` +
                    scoreStr + deltaStr +
                    (result.fixApplied ? ` · ${result.fixedCount} auto-fixed` : "");

                // ── JSON output ──
                if ((args.outputFormat ?? "html") === "json") {
                    return { content: [{ type: "text" as const, text:
                        `${headline}\n\n${JSON.stringify(result, null, 2)}` }] };
                }

                // ── HTML/PDF output ──
                const reportPath = args.filePath ?? path.join(
                    os.homedir(), "Desktop", `bimbot-audit-${Date.now()}.html`
                );
                fs.writeFileSync(reportPath, renderAuditReportHtml(result), "utf8");

                let outputNote = `Open it in a browser — fixable groups have "Copy fix prompt" buttons; print the page to save as PDF.`;
                if (args.outputFormat === "pdf") {
                    // Auto-open in browser for Print → Save as PDF
                    try {
                        const open = (await import("open")).default;
                        await open(reportPath);
                        outputNote = `Report opened in browser — use Print (Ctrl+P) → Save as PDF.`;
                    } catch {
                        outputNote = `Could not auto-open report. Open manually: ${reportPath}`;
                    }
                }

                return { content: [{ type: "text" as const, text:
                    `${headline}\n📄 Report: ${reportPath}\n${outputNote}` +
                    (result.totalViolations > 0 && !result.fixApplied
                        ? `\nRun again with fix=true to auto-apply the safe fixes.` : "") }] };
            } catch (error) {
                return { content: [{ type: "text" as const, text:
                    `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    // 1. Check warnings
    server.tool(
        "check_warnings",
        "Get all active warnings in the Revit model. Useful for model cleanup and quality control.",
        {
            severity: z.enum(["All", "Error", "Warning"]).optional().describe("Filter by severity (default: All)"),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("check_warnings", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    // 2. Audit model
    server.tool(
        "audit_model",
        "Perform a comprehensive model audit: check for orphaned elements, unused families, missing views on sheets, etc.",
        {},
        async () => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("audit_model", {})
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    // 3. Check room compliance
    server.tool(
        "check_room_compliance",
        "Check rooms for compliance: minimum area requirements, accessibility, proper naming, and boundary closure.",
        {
            minArea: z.number().optional().describe("Minimum room area in square feet"),
            checkAccessibility: z.boolean().optional().describe("Check accessibility compliance (default: false)"),
            checkNaming: z.boolean().optional().describe("Check naming conventions (default: false)"),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("check_room_compliance", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    // 4. Check naming conventions
    server.tool(
        "check_naming_conventions",
        "Verify that views, sheets, and families follow naming conventions (regex pattern matching).",
        {
            category: z.enum(["Views", "Sheets", "Families", "Levels", "All"]).describe("Category to check"),
            pattern: z.string().optional().describe("Regex pattern for valid names (optional)"),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("check_naming_conventions", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    // 5. Find duplicates
    server.tool(
        "find_duplicates",
        "Find duplicate elements in the model (overlapping walls, duplicate rooms, etc.).",
        {
            category: z.string().describe("Category to check for duplicates, e.g. 'Walls', 'Rooms', 'Doors'"),
            tolerance: z.number().optional().describe("Distance tolerance in feet for considering elements as duplicates (default: 0.01)"),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("find_duplicates", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    // 6. Purge unused
    server.tool(
        "purge_unused",
        "Find and optionally purge unused families, types, and materials from the model.",
        {
            dryRun: z.boolean().optional().describe("If true, only lists what would be purged without deleting (default: true)"),
            categories: z.array(z.string()).optional().describe("Specific categories to purge (default: all)"),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("purge_unused", { dryRun: args.dryRun ?? true, categories: args.categories })
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    // 7. Check links status
    server.tool(
        "check_links_status",
        "Check the status of all linked models (loaded, unloaded, missing).",
        {},
        async () => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("check_links_status", {})
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    // 8. Validate parameters
    server.tool(
        "validate_parameters",
        "Validate that required parameters are filled in for elements of a specific category.",
        {
            category: z.string().describe("Category to validate, e.g. 'Doors', 'Windows', 'Rooms'"),
            requiredParameters: z.array(z.string()).describe("List of parameter names that must have values"),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("validate_parameters", args)
                );
                return { content: [{ type: "text", text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    // 9. Validate shared coordinates
    server.tool(
        "validate_shared_coordinates",
        "Check if all linked models share the same shared coordinate system as the host or a master link. " +
        "Reports per-link offsets (X, Y, Z in mm) and rotation angle. Optionally acquires coordinates from a master link to fix misalignment.",
        {
            masterLinkName: z.string().optional().describe("Optional link name to use as the master coordinate reference. If omitted, uses host model coordinates."),
            tolerance: z.number().optional().describe("Coordinate mismatch threshold in mm. Default is 1.0 mm."),
            autoFix: z.boolean().optional().describe("If true, acquires coordinates from the master link to align the host model."),
        },
        async (args) => {
            try {
                const response = await withRevitConnection(async (client) =>
                    client.sendCommand("validate_shared_coordinates", args)
                );
                return { content: [{ type: "text" as const, text: JSON.stringify(response, null, 2) }] };
            } catch (error) {
                return { content: [{ type: "text" as const, text: `Failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );
}
