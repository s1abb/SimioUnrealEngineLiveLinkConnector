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
        
        // Minimal program configuration - enable engine for LiveLink
        bBuildDeveloperTools = false;
        bCompileAgainstEngine = true;        // CHANGED: Need Engine for LiveLink modules
        bCompileAgainstCoreUObject = true;
        bCompileWithPluginSupport = true;     // CHANGED: LiveLink is in a plugin
        bIncludePluginsForTargetPlatforms = true;  // CHANGED: Need to load LiveLink plugin
        bBuildWithEditorOnlyData = false;
        
        // Enable logging for diagnostics
        bUseLoggingInShipping = true;
        
        GlobalDefinitions.Add("UE_TRACE_ENABLED=1");
        
        // Explicitly enable the LiveLink plugin
        ExtraModuleNames.AddRange(new string[] { "LiveLink", "LiveLinkInterface", "LiveLinkMessageBusFramework" });
    }
}