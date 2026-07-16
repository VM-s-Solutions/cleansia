import Foundation

/// Bridges a push-notification tap to the app's navigation across the
/// AppDelegate→SwiftUI seam.
///
/// The `UNUserNotificationCenter` `didReceive` callback can fire at COLD LAUNCH —
/// before the SwiftUI layer's `.task` has wired the navigation handler. If the
/// handler is nil at that moment the resolved destination would be dropped and
/// the user lands on Home instead of the tapped order. This buffers a tap that
/// arrives before `onTap` is assigned and flushes it the instant one is; a tap
/// that arrives after fires immediately.
///
/// Main-actor isolated: both the notification-center callback and the `.task`
/// assignment run on the main thread, so the buffer needs no locking.
@MainActor
public final class PushTapBuffer<Destination> {
    private var pending: Destination?

    /// The navigation handler, assigned by the SwiftUI layer once it appears.
    /// Assigning it flushes any tap that arrived first (cold-launch case).
    public var onTap: ((Destination) -> Void)? {
        didSet {
            guard let onTap, let buffered = pending else { return }
            pending = nil
            onTap(buffered)
        }
    }

    public nonisolated init() {}

    /// Deliver a resolved tap destination: fire now if a handler is attached,
    /// otherwise buffer it (latest wins) until one is.
    public func deliver(_ destination: Destination) {
        if let onTap {
            onTap(destination)
        } else {
            pending = destination
        }
    }
}
