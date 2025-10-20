using UnrealBuildTool;

public class UnrealLiveLinkNative : ModuleRules
{
    public UnrealLiveLinkNative(ReadOnlyTargetRules Target) : base(Target)
    {
        PCHUsage = PCHUsageMode.UseExplicitOrSharedPCHs;
        
        // Disable unity builds for cleaner compilation
        bUseUnity = false;
        
        // Basic dependencies (Sub-Phase 6.1)
        PublicDependencyModuleNames.AddRange(new string[] 
        {
            "Core",
            "CoreUObject"
        });
        
        // LiveLink dependencies (Sub-Phase 6.6)
        PublicDependencyModuleNames.AddRange(new string[] 
        {
            "LiveLinkInterface",            // LiveLink type definitions
            "LiveLinkMessageBusFramework",  // Message Bus source
            "Messaging",                    // Message Bus communication
        });
        
        // Export symbols for DLL
        PublicDefinitions.Add("ULL_API=__declspec(dllexport)");
        
        // Optimize for shipping
        OptimizeCode = CodeOptimization.InShippingBuildsOnly;
    }
}