import Foundation

/// The Live Activity's user-facing copy. It lives in Core — not in the customer app's `L10n` — because the
/// widget is a SEPARATE target/process that links CleansiaCore but not the app module, so Core's catalog is
/// the only one both sides can read.
///
/// The widget has its own process and its own `UserDefaults` domain, so it follows the in-app language by
/// reading the tag the app leaves in their App Group (`AppGroupLanguage`) and calling
/// `CoreL10n.apply(languageTag:)` with it. Before the app has written one, these resolve against the
/// device's preferred languages — which works only because the extension declares all five in
/// `CFBundleLocalizations`; Core's bundle can resolve no language its host does not.
public enum LiveActivityL10n {
    /// The leg vocabulary of the four-step journey. Terse on purpose: these sit under a quarter-width
    /// segment of the progress bar.
    public enum Step {
        public static var confirmed: String {
            CoreL10n.localized("live_activity.step.confirmed")
        }

        public static var onTheWay: String {
            CoreL10n.localized("live_activity.step.on_the_way")
        }

        public static var cleaning: String {
            CoreL10n.localized("live_activity.step.cleaning")
        }

        public static var done: String {
            CoreL10n.localized("live_activity.step.done")
        }
    }

    public enum Status {
        public static var onTheWayDetail: String {
            CoreL10n.localized("live_activity.status.on_the_way.detail")
        }

        public static var inProgressDetail: String {
            CoreL10n.localized("live_activity.status.in_progress.detail")
        }

        public static var completedTitle: String {
            CoreL10n.localized("live_activity.status.completed.title")
        }

        public static var completedDetail: String {
            CoreL10n.localized("live_activity.status.completed.detail")
        }

        public static var cancelledTitle: String {
            CoreL10n.localized("live_activity.status.cancelled.title")
        }

        public static var cancelledDetail: String {
            CoreL10n.localized("live_activity.status.cancelled.detail")
        }

        public static var genericTitle: String {
            CoreL10n.localized("live_activity.status.generic.title")
        }
    }

    /// The caption over the wall-clock instant the clean is expected to end.
    public static var finish: String {
        CoreL10n.localized("live_activity.finish")
    }

    /// The caption over the wall-clock instant the cleaner is expected to arrive.
    public static var arrival: String {
        CoreL10n.localized("live_activity.arrival")
    }

    /// Spoken form of the stepper. It is the accessibility label of the four legs, and the written
    /// position in the expanded Dynamic Island — the compact slot has room for the dots only.
    public static func stepOf(_ position: Int, _ total: Int) -> String {
        String(format: CoreL10n.localized("live_activity.step_of"), position, total)
    }

    public static var bookingFallback: String {
        CoreL10n.localized("live_activity.booking_fallback")
    }

    public static func orderNumber(_ number: String) -> String {
        String(format: CoreL10n.localized("live_activity.order_number"), number)
    }
}
