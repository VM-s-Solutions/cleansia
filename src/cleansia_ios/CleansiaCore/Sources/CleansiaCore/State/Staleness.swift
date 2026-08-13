import Foundation

/// Freshness watermark for one logical cache: stamp after a successful fetch, ask before spending a
/// round-trip.
///
/// **User-initiated pulls bypass it entirely** — the user's intent outranks cache age. **A "loaded" flag
/// cannot double as a freshness signal**: it is a one-way first-paint latch, so anything that changes
/// server-side after the first fetch never reaches the screen.
/// → /mobile-app/patterns#session-wipe
@MainActor
public final class Staleness {
    private let window: TimeInterval
    private let now: () -> Date
    private var mark: Date?

    /// `nonisolated` so it can be a default argument. Repositories declare
    /// `staleness: Staleness = Staleness()`, and a default argument is evaluated in the
    /// CALLER's context, which is nonisolated — an isolated init there is a compile error
    /// at every call site rather than at the declaration, which is why it is easy to miss.
    /// Safe because the initializer only assigns stored properties; every subsequent access
    /// is still `MainActor`-isolated.
    public nonisolated init(window: TimeInterval = 30, now: @escaping () -> Date = Date.init) {
        self.window = window
        self.now = now
    }

    public var isStale: Bool {
        guard let mark else { return true }
        return now().timeIntervalSince(mark) >= window
    }

    /// Stamp the watermark. Call only after a fetch that actually landed — a
    /// failed fetch left the cache untouched, so the next entry must retry.
    public func markFresh() {
        mark = now()
    }

    /// Forget the watermark: the next check reads stale. Sign-out, and any
    /// mutation the owner knows invalidated the cache.
    public func invalidate() {
        mark = nil
    }
}
