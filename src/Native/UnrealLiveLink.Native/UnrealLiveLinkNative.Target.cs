using UnrealBuildTool;
using System.Collections.Generic;

[SupportedPlatforms(UnrealPlatformClass.Desktop)]
public class UnrealLiveLinkNativeTarget : TargetRules
{
    public UnrealLiveLinkNativeTarget(TargetInfo Target) : base(Target)
    {
        Type = TargetType.Program;
        LinkType = TargetLinkType.Monolithic;
        LaunchModuleName = "UnrealLiveLinkNative";
        
        // Minimal program configuration (NOT a plugin)
        bBuildDeveloperTools = false;
        bCompileAgainstEngine = false;
        bCompileAgainstCoreUObject = true;
        bCompileWithPluginSupport = false;
        bIncludePluginsForTargetPlatforms = false;
        bBuildWithEditorOnlyData = false;
        
        // Enable logging for diagnostics
        bUseLoggingInShipping = true;
        
        GlobalDefinitions.Add("UE_TRACE_ENABLED=1");
    }
}