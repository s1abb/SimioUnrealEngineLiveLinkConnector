#include "MockLiveLink.h"
#include <iostream>

// Simple test to verify log clearing functionality
int main() {
    std::cout << "Testing MockLiveLink log clearing functionality..." << std::endl;
    
    // Test 1: Initialize (should clear log)
    int result = ULL_Initialize("TestProvider");
    if (result != 0) {
        std::cout << "ERROR: ULL_Initialize failed" << std::endl;
        return 1;
    }
    
    // Test 2: Add some test calls
    ULL_IsConnected();
    ULL_RegisterObject("TestObject");
    
    // Test 3: Shutdown
    ULL_Shutdown();
    
    std::cout << "Test completed! Check tests\\Simio.Tests\\SimioUnrealLiveLink_Mock.log" << std::endl;
    return 0;
}