using UnrealBuildTool;

public class UnrealLiveLinkNative : ModuleRules
{
    public UnrealLiveLinkNative(ReadOnlyTargetRules Target) : base(Target)
    {
        PCHUsage = PCHUsageMode.UseExplicitOrSharedPCHs;
        
        // Disable unity builds for cleaner compilation
        bUseUnity = false;
        
        // Use PRIVATE dependencies (not exposed in public API)
        // Based on UnrealLiveLinkCInterface reference implementation
        PrivateDependencyModuleNames.AddRange(new string[] 
        {
            "Core",
            "CoreUObject",
            "ApplicationCore",              // Required for minimal runtime
            "LiveLinkInterface",            // LiveLink type definitions
            "LiveLinkMessageBusFramework",  // Message Bus framework
            "UdpMessaging",                 // Network transport
        });
        
        // Export symbols for DLL
        PublicDefinitions.Add("ULL_API=__declspec(dllexport)");
        
        // Optimize for shipping
        OptimizeCode = CodeOptimization.InShippingBuildsOnly;
    }
}