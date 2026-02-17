#import <UIKit/UIKit.h>

static UIImpactFeedbackGenerator *lightGen = nil;
static UIImpactFeedbackGenerator *mediumGen = nil;
static UIImpactFeedbackGenerator *heavyGen = nil;
static UINotificationFeedbackGenerator *notifGen = nil;

static void EnsureGenerators() {
    if (lightGen == nil) {
        lightGen = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleLight];
        [lightGen prepare];
    }
    if (mediumGen == nil) {
        mediumGen = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleMedium];
        [mediumGen prepare];
    }
    if (heavyGen == nil) {
        heavyGen = [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleHeavy];
        [heavyGen prepare];
    }
    if (notifGen == nil) {
        notifGen = [[UINotificationFeedbackGenerator alloc] init];
        [notifGen prepare];
    }
}

extern "C" {
    bool _HapticSupported() {
        if (@available(iOS 10.0, *)) {
            return YES;
        }
        return NO;
    }

    void _HapticLight() {
        EnsureGenerators();
        [lightGen impactOccurred];
        [lightGen prepare];
    }

    void _HapticMedium() {
        EnsureGenerators();
        [mediumGen impactOccurred];
        [mediumGen prepare];
    }

    void _HapticHeavy() {
        EnsureGenerators();
        [heavyGen impactOccurred];
        [heavyGen prepare];
    }

    void _HapticSuccess() {
        EnsureGenerators();
        [notifGen notificationOccurred:UINotificationFeedbackTypeSuccess];
        [notifGen prepare];
    }
}
