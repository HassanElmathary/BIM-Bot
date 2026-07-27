/**
 * Renders the audit_model_standards result as a self-contained HTML report.
 * Print-friendly (File → Print → Save as PDF). Each fixable violation group
 * has a "Copy fix prompt" button that puts a ready-to-paste instruction for
 * Claude/BIM-Bot chat on the clipboard — a static file cannot call Revit
 * directly, so the prompt is the bridge back into the live session.
 */

export interface AuditViolation {
    ruleId: string;
    ruleType: string;
    severity: string;
    message: string;
    elementId: number;
    elementName: string;
    category: string;
    fixable: boolean;
    fixed: boolean;
}

export interface AuditResult {
    standardName: string;
    projectName: string;
    auditDate: string;
    totalViolations: number;
    errors: number;
    warnings: number;
    fixedCount: number;
    fixApplied: boolean;
    violations: AuditViolation[];
}

function esc(s: unknown): string {
    return String(s ?? "")
        .replace(/&/g, "&amp;").replace(/</g, "&lt;")
        .replace(/>/g, "&gt;").replace(/"/g, "&quot;");
}

const FIX_PROMPTS: Record<string, (ruleId: string) => string> = {
    naming: () =>
        "Use find_replace_names to fix the names flagged in my BIM-Bot audit report.",
    requiredParameter: (ruleId) =>
        `Run audit_model_standards with fix=true to fill the missing parameters flagged by rule ${ruleId} (set a defaultValue in the standard first).`,
    lineWeight: () =>
        "Run audit_model_standards with fix=true to reset object-style line weights to the standard.",
    viewTemplate: () =>
        "Run audit_model_standards with fix=true to assign the default view template to the flagged views (set views.defaultTemplate in the standard).",
    health: () =>
        "Ask BIM-Bot: review the model-health violations in my audit report (check_warnings, find_cad_imports).",
};

export function renderAuditReportHtml(result: AuditResult): string {
    const byRule = new Map<string, AuditViolation[]>();
    for (const v of result.violations) {
        const list = byRule.get(v.ruleId) ?? [];
        list.push(v);
        byRule.set(v.ruleId, list);
    }

    const date = new Date(result.auditDate);
    const compliant = result.totalViolations === 0;

    const sections = Array.from(byRule.entries()).map(([ruleId, list]) => {
        const first = list[0];
        const sev = first.severity === "error" ? "error" : "warning";
        const fixable = list.some((v) => v.fixable && !v.fixed);
        const prompt = FIX_PROMPTS[first.ruleType]?.(ruleId) ?? "";
        const rows = list.map((v) => `
        <tr>
          <td class="mono">${v.elementId || ""}</td>
          <td>${esc(v.elementName)}</td>
          <td>${esc(v.category)}</td>
          <td>${esc(v.message)}</td>
          <td>${v.fixed ? "✅ fixed" : v.fixable ? "fixable" : "manual"}</td>
        </tr>`).join("");

        return `
    <section>
      <h2>
        <span class="badge ${sev}">${sev}</span>
        ${esc(ruleId)}
        <span class="count">${list.length} violation${list.length === 1 ? "" : "s"}</span>
        ${fixable && prompt ? `<button class="fix" data-prompt="${esc(prompt)}">🔧 Copy fix prompt</button>` : ""}
      </h2>
      <table>
        <thead><tr><th>Element ID</th><th>Name</th><th>Category</th><th>Issue</th><th>Fix</th></tr></thead>
        <tbody>${rows}</tbody>
      </table>
    </section>`;
    }).join("\n");

    return `<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>BIM-Bot Audit — ${esc(result.projectName)}</title>
<style>
  :root { color-scheme: light dark; }
  * { box-sizing: border-box; }
  body {
    font: 14px/1.5 "Segoe UI", system-ui, sans-serif;
    margin: 0 auto; max-width: 960px; padding: 32px 24px;
    color: #1a1d23; background: #fff;
  }
  header { border-bottom: 3px solid #2e5090; padding-bottom: 16px; margin-bottom: 24px; }
  h1 { margin: 0 0 4px; font-size: 22px; color: #2e5090; }
  .meta { color: #667; font-size: 13px; }
  .verdict { margin: 16px 0; padding: 12px 16px; border-radius: 8px; font-weight: 600; }
  .verdict.pass { background: #e6f4ea; color: #17643a; border: 1px solid #b7dfc4; }
  .verdict.fail { background: #fdecea; color: #90261c; border: 1px solid #f4c3bd; }
  section { margin: 24px 0; break-inside: avoid; }
  h2 { font-size: 15px; display: flex; align-items: center; gap: 10px; flex-wrap: wrap;
       border-bottom: 1px solid #dde; padding-bottom: 6px; }
  .badge { font-size: 11px; text-transform: uppercase; padding: 2px 8px; border-radius: 10px; font-weight: 700; }
  .badge.error { background: #fdecea; color: #b3261e; }
  .badge.warning { background: #fff4e0; color: #8a5300; }
  .count { color: #667; font-weight: 400; font-size: 13px; }
  button.fix { margin-left: auto; border: 1px solid #2e5090; color: #2e5090; background: none;
       padding: 4px 12px; border-radius: 6px; cursor: pointer; font-size: 12px; }
  button.fix:hover { background: #2e5090; color: #fff; }
  table { border-collapse: collapse; width: 100%; font-size: 13px; }
  th { text-align: left; background: #f0f3f9; padding: 6px 10px; border-bottom: 2px solid #cdd6e6; }
  td { padding: 5px 10px; border-bottom: 1px solid #e8ecf3; vertical-align: top; }
  tr:nth-child(even) td { background: #f8fafd; }
  .mono { font-family: Consolas, monospace; }
  .summary-line { font-size: 14px; }
  footer { margin-top: 40px; padding-top: 12px; border-top: 1px solid #dde;
       color: #889; font-size: 12px; }
  @media (prefers-color-scheme: dark) {
    body { color: #dfe3ea; background: #16181d; }
    h1 { color: #7fa4e0; }
    header { border-color: #7fa4e0; }
    th { background: #22262e; border-color: #39404d; }
    td { border-color: #262b34; }
    tr:nth-child(even) td { background: #1b1f26; }
    h2 { border-color: #333a45; }
    .verdict.pass { background: #10281a; color: #7fd8a2; border-color: #1d4a2f; }
    .verdict.fail { background: #331512; color: #f2998e; border-color: #5c231c; }
  }
  @media print {
    button.fix { display: none; }
    body { max-width: none; padding: 0; }
  }
</style>
</head>
<body>
<header>
  <h1>🤖 BIM-Bot Model Audit</h1>
  <div class="meta">
    <strong>${esc(result.projectName)}</strong> ·
    Standard: ${esc(result.standardName)} ·
    ${date.toLocaleString()}
  </div>
</header>

<div class="verdict ${compliant ? "pass" : "fail"}">
  ${compliant
        ? "✅ Model is fully compliant with the standard."
        : `❌ ${result.totalViolations} violation${result.totalViolations === 1 ? "" : "s"} found — ` +
          `${result.errors} error${result.errors === 1 ? "" : "s"}, ${result.warnings} warning${result.warnings === 1 ? "" : "s"}` +
          (result.fixApplied ? ` · ${result.fixedCount} auto-fixed this run` : "")}
</div>

${result.fixApplied && result.fixedCount > 0
        ? `<p class="summary-line">🔧 ${result.fixedCount} violation${result.fixedCount === 1 ? " was" : "s were"} fixed automatically; rows marked "✅ fixed" below.</p>`
        : ""}

${sections || ""}

<footer>
  Generated by BIM-Bot audit_model_standards · Print this page to save as PDF ·
  "Copy fix prompt" buttons put a ready instruction on the clipboard — paste it to Claude or the BIM-Bot chat.
</footer>

<script>
  document.querySelectorAll("button.fix").forEach((btn) => {
    btn.addEventListener("click", async () => {
      try {
        await navigator.clipboard.writeText(btn.dataset.prompt);
        const old = btn.textContent;
        btn.textContent = "✅ Copied — paste to BIM-Bot";
        setTimeout(() => { btn.textContent = old; }, 2500);
      } catch {
        prompt("Copy this fix instruction:", btn.dataset.prompt);
      }
    });
  });
</script>
</body>
</html>`;
}
