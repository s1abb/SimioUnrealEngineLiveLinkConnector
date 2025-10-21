#pragma once

#include "CoreMinimal.h"

DECLARE_LOG_CATEGORY_EXTERN(LogUnrealLiveLinkNative, Log, All);

// Verbose logging control
// Set to 0 to disable verbose logs in production builds
#ifndef ULL_ENABLE_VERBOSE_LOGGING
    #define ULL_ENABLE_VERBOSE_LOGGING 1
#endif

#if ULL_ENABLE_VERBOSE_LOGGING
    #define ULL_VERBOSE_LOG(Format, ...) UE_LOG(LogUnrealLiveLinkNative, Verbose, Format, ##__VA_ARGS__)
#else
    #define ULL_VERBOSE_LOG(Format, ...)
#endif