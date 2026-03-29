/**
 * Notion Client — syncs Revit element data to Notion databases.
 * Uses the official @notionhq/client SDK.
 */

import { Client } from "@notionhq/client";

let notionClient: Client | null = null;

function getNotionClient(): Client {
    if (!notionClient) {
        const apiKey = process.env.NOTION_API_KEY;
        if (!apiKey || apiKey === "your_notion_integration_token") {
            throw new Error(
                "Notion API key not configured. Set NOTION_API_KEY in your .env file.\n" +
                "Get one at: https://www.notion.so/my-integrations"
            );
        }
        notionClient = new Client({ auth: apiKey });
    }
    return notionClient;
}

/**
 * Map a value to a Notion property based on its type.
 */
function toNotionProperty(value: unknown): Record<string, unknown> {
    if (value === null || value === undefined) {
        return { rich_text: [{ text: { content: "" } }] };
    }
    if (typeof value === "number") {
        return { number: value };
    }
    if (typeof value === "boolean") {
        return { checkbox: value };
    }
    // Everything else → rich_text
    const str = typeof value === "object" ? JSON.stringify(value) : String(value);
    // Notion rich_text max is 2000 chars
    const truncated = str.length > 2000 ? str.substring(0, 1997) + "..." : str;
    return { rich_text: [{ text: { content: truncated } }] };
}

export interface NotionSyncOptions {
    /** Notion database ID to sync to */
    databaseId: string;
    /** Optional field mapping: { "notion_property": "revit_field" } */
    fieldMapping?: Record<string, string>;
    /** Max pages to create per batch (default: 100) */
    batchSize?: number;
}

export interface NotionSyncResult {
    created: number;
    failed: number;
    errors: string[];
}

/**
 * Sync Revit element data to a Notion database.
 * Creates one page per element. Uses batching to limit API load.
 */
export async function syncToNotion(
    data: Record<string, unknown>[],
    options: NotionSyncOptions
): Promise<NotionSyncResult> {
    if (!data || data.length === 0) {
        throw new Error("No data to sync");
    }

    const notion = getNotionClient();
    const batchSize = options.batchSize || 100;
    const result: NotionSyncResult = { created: 0, failed: 0, errors: [] };

    // Process in batches
    for (let i = 0; i < data.length; i += batchSize) {
        const batch = data.slice(i, i + batchSize);

        for (const item of batch) {
            try {
                const properties: Record<string, unknown> = {};

                if (options.fieldMapping) {
                    // Use explicit mapping
                    for (const [notionProp, revitField] of Object.entries(options.fieldMapping)) {
                        properties[notionProp] = toNotionProperty(item[revitField]);
                    }
                } else {
                    // Auto-map: use Revit field names as Notion property names
                    for (const [key, value] of Object.entries(item)) {
                        properties[key] = toNotionProperty(value);
                    }
                }

                // If there's a "Name" field, use it as the page title
                const nameField = item["Name"] || item["name"] || item["Mark"] || item["mark"];
                if (nameField) {
                    properties["Name"] = {
                        title: [{ text: { content: String(nameField) } }],
                    };
                }

                await notion.pages.create({
                    parent: { database_id: options.databaseId },
                    properties: properties as Record<string, unknown> as import("@notionhq/client/build/src/api-endpoints.js").CreatePageParameters["properties"],
                });

                result.created++;
            } catch (error) {
                result.failed++;
                result.errors.push(
                    `Element ${i}: ${error instanceof Error ? error.message : String(error)}`
                );
            }
        }

        // Small delay between batches to avoid rate limits
        if (i + batchSize < data.length) {
            await new Promise((resolve) => setTimeout(resolve, 500));
        }
    }

    return result;
}

/**
 * Query an existing Notion database to retrieve pages.
 * Uses direct API call since `databases.query` was removed in SDK v5.
 */
export async function queryNotionDatabase(
    databaseId: string,
    pageSize: number = 100
): Promise<unknown[]> {
    const apiKey = process.env.NOTION_API_KEY;
    if (!apiKey || apiKey === "your_notion_integration_token") {
        throw new Error(
            "Notion API key not configured. Set NOTION_API_KEY in your .env file.\n" +
            "Get one at: https://www.notion.so/my-integrations"
        );
    }

    const response = await fetch(`https://api.notion.com/v1/databases/${databaseId}/query`, {
        method: "POST",
        headers: {
            "Authorization": `Bearer ${apiKey}`,
            "Content-Type": "application/json",
            "Notion-Version": "2022-06-28",
        },
        body: JSON.stringify({ page_size: pageSize }),
    });

    if (!response.ok) {
        const errText = await response.text();
        throw new Error(`Notion API error (${response.status}): ${errText}`);
    }

    const data = (await response.json()) as { results: unknown[] };
    return data.results;
}
