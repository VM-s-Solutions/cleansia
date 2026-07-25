import Foundation

/// The Live Activity's user-facing copy. It lives in Core — not in the customer app's `L10n` — because the
/// widget is a SEPARATE target/process that links CleansiaCore but not the app module, so Core's catalog is
/// the only one both sides can read.
///
/// The widget never calls `CoreL10n.apply(languageTag:)` (that is the app's launch path, and an extension
/// has its own process and its own `UserDefaults` domain), so these resolve against the device's preferred
/// languages rather than the in-app language override.
public enum LiveActivityL10n {
    public enum Status {
        public static var onTheWayTitle: String {
            CoreL10n.localized("live_activity.status.on_the_way.title")
        }

        public static var onTheWayDetail: String {
            CoreL10n.localized("live_activity.status.on_the_way.detail")
        }

        public static var inProgressTitle: String {
            CoreL10n.localized("live_activity.status.in_progress.title")
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

    public enum Eta {
        /// The lock-screen caption under the ticking digits.
        public static var remaining: String {
            CoreL10n.localized("live_activity.eta.remaining")
        }

        /// The Dynamic Island's narrower slot wants the shortest wording for the same meaning.
        public static var left: String {
            CoreL10n.localized("live_activity.eta.left")
        }

        public static var elapsed: String {
            CoreL10n.localized("live_activity.eta.elapsed")
        }
    }

    public static var bookingFallback: String {
        CoreL10n.localized("live_activity.booking_fallback")
    }

    public static func orderNumber(_ number: String) -> String {
        String(format: CoreL10n.localized("live_activity.order_number"), number)
    }
}
