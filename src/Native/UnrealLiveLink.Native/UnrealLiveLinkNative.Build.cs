using UnrealBuildTool;

public class UnrealLiveLinkNative : ModuleRules
{
    public UnrealLiveLinkNative(ReadOnlyTargetRules Target) : base(Target)
    {
        PCHUsage = PCHUsageMode.UseExplicitOrSharedPCHs;
        
        // Basic dependencies (Sub-Phase 6.1)
        PublicDependencyModuleNames.AddRange(new string[] 
        {
            "Core",
            "CoreUObject"
        });
        
        // LiveLink dependencies (Sub-Phase 6.5)
        PublicDependencyModuleNames.AddRange(new string[] 
        {
            "LiveLinkInterface",            // LiveLink type definitions
            "LiveLinkMessageBusFramework",  // ILiveLinkProvider API
            "Messaging",                    // Message Bus communication
            "UdpMessaging"                  // Network transport for Message Bus
        });
        
        // Export symbols for DLL
        PublicDefinitions.Add("ULL_API=__declspec(dllexport)");
        
        // Optimize for shipping
        OptimizeCode = CodeOptimization.InShippingBuildsOnly;
    }
}