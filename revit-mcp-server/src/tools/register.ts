import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";

// Explicit imports for new tools
import { registerFederatedQueryTools } from "./federated_query_tools.js";
import { registerModelHealthTools } from "./model_health_tools.js";
import { registerIdsValidationTools } from "./ids_validation_tools.js";
import { registerClashBcfTools } from "./clash_bcf_tools.js";
import { registerModelDiffTools } from "./model_diff_tools.js";
import { registerFamilySanitizerTools } from "./family_sanitizer_tools.js";
import { registerCobieHandoverTools } from "./cobie_handover_tools.js";

export async function registerTools(server: McpServer) {
    const __filename = fileURLToPath(import.meta.url);
    const __dirname = path.dirname(__filename);

    const files = fs.readdirSync(__dirname);
    const newToolFiles = [
        "federated_query_tools",
        "model_health_tools",
        "ids_validation_tools",
        "clash_bcf_tools",
        "model_diff_tools",
        "family_sanitizer_tools",
        "cobie_handover_tools"
    ];
    
    const toolFiles = files.filter(
        (file) =>
            (file.endsWith(".ts") || file.endsWith(".js")) &&
            !file.endsWith(".d.ts") &&
            !file.endsWith(".d.js") &&
            file !== "register.ts" &&
            file !== "register.js" &&
            !newToolFiles.some(nf => file.includes(nf))
    );

    let registered = 0;
    for (const file of toolFiles) {
        try {
            const importPath = `./${file.replace(/\.(ts|js)$/, ".js")}`;
            const module = await import(importPath);

            const registerFn = Object.keys(module).find(
                (key) => key.startsWith("register") && typeof module[key] === "function"
            );

            if (registerFn) {
                module[registerFn](server);
                registered++;
                console.error(`  ✓ Registered tool: ${file.replace(/\.(ts|js)$/, "")}`);
            }
        } catch (error) {
            console.error(`  ✗ Failed to register ${file}:`, error);
        }
    }

    // Explicitly register new tools
    registerFederatedQueryTools(server);
    console.error(`  ✓ Registered tool: federated_query_tools`);
    registerModelHealthTools(server);
    console.error(`  ✓ Registered tool: model_health_tools`);
    registerIdsValidationTools(server);
    console.error(`  ✓ Registered tool: ids_validation_tools`);
    registerClashBcfTools(server);
    console.error(`  ✓ Registered tool: clash_bcf_tools`);
    registerModelDiffTools(server);
    console.error(`  ✓ Registered tool: model_diff_tools`);
    registerFamilySanitizerTools(server);
    console.error(`  ✓ Registered tool: family_sanitizer_tools`);
    registerCobieHandoverTools(server);
    console.error(`  ✓ Registered tool: cobie_handover_tools`);
    
    registered += 7;

    console.error(`Total tools registered: ${registered}`);
}
