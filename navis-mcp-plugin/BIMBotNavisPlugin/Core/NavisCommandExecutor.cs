using System;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace BIMBotNavisPlugin.Core
{
    /// <summary>
    /// Dispatches navis_* JSON-RPC methods to the Navisworks Manage API.
    /// NOTE: Requires Navisworks Manage reference at build time
    /// (Autodesk.Navisworks.Api.dll). Each method degrades gracefully:
    /// - Simulate (no Clash API) → "requires Navisworks Manage" error
    /// - No open document → clear error, never crash
    ///
    /// Full implementation binds to:
    ///   Autodesk.Navisworks.Api.Application.ActiveDocument
    ///   Document.Models / CurrentSelection / SavedViewpoints / SelectionSets
    ///   DocumentClash (Manage-only) / TimelinerDocument
    /// This scaffold compiles WITHOUT the Navisworks DLLs (NAVISHASAPI undefined)
    /// so the MCP server + installer work can proceed; binding is filled in
    /// on a machine with Manage installed by defining NAVISHASAPI.
    /// </summary>
    public static class NavisCommandExecutor
    {
        public static JObject Execute(string method, JObject pars)
        {
#if NAVISHASAPI
            return ExecuteLive(method, pars);
#else
            return ExecuteStub(method, pars);
#endif
        }

#if NAVISHASAPI
        private static JObject ExecuteLive(string method, JObject pars)
        {
            // Live binding (filled on dev machine with Manage installed).
            // Keep one case per navis_* tool from revit-mcp-server/src/tools/navis_*.ts.
            // All Document access must run on the Navisworks main thread —
            // the caller (plugin command) marshals via Application.ExecuteDeferred
            // or Control.Invoke before reaching here.
            throw new NotImplementedException($"Live Navisworks binding for '{method}' — build with NAVISHASAPI on a Manage machine.");
        }
#endif

        private static JObject ExecuteStub(string method, JObject pars)
        {
            // Stub keeps the protocol + MCP tools testable without Manage installed.
            return method switch
            {
                "navis_get_model_info" => new JObject
                {
                    ["product"] = "navisworks",
                    ["status"] = "stub — build with NAVISHASAPI on a Navisworks Manage machine",
                    ["pluginVersion"] = NavisPluginApp.Version,
                    ["defaultPort"] = 8091,
                },
                "navis_list_clash_tests" => throw new Exception("Requires Navisworks Manage with a model open (stub build)."),
                "navis_run_clash_test" => throw new Exception("Requires Navisworks Manage with a model open (stub build)."),
                "navis_get_clash_results" => throw new Exception("Requires Navisworks Manage with a model open (stub build)."),
                "navis_set_clash_status" => throw new Exception("Requires Navisworks Manage with a model open (stub build)."),
                "navis_export_clash_report" => throw new Exception("Requires Navisworks Manage with a model open (stub build)."),
                _ => throw new Exception($"Unknown Navisworks command '{method}' (stub build — {pars.Count} params received)."),
            };
        }
    }
}
