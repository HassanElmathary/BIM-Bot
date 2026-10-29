#include "MCPHttpServer.h"

// HTTP server
#include "HttpServerModule.h"
#include "HttpPath.h"
#include "HttpServerRequest.h"
#include "HttpServerResponse.h"

// JSON
#include "Dom/JsonObject.h"
#include "Dom/JsonValue.h"
#include "Serialization/JsonReader.h"
#include "Serialization/JsonSerializer.h"
#include "Serialization/JsonWriter.h"

// Editor / Engine
#include "Editor.h"
#include "Engine/Engine.h"
#include "Engine/World.h"
#include "Engine/GameViewportClient.h"
#include "EngineUtils.h"
#include "LevelEditorViewport.h"
#include "UnrealEdMisc.h"

// Screenshot
#include "HighResScreenshot.h"
#include "Misc/FileHelper.h"
#include "Misc/Paths.h"
#include "Misc/App.h"
#include "IImageWrapper.h"
#include "IImageWrapperModule.h"
#include "Modules/ModuleManager.h"

// Notifications
#include "Framework/Notifications/NotificationManager.h"
#include "Widgets/Notifications/SNotificationList.h"

// Projects
#include "Interfaces/IProjectManager.h"
#include "ProjectDescriptor.h"

// Asset import
#include "AssetToolsModule.h"
#include "AssetImportTask.h"

// Threading
#include "Async/Async.h"

// ─────────────────────────────────────────────────────────────────────────────
// Construction / destruction
// ─────────────────────────────────────────────────────────────────────────────

FMCPHttpServer::FMCPHttpServer()
{
}

FMCPHttpServer::~FMCPHttpServer()
{
    Stop();
}

// ─────────────────────────────────────────────────────────────────────────────
// Start / Stop
// ─────────────────────────────────────────────────────────────────────────────

void FMCPHttpServer::Start(int32 Port)
{
    BoundPort = Port;

    FHttpServerModule& HttpServerModule =
        FModuleManager::Get().LoadModuleChecked<FHttpServerModule>("HTTPServer");

    Router = HttpServerModule.GetHttpRouter(Port);
    if (!Router.IsValid())
    {
        UE_LOG(LogTemp, Error, TEXT("[UnrealMCPBridge] Failed to get HTTP router for port %d"), Port);
        return;
    }

    // Register routes
    Router->BindRoute(FHttpPath(TEXT("/mcp/info")),
        EHttpServerRequestVerbs::VERB_GET,
        FHttpRequestHandler::CreateRaw(this, &FMCPHttpServer::HandleInfo));

    Router->BindRoute(FHttpPath(TEXT("/mcp/screenshot")),
        EHttpServerRequestVerbs::VERB_POST,
        FHttpRequestHandler::CreateRaw(this, &FMCPHttpServer::HandleScreenshot));

    Router->BindRoute(FHttpPath(TEXT("/mcp/console")),
        EHttpServerRequestVerbs::VERB_POST,
        FHttpRequestHandler::CreateRaw(this, &FMCPHttpServer::HandleConsole));

    Router->BindRoute(FHttpPath(TEXT("/mcp/notify")),
        EHttpServerRequestVerbs::VERB_POST,
        FHttpRequestHandler::CreateRaw(this, &FMCPHttpServer::HandleNotify));

    Router->BindRoute(FHttpPath(TEXT("/mcp/project")),
        EHttpServerRequestVerbs::VERB_GET,
        FHttpRequestHandler::CreateRaw(this, &FMCPHttpServer::HandleProject));

    Router->BindRoute(FHttpPath(TEXT("/mcp/import")),
        EHttpServerRequestVerbs::VERB_POST,
        FHttpRequestHandler::CreateRaw(this, &FMCPHttpServer::HandleImport));

    HttpServerModule.StartAllListeners();

    UE_LOG(LogTemp, Log, TEXT("[UnrealMCPBridge] Listening on http://localhost:%d"), Port);
}

void FMCPHttpServer::Stop()
{
    if (Router.IsValid())
    {
        FHttpServerModule* HttpServerModule =
            FModuleManager::GetModulePtr<FHttpServerModule>("HTTPServer");
        if (HttpServerModule)
        {
            HttpServerModule->StopAllListeners();
        }
        Router.Reset();
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// GET /mcp/info
// ─────────────────────────────────────────────────────────────────────────────

bool FMCPHttpServer::HandleInfo(const FHttpServerRequest& Request, const FHttpResultCallback& OnComplete)
{
    TSharedRef<FJsonObject> Json = MakeShared<FJsonObject>();
    Json->SetStringField(TEXT("plugin"), TEXT("UnrealMCPBridge"));
    Json->SetStringField(TEXT("version"), TEXT("1.0.0"));
    Json->SetNumberField(TEXT("port"), BoundPort);
    Json->SetStringField(TEXT("engine_version"), FString(ENGINE_VERSION_STRING));
    Json->SetBoolField(TEXT("is_editor"), GIsEditor);

    OnComplete(JsonOk(Json));
    return true;
}

// ─────────────────────────────────────────────────────────────────────────────
// POST /mcp/screenshot  { "width": 1920, "height": 1080, "format": "png" }
// ─────────────────────────────────────────────────────────────────────────────

bool FMCPHttpServer::HandleScreenshot(const FHttpServerRequest& Request, const FHttpResultCallback& OnComplete)
{
    TSharedPtr<FJsonObject> Body;
    if (!ParseJsonBody(Request, Body))
    {
        OnComplete(JsonError(400, TEXT("Invalid JSON body")));
        return true;
    }

    int32 Width  = (int32)(Body->GetNumberField(TEXT("width")));
    int32 Height = (int32)(Body->GetNumberField(TEXT("height")));
    if (Width  <= 0) Width  = 1920;
    if (Height <= 0) Height = 1080;

    // Screenshot must happen on the game thread
    AsyncTask(ENamedThreads::GameThread, [Width, Height, OnComplete]()
    {
        // Build a unique temp path
        FString TempPath = FPaths::ProjectSavedDir() /
            FString::Printf(TEXT("MCP_Screenshot_%lld.png"), FDateTime::Now().GetTicks());

        // Request high-res screenshot
        FHighResScreenshotConfig& Config = GetHighResScreenshotConfig();
        Config.SetResolution(Width, Height, 1.0f);
        FScreenshotRequest::RequestScreenshot(TempPath, false, false);

        // Wait up to 5 s for the file to appear (screenshots are async in UE)
        double Deadline = FPlatformTime::Seconds() + 5.0;
        bool bFound = false;
        while (FPlatformTime::Seconds() < Deadline)
        {
            if (FPaths::FileExists(TempPath))
            {
                bFound = true;
                break;
            }
            FPlatformProcess::Sleep(0.1f);
        }

        if (!bFound)
        {
            TSharedRef<FJsonObject> ErrJson = MakeShared<FJsonObject>();
            ErrJson->SetStringField(TEXT("error"), TEXT("Screenshot timed out — viewport may not be active"));
            OnComplete(FHttpServerResponse::Create(503, TEXT("Service Unavailable"),
                { { TEXT("Content-Type"), TEXT("application/json") } },
                TArray<uint8>(TCHAR_TO_UTF8(*WriteJson(ErrJson)))));
            return;
        }

        // Read file and base64-encode
        TArray<uint8> RawData;
        if (!FFileHelper::LoadFileToArray(RawData, *TempPath))
        {
            OnComplete(FHttpServerResponse::Create(500, TEXT("Internal Server Error"),
                {}, {}));
            return;
        }
        IFileManager::Get().Delete(*TempPath); // clean up temp

        FString Base64 = FBase64::Encode(RawData);

        TSharedRef<FJsonObject> Json = MakeShared<FJsonObject>();
        Json->SetStringField(TEXT("image"), Base64);
        Json->SetStringField(TEXT("format"), TEXT("png"));
        Json->SetNumberField(TEXT("width"), Width);
        Json->SetNumberField(TEXT("height"), Height);

        // We can't call OnComplete from a lambda inside AsyncTask easily,
        // so we go back to any thread — the callback itself is thread-safe.
        FString JsonStr;
        TSharedRef<TJsonWriter<>> Writer = TJsonWriterFactory<>::Create(&JsonStr);
        FJsonSerializer::Serialize(Json, Writer);

        TArray<uint8> BodyBytes;
        FTCHARToUTF8 Converter(*JsonStr);
        BodyBytes.Append((const uint8*)Converter.Get(), Converter.Length());

        auto Response = FHttpServerResponse::Create(BodyBytes, TEXT("application/json"));
        OnComplete(MoveTemp(Response));
    });

    return true;
}

// ─────────────────────────────────────────────────────────────────────────────
// POST /mcp/console  { "command": "stat fps" }
// ─────────────────────────────────────────────────────────────────────────────

bool FMCPHttpServer::HandleConsole(const FHttpServerRequest& Request, const FHttpResultCallback& OnComplete)
{
    TSharedPtr<FJsonObject> Body;
    if (!ParseJsonBody(Request, Body))
    {
        OnComplete(JsonError(400, TEXT("Invalid JSON body")));
        return true;
    }

    FString Command = Body->GetStringField(TEXT("command"));
    if (Command.IsEmpty())
    {
        OnComplete(JsonError(400, TEXT("\"command\" field is required")));
        return true;
    }

    AsyncTask(ENamedThreads::GameThread, [Command, OnComplete]()
    {
        UWorld* World = GEditor ? GEditor->GetEditorWorldContext().World() : nullptr;
        if (GEngine)
        {
            GEngine->Exec(World, *Command);
        }

        TSharedRef<FJsonObject> Json = MakeShared<FJsonObject>();
        Json->SetBoolField(TEXT("success"), true);
        Json->SetStringField(TEXT("command"), Command);

        FString JsonStr;
        TSharedRef<TJsonWriter<>> Writer = TJsonWriterFactory<>::Create(&JsonStr);
        FJsonSerializer::Serialize(Json, Writer);

        TArray<uint8> BodyBytes;
        FTCHARToUTF8 Converter(*JsonStr);
        BodyBytes.Append((const uint8*)Converter.Get(), Converter.Length());

        OnComplete(FHttpServerResponse::Create(BodyBytes, TEXT("application/json")));
    });

    return true;
}

// ─────────────────────────────────────────────────────────────────────────────
// POST /mcp/notify  { "message": "...", "severity": "info", "duration": 5.0 }
// ─────────────────────────────────────────────────────────────────────────────

bool FMCPHttpServer::HandleNotify(const FHttpServerRequest& Request, const FHttpResultCallback& OnComplete)
{
    TSharedPtr<FJsonObject> Body;
    if (!ParseJsonBody(Request, Body))
    {
        OnComplete(JsonError(400, TEXT("Invalid JSON body")));
        return true;
    }

    FString Message  = Body->GetStringField(TEXT("message"));
    FString Severity = Body->HasField(TEXT("severity")) ? Body->GetStringField(TEXT("severity")) : TEXT("info");
    float Duration   = Body->HasField(TEXT("duration"))  ? (float)Body->GetNumberField(TEXT("duration")) : 5.0f;

    if (Message.IsEmpty())
    {
        OnComplete(JsonError(400, TEXT("\"message\" field is required")));
        return true;
    }

    AsyncTask(ENamedThreads::GameThread, [Message, Severity, Duration, OnComplete]()
    {
        FNotificationInfo Info(FText::FromString(Message));
        Info.ExpireDuration   = Duration;
        Info.bFireAndForget   = true;
        Info.bUseLargeFont    = false;
        Info.bUseSuccessFailIcons = true;

        TSharedPtr<SNotificationItem> Notification =
            FSlateNotificationManager::Get().AddNotification(Info);

        if (Notification.IsValid())
        {
            if (Severity == TEXT("error"))
                Notification->SetCompletionState(SNotificationItem::CS_Fail);
            else if (Severity == TEXT("warning"))
                Notification->SetCompletionState(SNotificationItem::CS_Pending);
            else
                Notification->SetCompletionState(SNotificationItem::CS_Success);
        }

        TSharedRef<FJsonObject> Json = MakeShared<FJsonObject>();
        Json->SetBoolField(TEXT("success"), true);
        Json->SetStringField(TEXT("message"), Message);
        Json->SetStringField(TEXT("severity"), Severity);

        FString JsonStr;
        TSharedRef<TJsonWriter<>> Writer = TJsonWriterFactory<>::Create(&JsonStr);
        FJsonSerializer::Serialize(Json, Writer);

        TArray<uint8> BodyBytes;
        FTCHARToUTF8 Converter(*JsonStr);
        BodyBytes.Append((const uint8*)Converter.Get(), Converter.Length());

        OnComplete(FHttpServerResponse::Create(BodyBytes, TEXT("application/json")));
    });

    return true;
}

// ─────────────────────────────────────────────────────────────────────────────
// GET /mcp/project
// ─────────────────────────────────────────────────────────────────────────────

bool FMCPHttpServer::HandleProject(const FHttpServerRequest& Request, const FHttpResultCallback& OnComplete)
{
    TSharedRef<FJsonObject> Json = MakeShared<FJsonObject>();

    // Engine version
    Json->SetStringField(TEXT("engine_version"), FString(ENGINE_VERSION_STRING));

    // Project name and path
    Json->SetStringField(TEXT("project_name"), FApp::GetProjectName());
    Json->SetStringField(TEXT("project_dir"), FPaths::ConvertRelativePathToFull(FPaths::ProjectDir()));
    Json->SetStringField(TEXT("content_dir"), FPaths::ConvertRelativePathToFull(FPaths::ProjectContentDir()));

    // Active level
    UWorld* World = GEditor ? GEditor->GetEditorWorldContext().World() : nullptr;
    if (World)
    {
        Json->SetStringField(TEXT("current_level"), World->GetName());
        Json->SetStringField(TEXT("current_level_path"), World->GetPathName());
    }

    // Enabled plugins
    TArray<TSharedRef<FJsonValue>> PluginList;
    IPluginManager& PluginManager = IPluginManager::Get();
    for (const TSharedRef<IPlugin>& Plugin : PluginManager.GetEnabledPlugins())
    {
        PluginList.Add(MakeShared<FJsonValueString>(Plugin->GetName()));
    }
    Json->SetArrayField(TEXT("enabled_plugins"), PluginList);

    OnComplete(JsonOk(Json));
    return true;
}

// ─────────────────────────────────────────────────────────────────────────────
// POST /mcp/import  { "filePath": "C:/...", "destinationPath": "/Game/Models" }
// ─────────────────────────────────────────────────────────────────────────────

bool FMCPHttpServer::HandleImport(const FHttpServerRequest& Request, const FHttpResultCallback& OnComplete)
{
    TSharedPtr<FJsonObject> Body;
    if (!ParseJsonBody(Request, Body))
    {
        OnComplete(JsonError(400, TEXT("Invalid JSON body")));
        return true;
    }

    FString FilePath        = Body->GetStringField(TEXT("filePath"));
    FString DestinationPath = Body->GetStringField(TEXT("destinationPath"));

    if (FilePath.IsEmpty() || DestinationPath.IsEmpty())
    {
        OnComplete(JsonError(400, TEXT("\"filePath\" and \"destinationPath\" are required")));
        return true;
    }

    if (!FPaths::FileExists(FilePath))
    {
        OnComplete(JsonError(404, FString::Printf(TEXT("File not found: %s"), *FilePath)));
        return true;
    }

    AsyncTask(ENamedThreads::GameThread, [FilePath, DestinationPath, OnComplete]()
    {
        IAssetTools& AssetTools =
            FModuleManager::LoadModuleChecked<FAssetToolsModule>("AssetTools").Get();

        UAssetImportTask* Task = NewObject<UAssetImportTask>();
        Task->Filename        = FilePath;
        Task->DestinationPath = DestinationPath;
        Task->bSave           = true;
        Task->bAutomated      = true;
        Task->bReplaceExisting = false;

        TArray<UAssetImportTask*> Tasks = { Task };
        AssetTools.ImportAssetTasks(Tasks);

        TArray<TSharedRef<FJsonValue>> ImportedPaths;
        for (const FString& ImportedPath : Task->GetImportedObjectPaths())
        {
            ImportedPaths.Add(MakeShared<FJsonValueString>(ImportedPath));
        }

        TSharedRef<FJsonObject> Json = MakeShared<FJsonObject>();
        Json->SetBoolField(TEXT("success"), ImportedPaths.Num() > 0);
        Json->SetStringField(TEXT("file"), FilePath);
        Json->SetStringField(TEXT("destination"), DestinationPath);
        Json->SetArrayField(TEXT("imported"), ImportedPaths);

        FString JsonStr;
        TSharedRef<TJsonWriter<>> Writer = TJsonWriterFactory<>::Create(&JsonStr);
        FJsonSerializer::Serialize(Json, Writer);

        TArray<uint8> BodyBytes;
        FTCHARToUTF8 Converter(*JsonStr);
        BodyBytes.Append((const uint8*)Converter.Get(), Converter.Length());

        OnComplete(FHttpServerResponse::Create(BodyBytes, TEXT("application/json")));
    });

    return true;
}

// ─────────────────────────────────────────────────────────────────────────────
// Helpers
// ─────────────────────────────────────────────────────────────────────────────

bool FMCPHttpServer::ParseJsonBody(const FHttpServerRequest& Request, TSharedPtr<FJsonObject>& OutJson)
{
    if (Request.Body.Num() == 0)
    {
        OutJson = MakeShared<FJsonObject>();
        return true;
    }

    FString BodyStr = FString(UTF8_TO_TCHAR(
        reinterpret_cast<const char*>(Request.Body.GetData())));

    TSharedRef<TJsonReader<>> Reader = TJsonReaderFactory<>::Create(BodyStr);
    return FJsonSerializer::Deserialize(Reader, OutJson) && OutJson.IsValid();
}

TUniquePtr<FHttpServerResponse> FMCPHttpServer::JsonOk(const TSharedRef<FJsonObject>& Body)
{
    FString JsonStr = JsonToString(Body);
    TArray<uint8> Bytes;
    FTCHARToUTF8 Converter(*JsonStr);
    Bytes.Append((const uint8*)Converter.Get(), Converter.Length());
    return FHttpServerResponse::Create(Bytes, TEXT("application/json"));
}

TUniquePtr<FHttpServerResponse> FMCPHttpServer::JsonError(int32 StatusCode, const FString& Message)
{
    TSharedRef<FJsonObject> Json = MakeShared<FJsonObject>();
    Json->SetStringField(TEXT("error"), Message);

    FString JsonStr = JsonToString(Json);
    TArray<uint8> Bytes;
    FTCHARToUTF8 Converter(*JsonStr);
    Bytes.Append((const uint8*)Converter.Get(), Converter.Length());

    TMap<FString, TArray<FString>> Headers;
    Headers.Add(TEXT("Content-Type"), { TEXT("application/json") });

    return FHttpServerResponse::Create(StatusCode, TEXT("Error"), Headers, Bytes);
}

FString FMCPHttpServer::JsonToString(const TSharedRef<FJsonObject>& Json)
{
    FString Output;
    TSharedRef<TJsonWriter<>> Writer = TJsonWriterFactory<>::Create(&Output);
    FJsonSerializer::Serialize(Json, Writer);
    return Output;
}
