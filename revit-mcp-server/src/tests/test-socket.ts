/**
 * Integration test: MCP Server ↔ Revit Plugin socket communication.
 * Run this test with the Revit plugin's SocketService running (port 8080).
 *
 * Usage: node build/tests/test-socket.js
 */

import net from "net";

const HOST = "localhost";
const PORT = 8080;
const TIMEOUT = 5000;

interface TestResult {
    name: string;
    passed: boolean;
    message: string;
    duration: number;
}

const results: TestResult[] = [];

async function sendCommand(
    method: string,
    params: Record<string, unknown> = {}
): Promise<unknown> {
    return new Promise((resolve, reject) => {
        const socket = new net.Socket();
        let buffer = "";

        socket.setTimeout(TIMEOUT);

        socket.on("data", (data) => {
            buffer += data.toString();
            try {
                const response = JSON.parse(buffer);
                socket.destroy();
                if (response.error) {
                    reject(new Error(response.error.message));
                } else {
                    resolve(response.result);
                }
            } catch {
                // Incomplete JSON, wait for more data
            }
        });

        socket.on("timeout", () => {
            socket.destroy();
            reject(new Error(`Timeout after ${TIMEOUT}ms`));
        });

        socket.on("error", (err) => {
            reject(err);
        });

        socket.connect(PORT, HOST, () => {
            const command = {
                jsonrpc: "2.0",
                method,
                params,
                id: Date.now(),
            };
            socket.write(JSON.stringify(command));
        });
    });
}

async function runTest(
    name: string,
    fn: () => Promise<void>
): Promise<void> {
    const start = Date.now();
    try {
        await fn();
        results.push({
            name,
            passed: true,
            message: "OK",
            duration: Date.now() - start,
        });
        console.log(`  ✓ ${name} (${Date.now() - start}ms)`);
    } catch (error) {
        results.push({
            name,
            passed: false,
            message: error instanceof Error ? error.message : String(error),
            duration: Date.now() - start,
        });
        console.log(
            `  ✗ ${name}: ${error instanceof Error ? error.message : error}`
        );
    }
}

// --- Test Suite ---

async function main() {
    console.log("\n🧪 BIM-Bot Socket Integration Tests\n");

    // Test 1: Connection
    await runTest("Can connect to Revit plugin on port 8080", async () => {
        const socket = new net.Socket();
        await new Promise<void>((resolve, reject) => {
            socket.setTimeout(3000);
            socket.on("connect", () => {
                socket.destroy();
                resolve();
            });
            socket.on("error", reject);
            socket.on("timeout", () => reject(new Error("Connection timeout")));
            socket.connect(PORT, HOST);
        });
    });

    // Test 2: Get current view info
    await runTest("get_current_view_info returns data", async () => {
        const result = await sendCommand("get_current_view_info");
        if (!result) throw new Error("Empty result");
    });

    // Test 3: Get project info
    await runTest("get_project_info returns data", async () => {
        const result = await sendCommand("get_project_info");
        if (!result) throw new Error("Empty result");
    });

    // Test 4: Get levels
    await runTest("get_levels returns array", async () => {
        const result = await sendCommand("get_levels");
        if (!Array.isArray(result))
            throw new Error("Expected array, got " + typeof result);
    });

    // Test 5: Get views
    await runTest("get_views returns data", async () => {
        const result = await sendCommand("get_views");
        if (!result) throw new Error("Empty result");
    });

    // Test 6: Get warnings
    await runTest("get_warnings returns data", async () => {
        const result = await sendCommand("get_warnings");
        if (!result) throw new Error("Empty result");
    });

    // Test 7: Model statistics
    await runTest("get_model_statistics returns data", async () => {
        const result = await sendCommand("get_model_statistics");
        if (!result) throw new Error("Empty result");
    });

    // Test 8: Invalid command
    await runTest("Invalid command returns error", async () => {
        try {
            await sendCommand("nonexistent_command_xyz");
            throw new Error("Should have thrown");
        } catch (e) {
            if (
                e instanceof Error &&
                e.message.includes("Should have thrown")
            ) {
                throw e;
            }
            // Expected error — test passes
        }
    });

    // Summary
    const passed = results.filter((r) => r.passed).length;
    const failed = results.filter((r) => !r.passed).length;
    console.log(`\n${"─".repeat(50)}`);
    console.log(
        `Results: ${passed} passed, ${failed} failed, ${results.length} total`
    );
    if (failed > 0) {
        console.log("\nFailed tests:");
        results
            .filter((r) => !r.passed)
            .forEach((r) => console.log(`  ✗ ${r.name}: ${r.message}`));
    }
    console.log();
    process.exit(failed > 0 ? 1 : 0);
}

main().catch((err) => {
    console.error("Test runner error:", err);
    process.exit(1);
});
