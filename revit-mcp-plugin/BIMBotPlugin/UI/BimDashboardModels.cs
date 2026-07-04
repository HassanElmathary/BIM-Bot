using System.Collections.Generic;

namespace BIMBotPlugin.UI
{
    // ══════════════════════════════════════════════════════════════
    //  BIM Dashboard Data Models
    //  Mirrors the TypeScript DashboardData interface from
    //  dashboard_generator.ts for JSON deserialization.
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Root data payload for the native BIM compliance dashboard.
    /// Deserialized from the MCP 'show_bim_dashboard' command.
    /// </summary>
    public class BimDashboardData
    {
        public string ProjectName { get; set; }
        public string GeneratedAt { get; set; }
        public string BepVersion { get; set; }
        public string MidpVersion { get; set; }
        public int OverallScore { get; set; }
        public int TotalElements { get; set; }
        public int TotalPass { get; set; }
        public int TotalWarn { get; set; }
        public int TotalFail { get; set; }
        public List<CategorySummaryModel> Categories { get; set; } = new List<CategorySummaryModel>();
        public List<ElementRowModel> Elements { get; set; } = new List<ElementRowModel>();
        public List<ComplianceIssueModel> Issues { get; set; } = new List<ComplianceIssueModel>();
        public Dictionary<string, int> LevelDistribution { get; set; } = new Dictionary<string, int>();
        public List<string> ConfiguredCategories { get; set; } = new List<string>();
    }

    /// <summary>
    /// Per-category compliance summary with pass/warn/fail breakdown.
    /// </summary>
    public class CategorySummaryModel
    {
        public string Category { get; set; }
        public int TotalElements { get; set; }
        public int PassCount { get; set; }
        public int WarnCount { get; set; }
        public int FailCount { get; set; }
        public int ComplianceScore { get; set; }
        public int ParameterFillRate { get; set; }
        public Dictionary<string, int> MissingParams { get; set; } = new Dictionary<string, int>();
    }

    /// <summary>
    /// Individual element row for the schedules grid.
    /// </summary>
    public class ElementRowModel
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public string TypeName { get; set; }
        public string Level { get; set; }
        public string Mark { get; set; }
        public string Status { get; set; }  // "pass" | "warning" | "fail"
        public List<string> MissingParams { get; set; } = new List<string>();
        public List<string> Issues { get; set; } = new List<string>();
    }

    /// <summary>
    /// A compliance issue with severity, context, and remediation suggestion.
    /// </summary>
    public class ComplianceIssueModel
    {
        public string ElementId { get; set; }
        public string ElementName { get; set; }
        public string Category { get; set; }
        public string Level { get; set; }
        public string Severity { get; set; }  // "critical" | "warning" | "info"
        public string Rule { get; set; }
        public string Message { get; set; }
        public string Suggestion { get; set; }
    }
}
