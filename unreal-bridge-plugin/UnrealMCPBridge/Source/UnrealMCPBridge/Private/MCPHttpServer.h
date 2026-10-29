#pragma once

#include "CoreMinimal.h"
#include "IHttpRouter.h"
#include "HttpServerRequest.h"
#include "HttpResultCallback.h"
#include "HttpServerResponse.h"

/**
 * FMCPHttpServer
 *
 * Lightweight HTTP server on port 30011.
 * Provides bridge endpoints consumed by the Node.js MCP server:
 *
 *   GET  /mcp/info        — health check + plugin version
 *   POST /mcp/screenshot  — capture active viewport as base64 PNG
 *   POST /mcp/console     — execute a console command
 *   POST /mcp/notify      — show an editor notification toast
 *   GET  /mcp/project     — project metadata (engine version, name, plugins)
 *   POST /mcp/import      — import a file into the content browser
 */
class FMCPHttpServer
{
public:
    FMCPHttpServer();
    ~FMCPHttpServer();

    /** Start listening on the given port. Call from module StartupModule(). */
    void Start(int32 Port = 30011);

    /** Stop the server. Call from module ShutdownModule(). */
    void Stop();

private:
    // ── Route handlers ────────────────────────────────────────────────────────

    bool HandleInfo(const FHttpServerRequest& Request, const FHttpResultCallback& OnComplete);
    bool HandleScreenshot(const FHttpServerRequest& Request, const FHttpResultCallback& OnComplete);
    bool HandleConsole(const FHttpServerRequest& Request, const FHttpResultCallback& OnComplete);
    bool HandleNotify(const FHttpServerRequest& Request, const FHttpResultCallback& OnComplete);
    bool HandleProject(const FHttpServerRequest& Request, const FHttpResultCallback& OnComplete);
    bool HandleImport(const FHttpServerRequest& Request, const FHttpResultCallback& OnComplete);

    // ── Helpers ───────────────────────────────────────────────────────────────

    /** Parse the request body as a JSON object. Returns false if malformed. */
    bool ParseJsonBody(const FHttpServerRequest& Request, TSharedPtr<FJsonObject>& OutJson);

    /** Build a JSON success response. */
    TUniquePtr<FHttpServerResponse> JsonOk(const TSharedRef<FJsonObject>& Body);

    /** Build a JSON error response with the given HTTP status code. */
    TUniquePtr<FHttpServerResponse> JsonError(int32 StatusCode, const FString& Message);

    /** Serialize a FJsonObject to a compact UTF-8 string. */
    FString JsonToString(const TSharedRef<FJsonObject>& Json);

    // ── State ─────────────────────────────────────────────────────────────────

    TSharedPtr<IHttpRouter> Router;
    int32 BoundPort = 0;
};
