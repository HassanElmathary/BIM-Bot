#include "UnrealMCPBridge.h"
#include "MCPHttpServer.h"
#include "Modules/ModuleManager.h"
#include "Misc/MessageDialog.h"

#define LOCTEXT_NAMESPACE "FUnrealMCPBridgeModule"

void FUnrealMCPBridgeModule::StartupModule()
{
    HttpServer = MakeShared<FMCPHttpServer>();
    HttpServer->Start(30011);

    UE_LOG(LogTemp, Log, TEXT("[UnrealMCPBridge] Started HTTP bridge on port 30011"));
}

void FUnrealMCPBridgeModule::ShutdownModule()
{
    if (HttpServer.IsValid())
    {
        HttpServer->Stop();
        HttpServer.Reset();
    }

    UE_LOG(LogTemp, Log, TEXT("[UnrealMCPBridge] Stopped HTTP bridge"));
}

#undef LOCTEXT_NAMESPACE

IMPLEMENT_MODULE(FUnrealMCPBridgeModule, UnrealMCPBridge)
