#pragma once

#include "CoreMinimal.h"
#include "Modules/ModuleManager.h"

class FMCPHttpServer;

class FUnrealMCPBridgeModule : public IModuleInterface
{
public:
    /** IModuleInterface implementation */
    virtual void StartupModule() override;
    virtual void ShutdownModule() override;

private:
    TSharedPtr<FMCPHttpServer> HttpServer;
};
