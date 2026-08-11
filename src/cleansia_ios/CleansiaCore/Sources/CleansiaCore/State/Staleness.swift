import Foundation

/// Freshness watermark for one logical cache. The owner stamps `markFresh()`
/// after a successful fetch; screen-entry and foreground hooks ask `isStale`
/// before spending a round-trip, so re-entering a screen inside `window` costs
/// nothing. User-initiated pulls bypass it entirely — the user's intent
/// outranks the cache age.
///
/// This exists because a "loaded" flag cannot double as a freshness signal: it
/// is a one-way first-paint latch, so anything that changes server-side after
/// the first fetch sits stale for the rest of the session.
///
/// Orthogonal to `SessionScopedCache`. A repository that owns one must call
/// `invalidate()` from its own `clear()`, or the watermark outlives sign-out
/// and the next account reads the previous account's snapshot as fresh.
///
/// `MainActor`-isolated, like the repositories that hold one.
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
