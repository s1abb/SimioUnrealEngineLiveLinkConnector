using UnrealBuildTool;
using System.Collections.Generic;

[SupportedPlatforms(UnrealPlatformClass.Desktop)]
public class UnrealLiveLinkNativeTarget : TargetRules
{
    public UnrealLiveLinkNativeTarget(TargetInfo Target) : base(Target)
    {
        Type = TargetType.Program;
        bShouldCompileAsDLL = true;  // Output as DLL instead of EXE for P/Invoke
        LinkType = TargetLinkType.Monolithic;
        LaunchModuleName = "UnrealLiveLinkNative";
        
        // Minimal program configuration based on UnrealLiveLinkCInterface reference
        bBuildDeveloperTools = false;
        bBuildWithEditorOnlyData = true;
        bCompileAgainstEngine = false;
        bCompileAgainstCoreUObject = true;
        bCompileWithPluginSupport = false;
        bIncludePluginsForTargetPlatforms = false;
        bCompileICU = false;
        
        // Enable logging for diagnostics
        bUseLoggingInShipping = true;
        
        GlobalDefinitions.Add("UE_TRACE_ENABLED=1");
        
        // Use Latest include order (from reference)
        IncludeOrderVersion = EngineIncludeOrderVersion.Latest;
    }
}