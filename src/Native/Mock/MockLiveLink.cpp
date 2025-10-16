#include "MockLiveLink.h"
#include <iostream>
#include <string>
#include <unordered_set>
#include <unordered_map>
#include <vector>
#include <iomanip>
#include <sstream>
#include <fstream>
#include <ctime>
#include <windows.h>

//
// Mock State Management
//

static bool g_isInitialized = false;
static std::string g_providerName;
static std::unordered_set<std::string> g_transformObjects;
static std::unordered_map<std::string, std::vector<std::string>> g_transformObjectProperties;
static std::unordered_map<std::string, std::vector<std::string>> g_dataSubjectProperties;

//
// Logging Helpers
//

void LogCall(const std::string& functionName, const std::string& params = "") {
    // Get current timestamp
    auto now = std::time(nullptr);
    auto tm = *std::localtime(&now);
    char timestamp[20];
    std::strftime(timestamp, sizeof(timestamp), "%H:%M:%S", &tm);
    
    std::string message = "[" + std::string(timestamp) + "] [MOCK] " + functionName;
    if (!params.empty()) {
        message += "(" + params + ")";
    }
    
    // Write to log file in tests directory
    std::ofstream logFile("C:\\repos\\SimioUnrealEngineLiveLinkConnector\\tests\\Simio.Tests\\SimioUnrealLiveLink_Mock.log", std::ios::app);
    if (logFile.is_open()) {
        logFile << message << std::endl;
        logFile.close();
    }
    
    // Also output to console for standalone testing
    std::cout << message << std::endl;
}

void LogError(const std::string& functionName, const std::string& error) {
    // Get current timestamp
    auto now = std::time(nullptr);
    auto tm = *std::localtime(&now);
    char timestamp[20];
    std::strftime(timestamp, sizeof(timestamp), "%H:%M:%S", &tm);
    
    std::string message = "[" + std::string(timestamp) + "] [MOCK ERROR] " + functionName + ": " + error;
    
    // Write to log file
    std::ofstream logFile("C:\\repos\\SimioUnrealEngineLiveLinkConnector\\tests\\Simio.Tests\\SimioUnrealLiveLink_Mock.log", std::ios::app);
    if (logFile.is_open()) {
        logFile << message << std::endl;
        logFile.close();
    }
    
    // Also output to console
    std::cout << message << std::endl;
}

void ClearLogFile() {
    // Clear the log file at the start of each simulation run
    std::ofstream logFile("C:\\repos\\SimioUnrealEngineLiveLinkConnector\\tests\\Simio.Tests\\SimioUnrealLiveLink_Mock.log", std::ios::trunc);
    if (logFile.is_open()) {
        logFile.close();
    }
}

std::string FormatTransform(const ULL_Transform* transform) {
    if (!transform) return "NULL";
    
    std::ostringstream oss;
    oss << std::fixed << std::setprecision(1);
    oss << "pos=[" << transform->position[0] << "," << transform->position[1] << "," << transform->position[2] << "], ";
    oss << "rot=[" << transform->rotation[0] << "," << transform->rotation[1] << "," << transform->rotation[2] << "," << transform->rotation[3] << "], ";
    oss << "scale=[" << transform->scale[0] << "," << transform->scale[1] << "," << transform->scale[2] << "]";
    return oss.str();
}

std::string FormatPropertyArray(const float* values, int count) {
    if (!values || count <= 0) return "[]";
    
    std::ostringstream oss;
    oss << std::fixed << std::setprecision(2);
    oss << "[";
    for (int i = 0; i < count; ++i) {
        if (i > 0) oss << ", ";
        oss << values[i];
    }
    oss << "]";
    return oss.str();
}

std::string FormatStringArray(const char** strings, int count) {
    if (!strings || count <= 0) return "[]";
    
    std::ostringstream oss;
    oss << "[";
    for (int i = 0; i < count; ++i) {
        if (i > 0) oss << ", ";
        oss << "'" << (strings[i] ? strings[i] : "NULL") << "'";
    }
    oss << "]";
    return oss.str();
}

//
// API Implementation - MUST MATCH UnrealLiveLinkNative.cs EXACTLY
//

extern "C" {

int ULL_Initialize(const char* providerName) {
    if (!providerName) {
        LogError("ULL_Initialize", "providerName is NULL");
        return 1; // Error
    }
    
    if (g_isInitialized) {
        LogError("ULL_Initialize", "Already initialized with provider '" + g_providerName + "'");
        return 1; // Error
    }
    
    // Clear the log file at the start of each new simulation run
    ClearLogFile();
    
    g_providerName = providerName;
    g_isInitialized = true;
    g_transformObjects.clear();
    g_transformObjectProperties.clear();
    g_dataSubjectProperties.clear();
    
    LogCall("ULL_Initialize", "providerName='" + g_providerName + "'");
    return 0; // Success
}

void ULL_Shutdown() {
    LogCall("ULL_Shutdown");
    
    g_isInitialized = false;
    g_providerName.clear();
    g_transformObjects.clear();
    g_transformObjectProperties.clear();
    g_dataSubjectProperties.clear();
}

int ULL_GetVersion() {
    LogCall("ULL_GetVersion");
    return 1; // Mock version 1
}

int ULL_IsConnected() {
    if (!g_isInitialized) {
        LogCall("ULL_IsConnected", "result=NOT_INITIALIZED");
        return 2; // Not initialized
    }
    
    // Mock always reports connected after initialization
    LogCall("ULL_IsConnected", "result=CONNECTED");
    return 0; // Connected
}

//=============================================================================
// Transform Subjects (3D Objects)
//=============================================================================

void ULL_RegisterObject(const char* subjectName) {
    if (!subjectName) {
        LogError("ULL_RegisterObject", "subjectName is NULL");
        return;
    }
    
    if (!g_isInitialized) {
        LogError("ULL_RegisterObject", "Not initialized");
        return;
    }
    
    g_transformObjects.insert(subjectName);
    LogCall("ULL_RegisterObject", "subjectName='" + std::string(subjectName) + "'");
}

void ULL_RegisterObjectWithProperties(const char* subjectName, const char** propertyNames, int propertyCount) {
    if (!subjectName) {
        LogError("ULL_RegisterObjectWithProperties", "subjectName is NULL");
        return;
    }
    
    if (!g_isInitialized) {
        LogError("ULL_RegisterObjectWithProperties", "Not initialized");
        return;
    }
    
    if (propertyCount < 0) {
        LogError("ULL_RegisterObjectWithProperties", "propertyCount is negative");
        return;
    }
    
    // Store property names
    std::vector<std::string> properties;
    if (propertyNames && propertyCount > 0) {
        for (int i = 0; i < propertyCount; ++i) {
            properties.push_back(propertyNames[i] ? propertyNames[i] : "NULL");
        }
    }
    
    g_transformObjects.insert(subjectName);
    g_transformObjectProperties[subjectName] = properties;
    
    std::string params = "subjectName='" + std::string(subjectName) + "', propertyNames=" + 
                        FormatStringArray(propertyNames, propertyCount) + ", count=" + std::to_string(propertyCount);
    LogCall("ULL_RegisterObjectWithProperties", params);
}

void ULL_UpdateObject(const char* subjectName, const ULL_Transform* transform) {
    if (!subjectName) {
        LogError("ULL_UpdateObject", "subjectName is NULL");
        return;
    }
    
    if (!g_isInitialized) {
        LogError("ULL_UpdateObject", "Not initialized");
        return;
    }
    
    // Auto-register if not already registered
    if (g_transformObjects.find(subjectName) == g_transformObjects.end()) {
        g_transformObjects.insert(subjectName);
    }
    
    std::string params = "subjectName='" + std::string(subjectName) + "', transform=" + FormatTransform(transform);
    LogCall("ULL_UpdateObject", params);
}

void ULL_UpdateObjectWithProperties(const char* subjectName, const ULL_Transform* transform, const float* propertyValues, int propertyCount) {
    if (!subjectName) {
        LogError("ULL_UpdateObjectWithProperties", "subjectName is NULL");
        return;
    }
    
    if (!g_isInitialized) {
        LogError("ULL_UpdateObjectWithProperties", "Not initialized");
        return;
    }
    
    if (propertyCount < 0) {
        LogError("ULL_UpdateObjectWithProperties", "propertyCount is negative");
        return;
    }
    
    // Check if object is registered with properties
    auto it = g_transformObjectProperties.find(subjectName);
    if (it != g_transformObjectProperties.end()) {
        if (propertyCount != (int)it->second.size()) {
            LogError("ULL_UpdateObjectWithProperties", "Property count mismatch: expected " + 
                    std::to_string(it->second.size()) + ", got " + std::to_string(propertyCount));
            return;
        }
    }
    
    // Auto-register if not already registered
    if (g_transformObjects.find(subjectName) == g_transformObjects.end()) {
        g_transformObjects.insert(subjectName);
    }
    
    std::string params = "subjectName='" + std::string(subjectName) + "', transform=" + FormatTransform(transform) +
                        ", properties=" + FormatPropertyArray(propertyValues, propertyCount);
    LogCall("ULL_UpdateObjectWithProperties", params);
}

void ULL_RemoveObject(const char* subjectName) {
    if (!subjectName) {
        LogError("ULL_RemoveObject", "subjectName is NULL");
        return;
    }
    
    if (!g_isInitialized) {
        LogError("ULL_RemoveObject", "Not initialized");
        return;
    }
    
    g_transformObjects.erase(subjectName);
    g_transformObjectProperties.erase(subjectName);
    
    LogCall("ULL_RemoveObject", "subjectName='" + std::string(subjectName) + "'");
}

//=============================================================================
// Data Subjects (Metrics/KPIs)
//=============================================================================

void ULL_RegisterDataSubject(const char* subjectName, const char** propertyNames, int propertyCount) {
    if (!subjectName) {
        LogError("ULL_RegisterDataSubject", "subjectName is NULL");
        return;
    }
    
    if (!g_isInitialized) {
        LogError("ULL_RegisterDataSubject", "Not initialized");
        return;
    }
    
    if (propertyCount < 0) {
        LogError("ULL_RegisterDataSubject", "propertyCount is negative");
        return;
    }
    
    // Store property names
    std::vector<std::string> properties;
    if (propertyNames && propertyCount > 0) {
        for (int i = 0; i < propertyCount; ++i) {
            properties.push_back(propertyNames[i] ? propertyNames[i] : "NULL");
        }
    }
    
    g_dataSubjectProperties[subjectName] = properties;
    
    std::string params = "subjectName='" + std::string(subjectName) + "', propertyNames=" + 
                        FormatStringArray(propertyNames, propertyCount) + ", count=" + std::to_string(propertyCount);
    LogCall("ULL_RegisterDataSubject", params);
}

void ULL_UpdateDataSubject(const char* subjectName, const char** propertyNames, const float* propertyValues, int propertyCount) {
    if (!subjectName) {
        LogError("ULL_UpdateDataSubject", "subjectName is NULL");
        return;
    }
    
    if (!g_isInitialized) {
        LogError("ULL_UpdateDataSubject", "Not initialized");
        return;
    }
    
    if (propertyCount < 0) {
        LogError("ULL_UpdateDataSubject", "propertyCount is negative");
        return;
    }
    
    // Check if subject is registered
    auto it = g_dataSubjectProperties.find(subjectName);
    if (it != g_dataSubjectProperties.end()) {
        // Already registered - check property count
        if (propertyCount != (int)it->second.size()) {
            LogError("ULL_UpdateDataSubject", "Property count mismatch: expected " + 
                    std::to_string(it->second.size()) + ", got " + std::to_string(propertyCount));
            return;
        }
    } else {
        // Auto-register with provided property names
        if (propertyNames && propertyCount > 0) {
            std::vector<std::string> properties;
            for (int i = 0; i < propertyCount; ++i) {
                properties.push_back(propertyNames[i] ? propertyNames[i] : "NULL");
            }
            g_dataSubjectProperties[subjectName] = properties;
        }
    }
    
    std::string params = "subjectName='" + std::string(subjectName) + "'";
    if (propertyNames) {
        params += ", propertyNames=" + FormatStringArray(propertyNames, propertyCount);
    } else {
        params += ", propertyNames=NULL";
    }
    params += ", values=" + FormatPropertyArray(propertyValues, propertyCount);
    
    LogCall("ULL_UpdateDataSubject", params);
}

void ULL_RemoveDataSubject(const char* subjectName) {
    if (!subjectName) {
        LogError("ULL_RemoveDataSubject", "subjectName is NULL");
        return;
    }
    
    if (!g_isInitialized) {
        LogError("ULL_RemoveDataSubject", "Not initialized");
        return;
    }
    
    g_dataSubjectProperties.erase(subjectName);
    
    LogCall("ULL_RemoveDataSubject", "subjectName='" + std::string(subjectName) + "'");
}

} // extern "C"