import Foundation

/// Bridges a push tap to navigation across the AppDelegate-to-SwiftUI seam.
///
/// **The tap callback can fire at COLD LAUNCH, before SwiftUI has wired the handler.** With no buffer
/// the destination is dropped and the user lands on Home instead of the order they tapped. This holds
/// an early tap and flushes it the instant a handler is assigned.
/// → /architecture/push-notifications
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
