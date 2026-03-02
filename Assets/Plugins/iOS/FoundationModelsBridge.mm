#import <Foundation/Foundation.h>

// Forward declare the Swift class (generated header)
@class FoundationModelsPlugin;

// Unity callback function pointer type
typedef void (*StringCallback)(const char* result);

// Store Unity game object name for UnitySendMessage
static NSString* _unityCallbackObject = @"AITextManager";

extern "C" {
    // Check if Foundation Models is available on this device
    bool _FM_IsAvailable() {
        if (@available(iOS 26.0, *)) {
            return [FoundationModelsPlugin isAvailable];
        }
        return NO;
    }

    // Initialize a session
    void _FM_InitSession() {
        if (@available(iOS 26.0, *)) {
            [FoundationModelsPlugin initSession];
        }
    }

    // Set the Unity callback object name
    void _FM_SetCallbackObject(const char* objectName) {
        _unityCallbackObject = [NSString stringWithUTF8String:objectName];
    }

    // Generate a single bark (async - result via UnitySendMessage)
    void _FM_GenerateBark(const char* eventType) {
        if (@available(iOS 26.0, *)) {
            NSString* event = [NSString stringWithUTF8String:eventType];
            [FoundationModelsPlugin generateBarkWithEventType:event callback:^(NSString* result) {
                UnitySendMessage(
                    [_unityCallbackObject UTF8String],
                    "OnBarkGenerated",
                    [result UTF8String]
                );
            }];
        }
    }

    // Generate a death quip (async)
    void _FM_GenerateDeathQuip(const char* context) {
        if (@available(iOS 26.0, *)) {
            NSString* ctx = [NSString stringWithUTF8String:context];
            [FoundationModelsPlugin generateDeathQuipWithContext:ctx callback:^(NSString* result) {
                UnitySendMessage(
                    [_unityCallbackObject UTF8String],
                    "OnDeathQuipGenerated",
                    [result UTF8String]
                );
            }];
        }
    }

    // Generate a batch of barks (async)
    void _FM_GenerateBarkBatch(int count) {
        if (@available(iOS 26.0, *)) {
            [FoundationModelsPlugin generateBarkBatchWithCount:count callback:^(NSString* result) {
                UnitySendMessage(
                    [_unityCallbackObject UTF8String],
                    "OnBarkBatchGenerated",
                    [result UTF8String]
                );
            }];
        }
    }

    // Generate a batch of graffiti (async)
    void _FM_GenerateGraffitiBatch(int count, const char* zones) {
        if (@available(iOS 26.0, *)) {
            NSString* z = [NSString stringWithUTF8String:zones];
            [FoundationModelsPlugin generateGraffitiBatchWithCount:count zones:z callback:^(NSString* result) {
                UnitySendMessage(
                    [_unityCallbackObject UTF8String],
                    "OnGraffitiBatchGenerated",
                    [result UTF8String]
                );
            }];
        }
    }

    // Generate race commentary (async)
    void _FM_GenerateCommentary(const char* raceState) {
        if (@available(iOS 26.0, *)) {
            NSString* state = [NSString stringWithUTF8String:raceState];
            [FoundationModelsPlugin generateCommentaryWithRaceState:state callback:^(NSString* result) {
                UnitySendMessage(
                    [_unityCallbackObject UTF8String],
                    "OnCommentaryGenerated",
                    [result UTF8String]
                );
            }];
        }
    }
}
