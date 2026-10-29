using UnrealBuildTool;

public class UnrealMCPBridge : ModuleRules
{
    public UnrealMCPBridge(ReadOnlyTargetRules Target) : base(Target)
    {
        PCHUsage = PCHUsageMode.UseExplicitOrSharedPCHs;

        PublicDependencyModuleNames.AddRange(new string[]
        {
            "Core",
            "CoreUObject",
            "Engine",
        });

        PrivateDependencyModuleNames.AddRange(new string[]
        {
            // Editor
            "UnrealEd",
            "EditorSubsystem",
            "Projects",

            // UI / notifications
            "Slate",
            "SlateCore",
            "EditorStyle",

            // HTTP server (built-in UE5 module — port 30011)
            "HTTPServer",

            // JSON serialization
            "Json",
            "JsonUtilities",

            // Screenshot / image
            "ImageWrapper",
            "RenderCore",
            "RHI",

            // Asset import
            "AssetTools",
            "AssetRegistry",

            // Viewport capture
            "LevelEditor",
            "ToolMenus",
        });
    }
}
