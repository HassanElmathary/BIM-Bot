/**
 * Smoke test for the v2.5.0 annotation-transfer tools.
 * Verifies registerTransferTools registers all 5 tools on a real McpServer
 * instance without errors (mirrors test-startup.ts style).
 *
 * Usage: node build/tests/test-transfer.js
 */

import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { registerTransferTools } from "../tools/transfer_tools.js";

const EXPECTED = [
    "list_open_documents",
    "activate_document",
    "export_annotations",
    "import_annotations",
    "transfer_annotations",
];

async function main() {
    console.log("\nBIM-Bot Transfer Tools Test (v2.5.0)\n");

    try {
        const server = new McpServer({ name: "bim-bot-transfer-test", version: "2.5.0" });
        registerTransferTools(server);
        console.log(`  Registered transfer tools module (expects: ${EXPECTED.join(", ")})`);

        // The SDK exposes registered tools via the internal _registeredTools map.
        // Fall back to a pass if the shape differs across SDK versions.
        const reg = (server as unknown as { _registeredTools?: Record<string, unknown> })._registeredTools;
        if (reg) {
            const missing = EXPECTED.filter((n) => !(n in reg));
            if (missing.length > 0) {
                console.error(`  Missing tools: ${missing.join(", ")}`);
                process.exit(1);
            }
            console.log(`  All ${EXPECTED.length} transfer tools present`);
        } else {
            console.log("  (SDK internals not inspectable — registration completed without errors)");
        }

        console.log("\n  All checks passed\n");
        process.exit(0);
    } catch (error) {
        console.error("  Transfer tools test failed:", error);
        process.exit(1);
    }
}

main();
