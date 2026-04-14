import { GoogleGenAI } from "@google/genai";
import { GoogleAuth } from "../auth/google-oauth.js";

interface GeminiConfig {
    apiKey?: string;
    model?: string;
    systemInstruction?: string;
}

interface ChatMessage {
    role: "user" | "model";
    parts: { text: string }[];
}

/**
 * Google Gemini API integration for the BIM-Bot server.
 * Supports both API key auth and OAuth token auth.
 * Provides chat, single-turn, and tool-augmented generation.
 */
export class GeminiService {
    private client: GoogleGenAI | null = null;
    private config: GeminiConfig;
    private auth: GoogleAuth;
    private chatHistory: ChatMessage[] = [];

    constructor(config?: Partial<GeminiConfig>) {
        this.config = {
            apiKey: config?.apiKey || process.env.GOOGLE_API_KEY || process.env.GEMINI_API_KEY || "",
            model: config?.model || "gemini-2.5-flash",
            systemInstruction: config?.systemInstruction || this.getDefaultSystemInstruction(),
        };
        this.auth = new GoogleAuth();
    }

    /**
     * Initialize the Gemini client.
     * Prefers API key, falls back to OAuth access token.
     */
    async initialize(): Promise<void> {
        if (this.config.apiKey) {
            this.client = new GoogleGenAI({ apiKey: this.config.apiKey });
        } else if (this.auth.isAuthenticated) {
            const token = await this.auth.getAccessToken();
            this.client = new GoogleGenAI({ apiKey: token });
        } else {
            throw new Error(
                "No Gemini API key or OAuth token available. " +
                "Set GOOGLE_API_KEY env var or sign in with Google OAuth."
            );
        }
    }

    /**
     * Generate a single response from Gemini.
     */
    async generate(prompt: string): Promise<string> {
        await this.ensureClient();

        const response = await this.client!.models.generateContent({
            model: this.config.model!,
            contents: prompt,
            config: {
                systemInstruction: this.config.systemInstruction,
            },
        });

        return response.text ?? "";
    }

    /**
     * Chat with Gemini, maintaining conversation history.
     */
    async chat(userMessage: string): Promise<string> {
        await this.ensureClient();

        // Add user message to a copy first — only persist after success
        const pendingHistory = [
            ...this.chatHistory,
            { role: "user" as const, parts: [{ text: userMessage }] },
        ];

        const response = await this.client!.models.generateContent({
            model: this.config.model!,
            contents: pendingHistory.map(msg => ({
                role: msg.role,
                parts: msg.parts,
            })),
            config: {
                systemInstruction: this.config.systemInstruction,
            },
        });

        const text = response.text ?? "";

        // Only update history on success
        this.chatHistory = pendingHistory;
        this.chatHistory.push({
            role: "model",
            parts: [{ text }],
        });

        // Keep history bounded (max 50 turns = 100 messages)
        if (this.chatHistory.length > 100) {
            this.chatHistory = this.chatHistory.slice(-100);
        }

        return text;
    }

    /**
     * Generate with Revit context — includes model data in the prompt.
     */
    async generateWithContext(
        prompt: string,
        revitContext: Record<string, unknown>
    ): Promise<string> {
        const contextStr = JSON.stringify(revitContext, null, 2);
        const fullPrompt =
            `Here is the current Revit model context:\n\`\`\`json\n${contextStr}\n\`\`\`\n\n` +
            `User request: ${prompt}`;

        return this.generate(fullPrompt);
    }

    /**
     * Analyze Revit data and return structured recommendations.
     */
    async analyzeModel(modelData: Record<string, unknown>): Promise<string> {
        const prompt =
            `Analyze the following Revit model data and provide:\n` +
            `1. Summary of model contents\n` +
            `2. Potential issues or warnings\n` +
            `3. Optimization recommendations\n` +
            `4. Quality score (1-10)\n\n` +
            `Model data:\n\`\`\`json\n${JSON.stringify(modelData, null, 2)}\n\`\`\``;

        return this.generate(prompt);
    }

    /**
     * Generate Revit automation code from natural language.
     */
    async generateRevitCode(description: string): Promise<string> {
        const prompt =
            `Generate C# code for the Revit API to accomplish the following:\n\n` +
            `${description}\n\n` +
            `Requirements:\n` +
            `- Use Autodesk.Revit.DB namespace\n` +
            `- Wrap modifications in Transaction\n` +
            `- Handle errors gracefully\n` +
            `- Return results as a string\n` +
            `- Assume variables: UIApplication uiApp, Document doc`;

        return this.generate(prompt);
    }

    /**
     * Clear conversation history.
     */
    clearHistory(): void {
        this.chatHistory = [];
    }

    /**
     * Switch the model (e.g. between flash and pro).
     */
    setModel(model: string): void {
        this.config.model = model;
    }

    private async ensureClient(): Promise<void> {
        if (!this.client) {
            await this.initialize();
        }
    }

    private getDefaultSystemInstruction(): string {
        return `You are an expert Revit BIM automation assistant integrated into the BIM-Bot (Model Context Protocol) system.

You have access to 55 tools for interacting with Autodesk Revit through the MCP protocol:

**Reading (15 tools)**: Query views, elements, parameters, rooms, levels, grids, sheets, families, schedules, linked models, warnings, and project info.

**Creating (15 tools)**: Create walls, floors, ceilings, roofs, levels, grids, rooms, sheets, views, schedules, tags, dimensions, text notes, and place elements.

**Editing (12 tools)**: Modify parameters, move, rotate, copy, delete, mirror, align, group elements, change types, set worksets, color elements, and batch modify.

**Documentation (8 tools)**: Place views on sheets, create viewports, export schedules/DWG, print sheets, create legends, add revisions, tag all in view.

**QA/QC (8 tools)**: Check warnings, audit model, room compliance, naming conventions, find duplicates, purge unused, check links, validate parameters.

**Advanced (5 tools)**: Execute C# code in Revit, AI element filter, reset view, select elements, get model statistics.

When the user asks you to do something:
1. Determine which tool(s) are needed
2. Call the appropriate tool(s) in sequence
3. Report the results clearly
4. Suggest follow-up actions if relevant

Always be precise with coordinates (in feet), element IDs, and parameter names. When creating geometry, confirm the level and location with the user if ambiguous.`;
    }
}

// Singleton instance
let geminiInstance: GeminiService | null = null;

export function getGeminiService(config?: Partial<GeminiConfig>): GeminiService {
    if (!geminiInstance) {
        geminiInstance = new GeminiService(config);
    }
    return geminiInstance;
}
