/**
 * BIM Dashboard Tools — MCP tools for BEP/MIDP compliance analysis.
 *
 * Tools:
 *  1. generate_bim_dashboard   — Full pipeline: extract → analyse → HTML
 *  2. configure_bep_midp       — Save/update BEP/MIDP config JSON
 *  3. validate_bep_compliance  — Quick text-based compliance check
 */

import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { z } from "zod";
import { withRevitConnection } from "../utils/ConnectionManager.js";
import {
    generateDashboardHtml,
    type DashboardData,
    type CategorySummary,
    type ElementRow,
    type ComplianceIssue,
} from "../utils/dashboard_generator.js";

// ── Types ──────────────────────────────────────────────────────

interface RevitElementsResponse {
    elements?: Record<string, unknown>[];
    hasMore?: boolean;
    totalCount?: number;
    count?: number;
    [key: string]: unknown;
}

interface BepConfig {
    projectInfo: { projectName: string; bepVersion: string; midpVersion: string };
    namingConventions: Record<string, string>;
    requiredParameters: Record<string, string[]>;
    lodRequirements: Record<string, { requiredParams: string[]; categories: string[] }>;
    classificationSystem: string;
    categories: string[];
}

// ── Default config ─────────────────────────────────────────────

const DEFAULT_CONFIG: BepConfig = {
    projectInfo: { projectName: "Untitled", bepVersion: "1.0", midpVersion: "1.0" },
    namingConventions: {
        families: "^[A-Z]{3}_.*$",
        levels: "^(Level|Ground|Roof).*$",
    },
    requiredParameters: {
        Walls: ["Mark", "Comments", "Fire Rating"],
        Doors: ["Mark", "Fire Rating", "Frame Material"],
        Windows: ["Mark", "Sill Height"],
        Rooms: ["Name", "Number", "Department", "Area"],
        Floors: ["Mark", "Structural"],
        default: ["Mark"],
    },
    lodRequirements: {
        LOD200: { requiredParams: ["Mark"], categories: ["Walls", "Floors", "Roofs"] },
        LOD300: { requiredParams: ["Mark", "Comments", "Material"], categories: ["Doors", "Windows", "Columns"] },
        LOD350: { requiredParams: ["Mark", "Comments", "Material", "Manufacturer"], categories: ["Mechanical Equipment", "Plumbing Fixtures"] },
    },
    classificationSystem: "Uniclass2015",
    categories: [
        "Walls", "Floors", "Doors", "Windows", "Rooms",
        "Ceilings", "Roofs", "Columns", "Structural Columns",
        "Structural Framing", "Stairs", "Railings", "Furniture",
        "Plumbing Fixtures", "Mechanical Equipment",
    ],
};

// ── Helpers ─────────────────────────────────────────────────────

async function fetchBatched(category: string, batchSize = 100): Promise<Record<string, unknown>[]> {
    const all: Record<string, unknown>[] = [];
    let offset = 0;
    let hasMore = true;
    while (hasMore) {
        const resp = (await withRevitConnection(async (c) =>
            c.sendCommand("export_elements", { category, offset, limit: batchSize })
        )) as RevitElementsResponse;
        const elems = resp?.elements;
        if (Array.isArray(elems) && elems.length > 0) all.push(...elems);
        hasMore = resp?.hasMore === true;
        offset += batchSize;
        if (offset > 100000) break;
    }
    return all;
}

async function readConfig(): Promise<BepConfig> {
    try {
        const resp = (await withRevitConnection(async (c) =>
            c.sendCommand("read_project_file", { fileName: "bep_config.json" })
        )) as { content?: string; error?: string };
        if (resp?.content) return { ...DEFAULT_CONFIG, ...JSON.parse(resp.content) };
    } catch { /* use default */ }
    return DEFAULT_CONFIG;
}

async function writeProjectFile(fileName: string, content: string): Promise<unknown> {
    return withRevitConnection(async (c) =>
        c.sendCommand("write_project_file", { fileName, content })
    );
}

function str(v: unknown): string {
    if (v === null || v === undefined) return "";
    return String(v);
}

// ── Compliance Engine ──────────────────────────────────────────

function analyseElements(
    elements: Record<string, unknown>[],
    category: string,
    config: BepConfig
): { rows: ElementRow[]; issues: ComplianceIssue[]; summary: CategorySummary } {
    const reqParams = config.requiredParameters[category] ?? config.requiredParameters["default"] ?? ["Mark"];
    const rows: ElementRow[] = [];
    const issues: ComplianceIssue[] = [];
    let pass = 0, warn = 0, fail = 0;
    let totalParams = 0, filledParams = 0;

    for (const el of elements) {
        const id = str(el["id"] || el["Id"]);
        const name = str(el["name"] || el["Name"] || "");
        const typeName = str(el["typeName"] || el["TypeName"] || el["familyType"] || "");
        const level = str(el["level"] || el["Level"] || "");
        const mark = str(el["mark"] || el["Mark"] || "");
        const elIssues: string[] = [];
        let severity: "pass" | "warning" | "fail" = "pass";

        // 1. Required parameters check
        for (const param of reqParams) {
            totalParams++;
            const val = str(el[param] || el[param.toLowerCase()] || "");
            if (val) {
                filledParams++;
            } else {
                elIssues.push(`Missing: ${param}`);
                issues.push({
                    elementId: id, elementName: name, category, level,
                    severity: "warning", rule: "Required Parameter",
                    message: `Parameter "${param}" is empty or missing.`,
                    suggestion: `Fill in the "${param}" parameter for this ${category.slice(0, -1)}.`,
                });
                if (severity === "pass") severity = "warning";
            }
        }

        // 2. Mark check (critical if missing on most categories)
        if (!mark && category !== "Rooms") {
            if (!elIssues.includes("Missing: Mark")) {
                elIssues.push("Missing: Mark");
                issues.push({
                    elementId: id, elementName: name, category, level,
                    severity: "critical", rule: "Mark Required",
                    message: "Element has no Mark value assigned.",
                    suggestion: "Assign a unique Mark identifier.",
                });
            }
            severity = "fail";
        }

        // 3. Level assignment check
        if (!level && !["Rooms", "Furniture"].includes(category)) {
            elIssues.push("No level assigned");
            issues.push({
                elementId: id, elementName: name, category, level: "—",
                severity: "warning", rule: "Level Assignment",
                message: "Element is not assigned to any level.",
                suggestion: "Assign the element to the correct level.",
            });
            if (severity === "pass") severity = "warning";
        }

        // 4. Naming convention check
        const famRegex = config.namingConventions["families"];
        if (famRegex && typeName) {
            try {
                if (!new RegExp(famRegex).test(typeName)) {
                    elIssues.push("Naming convention mismatch");
                    issues.push({
                        elementId: id, elementName: name, category, level,
                        severity: "info", rule: "Naming Convention",
                        message: `Type name "${typeName}" does not match pattern ${famRegex}.`,
                        suggestion: `Rename to match the project naming convention.`,
                    });
                }
            } catch { /* invalid regex, skip */ }
        }

        if (severity === "pass") pass++;
        else if (severity === "warning") warn++;
        else fail++;

        rows.push({ id, name, category, typeName, level, mark, status: severity, missingParams: [], issues: elIssues });
    }

    const total = elements.length || 1;
    const score = Math.round((pass / total) * 100);
    const fillRate = totalParams > 0 ? Math.round((filledParams / totalParams) * 100) : 100;

    const missingCount: Record<string, number> = {};
    for (const p of reqParams) {
        const missing = elements.filter(e => !str(e[p] || e[p.toLowerCase()])).length;
        if (missing > 0) missingCount[p] = missing;
    }

    return {
        rows,
        issues,
        summary: {
            category,
            totalElements: elements.length,
            passCount: pass, warnCount: warn, failCount: fail,
            complianceScore: score,
            parameterFillRate: fillRate,
            missingParams: missingCount,
        },
    };
}

// ── Registration ───────────────────────────────────────────────

export function registerBimDashboardTools(server: McpServer) {

    // ─── 1. Generate BIM Dashboard ─────────────────────────────
    server.tool(
        "generate_bim_dashboard",
        "Analyse the current Revit model for BEP/MIDP compliance and open the native " +
        "BIM Compliance Dashboard window inside Revit. " +
        "Extracts elements from all configured categories, analyses naming conventions, " +
        "required parameters, and LOD compliance.",
        {
            categories: z.array(z.string()).optional().describe(
                "Override category list (default: uses bep_config.json categories)"
            ),
        },
        async (args) => {
            try {
                const config = await readConfig();
                const cats = args.categories ?? config.categories;
                const allRows: ElementRow[] = [];
                const allIssues: ComplianceIssue[] = [];
                const summaries: CategorySummary[] = [];
                const levelDist: Record<string, number> = {};

                for (const cat of cats) {
                    try {
                        const elements = await fetchBatched(cat);
                        if (elements.length === 0) continue;

                        const { rows, issues, summary } = analyseElements(elements, cat, config);
                        allRows.push(...rows);
                        allIssues.push(...issues);
                        summaries.push(summary);

                        for (const r of rows) {
                            const lv = r.level || "Unassigned";
                            levelDist[lv] = (levelDist[lv] || 0) + 1;
                        }
                    } catch (err) {
                        // Category may not exist in the model — skip silently
                        console.error(`Skipping ${cat}:`, err);
                    }
                }

                if (allRows.length === 0) {
                    return { content: [{ type: "text", text: "No elements found in any configured category." }] };
                }

                const totalPass = summaries.reduce((s, c) => s + c.passCount, 0);
                const totalWarn = summaries.reduce((s, c) => s + c.warnCount, 0);
                const totalFail = summaries.reduce((s, c) => s + c.failCount, 0);
                const total = totalPass + totalWarn + totalFail || 1;
                const overallScore = Math.round((totalPass / total) * 100);

                // Sort issues: critical first
                const sevOrder = { critical: 0, warning: 1, info: 2 };
                allIssues.sort((a, b) => sevOrder[a.severity] - sevOrder[b.severity]);

                const dashData: DashboardData = {
                    projectName: config.projectInfo.projectName,
                    generatedAt: new Date().toISOString().replace("T", " ").slice(0, 19),
                    bepVersion: config.projectInfo.bepVersion,
                    midpVersion: config.projectInfo.midpVersion,
                    overallScore,
                    totalElements: allRows.length,
                    totalPass, totalWarn, totalFail,
                    categories: summaries,
                    elements: allRows,
                    issues: allIssues.slice(0, 500), // cap for performance
                    levelDistribution: levelDist,
                    configuredCategories: cats,
                };

                // ── Send to native WPF dashboard window ──────────────
                let nativeResult: string | null = null;
                try {
                    const resp = await withRevitConnection(async (c) =>
                        c.sendCommand("show_bim_dashboard", { data: JSON.stringify(dashData) })
                    );
                    nativeResult = "native";
                    console.log("Native dashboard opened:", resp);
                } catch (nativeErr) {
                    console.error("Native dashboard failed, falling back to HTML:", nativeErr);
                }

                // ── Fallback: also save HTML file ────────────────────
                const html = generateDashboardHtml(dashData);
                await writeProjectFile("BIM-Dashboard.html", html);

                const modeLabel = nativeResult
                    ? "🖥️ Native dashboard window opened in Revit"
                    : "📄 HTML fallback saved (native window unavailable)";

                return {
                    content: [{
                        type: "text",
                        text:
                            `✅ BIM Dashboard generated successfully!\n\n` +
                            `${modeLabel}\n\n` +
                            `📊 Overall Compliance: ${overallScore}%\n` +
                            `📦 Elements analysed: ${allRows.length}\n` +
                            `✅ Pass: ${totalPass} | ⚠️ Warning: ${totalWarn} | ❌ Fail: ${totalFail}\n` +
                            `📁 Categories: ${summaries.map(s => `${s.category} (${s.complianceScore}%)`).join(", ")}\n` +
                            `🔍 Issues found: ${allIssues.length}\n\n` +
                            `HTML backup also saved to _ProjectFiles/BIM-Dashboard.html`,
                    }],
                };
            } catch (error) {
                return { content: [{ type: "text", text: `Dashboard generation failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    // ─── 2. Configure BEP/MIDP ─────────────────────────────────
    server.tool(
        "configure_bep_midp",
        "Save or update the BEP/MIDP configuration for the current project. " +
        "Defines naming conventions, required parameters per category, LOD requirements, " +
        "and the list of categories to audit.",
        {
            projectName: z.string().optional().describe("Project name"),
            bepVersion: z.string().optional().describe("BEP version (default: 1.0)"),
            midpVersion: z.string().optional().describe("MIDP version (default: 1.0)"),
            categories: z.array(z.string()).optional().describe("Categories to audit"),
            requiredParameters: z.record(z.array(z.string())).optional().describe(
                "Required parameters per category, e.g. { Walls: ['Mark','Fire Rating'] }"
            ),
            namingConventions: z.record(z.string()).optional().describe(
                "Regex patterns for naming, e.g. { families: '^[A-Z]{3}_.*$' }"
            ),
        },
        async (args) => {
            try {
                const existing = await readConfig();
                const merged: BepConfig = {
                    ...existing,
                    projectInfo: {
                        projectName: args.projectName ?? existing.projectInfo.projectName,
                        bepVersion: args.bepVersion ?? existing.projectInfo.bepVersion,
                        midpVersion: args.midpVersion ?? existing.projectInfo.midpVersion,
                    },
                    categories: args.categories ?? existing.categories,
                    requiredParameters: args.requiredParameters
                        ? { ...existing.requiredParameters, ...args.requiredParameters }
                        : existing.requiredParameters,
                    namingConventions: args.namingConventions
                        ? { ...existing.namingConventions, ...args.namingConventions }
                        : existing.namingConventions,
                };

                const result = await writeProjectFile("bep_config.json", JSON.stringify(merged, null, 2));

                return {
                    content: [{
                        type: "text",
                        text:
                            `✅ BEP/MIDP configuration saved to _ProjectFiles/bep_config.json\n\n` +
                            `Project: ${merged.projectInfo.projectName}\n` +
                            `BEP: v${merged.projectInfo.bepVersion} | MIDP: v${merged.projectInfo.midpVersion}\n` +
                            `Categories: ${merged.categories.length}\n` +
                            `Required params rules: ${Object.keys(merged.requiredParameters).length}\n\n` +
                            JSON.stringify(result, null, 2),
                    }],
                };
            } catch (error) {
                return { content: [{ type: "text", text: `Config save failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );

    // ─── 3. Validate BEP Compliance (Quick) ────────────────────
    server.tool(
        "validate_bep_compliance",
        "Quick BEP compliance check — returns a text summary without generating the full dashboard. " +
        "Useful for fast spot-checks on specific categories.",
        {
            category: z.string().describe("Revit category to validate, e.g. 'Walls', 'Doors'"),
        },
        async (args) => {
            try {
                const config = await readConfig();
                const elements = await fetchBatched(args.category);

                if (elements.length === 0) {
                    return { content: [{ type: "text", text: `No ${args.category} elements found.` }] };
                }

                const { summary, issues } = analyseElements(elements, args.category, config);

                const criticals = issues.filter(i => i.severity === "critical");
                const warnings = issues.filter(i => i.severity === "warning");
                const infos = issues.filter(i => i.severity === "info");

                let text =
                    `📊 BEP Compliance Report — ${args.category}\n` +
                    `${"═".repeat(45)}\n\n` +
                    `Total elements: ${summary.totalElements}\n` +
                    `Compliance score: ${summary.complianceScore}%\n` +
                    `Parameter fill rate: ${summary.parameterFillRate}%\n\n` +
                    `✅ Pass: ${summary.passCount} | ⚠️ Warning: ${summary.warnCount} | ❌ Fail: ${summary.failCount}\n\n`;

                if (Object.keys(summary.missingParams).length > 0) {
                    text += `Missing Parameters:\n`;
                    for (const [p, count] of Object.entries(summary.missingParams)) {
                        text += `  • ${p}: ${count} elements\n`;
                    }
                    text += "\n";
                }

                if (criticals.length > 0) {
                    text += `❌ Critical Issues (${criticals.length}):\n`;
                    for (const i of criticals.slice(0, 10)) {
                        text += `  • ${i.elementName} (${i.elementId}): ${i.message}\n`;
                    }
                    text += "\n";
                }

                if (warnings.length > 0) {
                    text += `⚠️ Warnings (${warnings.length}):\n`;
                    for (const i of warnings.slice(0, 10)) {
                        text += `  • ${i.elementName} (${i.elementId}): ${i.message}\n`;
                    }
                    text += "\n";
                }

                if (infos.length > 0) {
                    text += `ℹ️ Info (${infos.length}):\n`;
                    for (const i of infos.slice(0, 5)) {
                        text += `  • ${i.elementName}: ${i.message}\n`;
                    }
                }

                return { content: [{ type: "text", text }] };
            } catch (error) {
                return { content: [{ type: "text", text: `Validation failed: ${error instanceof Error ? error.message : String(error)}` }] };
            }
        }
    );
}
