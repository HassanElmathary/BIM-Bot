import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { z } from "zod";
import { getGeminiService } from "../ai/gemini-service.js";
import { withNavisConnection } from "../utils/navisConnectionManager.js";

/**
 * Navisworks AI tools — ADDITIVE-ONLY.
 * Reuses the existing Gemini service (same GEMINI_API_KEY). Existing ai_* tools untouched.
 */
export function registerNavisAITools(server: McpServer) {
    const gemini = getGeminiService();

    server.tool(
        "navis_ai_chat",
        "Send a message to Gemini AI for Navisworks coordination help. Maintains conversation context (separate from Revit ai_chat).",
        {
            message: z.string().describe("Your message or question for the AI"),
            model: z.enum(["gemini-2.5-flash", "gemini-2.5-pro"]).optional().describe("AI model (default: gemini-2.5-flash)"),
        },
        async (args) => {
            try {
                if (args.model) gemini.setModel(args.model);
                const response = await gemini.chat(`[Navisworks context] ${args.message}`);
                return { content: [{ type: "text", text: response }] };
            } catch (e) { return { content: [{ type: "text", text: `AI error: ${e instanceof Error ? e.message : String(e)}` }] }; }
        }
    );

    server.tool(
        "navis_ai_analyze_clashes",
        "Fetch clash results from Navisworks then ask Gemini to triage them (group, prioritize, suggest owners).",
        {
            test: z.string().describe("Clash test name or GUID"),
            limit: z.number().optional().describe("Max clashes to fetch for analysis (default 50)"),
        },
        async (args) => {
            try {
                const raw = await withNavisConnection((c) =>
                    c.sendCommand("navis_get_clash_results", { test: args.test, limit: args.limit || 50, statusFilter: "" }));
                const response = await gemini.analyzeModel({ context: "navisworks-clash-triage", test: args.test, data: raw });
                return { content: [{ type: "text", text: response }] };
            } catch (e) { return { content: [{ type: "text", text: `AI error: ${e instanceof Error ? e.message : String(e)}` }] }; }
        }
    );

    server.tool(
        "navis_ai_generate_plugin_code",
        "Use AI to generate C# code for the Navisworks API from a natural language description.",
        { description: z.string().describe("Describe what the code should do in plain English") },
        async (args) => {
            try {
                const response = await gemini.generate(`Write Navisworks Manage C# (Autodesk.Navisworks.Api) code for: ${args.description}`);
                return { content: [{ type: "text", text: response }] };
            } catch (e) { return { content: [{ type: "text", text: `AI error: ${e instanceof Error ? e.message : String(e)}` }] }; }
        }
    );
}
