/**
 * Ollama AI Service — Local LLM integration for BIM data mapping.
 * Connects to a locally-running Ollama instance (default: http://localhost:11434).
 * Uses Qwen 2.5 7b (Q4_K_M quantization) for low-resource operation.
 */

interface OllamaConfig {
    baseUrl: string;
    model: string;
    systemPrompt: string;
}

interface OllamaChatMessage {
    role: "system" | "user" | "assistant";
    content: string;
}

interface OllamaGenerateResponse {
    model: string;
    response: string;
    done: boolean;
    total_duration?: number;
    eval_count?: number;
}

interface OllamaChatResponse {
    model: string;
    message: OllamaChatMessage;
    done: boolean;
    total_duration?: number;
    eval_count?: number;
}

export class OllamaService {
    private config: OllamaConfig;
    private chatHistory: OllamaChatMessage[] = [];

    constructor(config?: Partial<OllamaConfig>) {
        this.config = {
            baseUrl: config?.baseUrl || process.env.OLLAMA_URL || "http://localhost:11434",
            model: config?.model || process.env.OLLAMA_MODEL || "qwen2.5:7b-instruct-q4_K_M",
            systemPrompt: config?.systemPrompt || this.getDefaultSystemPrompt(),
        };
    }

    /**
     * Check if Ollama is reachable.
     */
    async isAvailable(): Promise<boolean> {
        try {
            const res = await fetch(`${this.config.baseUrl}/api/tags`, {
                method: "GET",
                signal: AbortSignal.timeout(3000),
            });
            return res.ok;
        } catch {
            return false;
        }
    }

    /**
     * Generate a single response (no history).
     */
    async generate(prompt: string): Promise<string> {
        const body = {
            model: this.config.model,
            prompt: prompt,
            system: this.config.systemPrompt,
            stream: false,
        };

        const res = await fetch(`${this.config.baseUrl}/api/generate`, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(body),
            signal: AbortSignal.timeout(120000), // 2 min for local LLM
        });

        if (!res.ok) {
            const errText = await res.text();
            throw new Error(`Ollama error (${res.status}): ${errText}`);
        }

        const data = (await res.json()) as OllamaGenerateResponse;
        return data.response;
    }

    /**
     * Chat with history (multi-turn conversation).
     */
    async chat(userMessage: string): Promise<string> {
        // Build messages array
        const messages: OllamaChatMessage[] = [
            { role: "system", content: this.config.systemPrompt },
            ...this.chatHistory,
            { role: "user", content: userMessage },
        ];

        const body = {
            model: this.config.model,
            messages,
            stream: false,
        };

        const res = await fetch(`${this.config.baseUrl}/api/chat`, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(body),
            signal: AbortSignal.timeout(120000),
        });

        if (!res.ok) {
            const errText = await res.text();
            throw new Error(`Ollama chat error (${res.status}): ${errText}`);
        }

        const data = (await res.json()) as OllamaChatResponse;
        const assistantMessage = data.message.content;

        // Persist history on success
        this.chatHistory.push(
            { role: "user", content: userMessage },
            { role: "assistant", content: assistantMessage }
        );

        // Keep bounded (max 50 turns = 100 messages)
        if (this.chatHistory.length > 100) {
            this.chatHistory = this.chatHistory.slice(-100);
        }

        return assistantMessage;
    }

    /**
     * Map Revit JSON fields to external target headers using AI.
     * This is the core "BIM Data Architect" function.
     */
    async mapFields(
        revitFields: string[],
        targetHeaders: string[],
        sampleData?: Record<string, unknown>[]
    ): Promise<string> {
        let prompt =
            `You are mapping Revit BIM data fields to external database headers.\n\n` +
            `Revit fields available:\n${revitFields.map((f) => `  - ${f}`).join("\n")}\n\n` +
            `Target headers to map to:\n${targetHeaders.map((h) => `  - ${h}`).join("\n")}\n\n`;

        if (sampleData && sampleData.length > 0) {
            const sample = sampleData.slice(0, 3);
            prompt += `Sample data (first ${sample.length} elements):\n\`\`\`json\n${JSON.stringify(sample, null, 2)}\n\`\`\`\n\n`;
        }

        prompt +=
            `IMPORTANT mapping rules:\n` +
            `- "Mark" in Revit = element ID tag used on construction drawings\n` +
            `- "Level" = building floor/story the element belongs to\n` +
            `- "Area" = surface area in square feet (Revit internal units)\n` +
            `- "TypeName" = the family type name (e.g. "Basic Wall : Generic - 200mm")\n` +
            `- GUID = globally unique identifier, stable across model versions\n\n` +
            `Return a JSON object mapping each target header to the best matching Revit field.\n` +
            `Format: { "Target Header": "revit_field_name", ... }\n` +
            `If no match exists, map to null. Only return the JSON, no explanation.`;

        return this.generate(prompt);
    }

    /**
     * Analyze Revit element data and produce BIM-specific insights.
     * Supports multiple analysis types: summary, anomalies, cost_estimate, compliance, optimization.
     */
    async analyzeData(
        data: Record<string, unknown>[],
        category: string,
        analysisType: string = "summary"
    ): Promise<string> {
        const analysisPrompts: Record<string, string> = {
            summary:
                `Provide a concise summary of these ${category} elements:\n` +
                `- Total count and breakdown by type\n` +
                `- Distribution across levels/floors\n` +
                `- Key statistics (area ranges, common marks)\n` +
                `- Any notable patterns`,

            anomalies:
                `Analyze these ${category} elements for anomalies:\n` +
                `- Elements with missing or unusual data (empty marks, zero areas)\n` +
                `- Duplicate names or IDs\n` +
                `- Elements on unexpected levels\n` +
                `- Outlier values compared to the norm\n` +
                `Flag each issue with severity (LOW/MEDIUM/HIGH).`,

            cost_estimate:
                `Based on these ${category} elements, estimate construction costs:\n` +
                `- Group by type and calculate quantities\n` +
                `- Apply rough unit cost estimates (use typical Middle East construction rates)\n` +
                `- Provide a summary table with line items\n` +
                `- Note any cost optimization opportunities\n` +
                `Disclaimer: These are rough estimates for planning purposes only.`,

            compliance:
                `Check these ${category} elements for common design compliance issues:\n` +
                `- Missing required parameters (Mark, Level)\n` +
                `- Naming convention consistency\n` +
                `- Elements not placed on any level\n` +
                `- Potential accessibility compliance issues\n` +
                `Rate overall compliance as a percentage.`,

            optimization:
                `Suggest optimizations for these ${category} elements:\n` +
                `- Opportunities to reduce type variation\n` +
                `- Elements that could be consolidated\n` +
                `- Layout efficiency suggestions\n` +
                `- Material usage optimization ideas`,
        };

        const analysisInstruction = analysisPrompts[analysisType] || analysisPrompts.summary;

        const prompt =
            `You are analyzing Revit BIM model data for a construction project.\n\n` +
            `Category: ${category}\n` +
            `Element count: ${data.length}\n\n` +
            `Data:\n\`\`\`json\n${JSON.stringify(data, null, 2)}\n\`\`\`\n\n` +
            `${analysisInstruction}\n\n` +
            `Be specific, reference actual element names/IDs from the data. Keep response under 500 words.`;

        return this.generate(prompt);
    }

    /**
     * Clear conversation history.
     */
    clearHistory(): void {
        this.chatHistory = [];
    }

    /**
     * Switch to a different model.
     */
    setModel(model: string): void {
        this.config.model = model;
    }

    private getDefaultSystemPrompt(): string {
        return (
            `Act as a BIM Data Architect specializing in Autodesk Revit and construction data management. ` +
            `Your job is to help users extract, map, analyze, and sync BIM data to external platforms ` +
            `(Excel, Google Sheets, Notion). Keep responses concise and actionable.\n\n` +
            `You understand Revit element categories (Walls, Doors, Rooms, Floors, Columns, etc.) ` +
            `and their standard parameters (GUID, Name, Level, Mark, Area, TypeName, Width, Height, etc.).\n\n` +
            `When mapping fields:\n` +
            `1. Match by semantic meaning, not just name similarity\n` +
            `2. Prefer exact matches over fuzzy matches\n` +
            `3. Flag any data type mismatches (e.g. numeric vs string)\n` +
            `4. Suggest default values for unmapped required fields\n\n` +
            `When analyzing data:\n` +
            `1. Reference specific element IDs and names\n` +
            `2. Use construction industry terminology\n` +
            `3. Highlight actionable insights over observations\n` +
            `4. Consider Middle East construction standards where relevant`
        );
    }
}

// Singleton
let ollamaInstance: OllamaService | null = null;

export function getOllamaService(config?: Partial<OllamaConfig>): OllamaService {
    if (!ollamaInstance) {
        ollamaInstance = new OllamaService(config);
    }
    return ollamaInstance;
}
