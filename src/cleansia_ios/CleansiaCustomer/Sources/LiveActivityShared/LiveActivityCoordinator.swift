import ActivityKit
import Foundation

/// Owns the ActivityKit lifecycle for the in-progress-clean Live Activity (ADR-0029), app-side. Starts an
/// activity with a push token so the backend drives updates while the app is closed, hands the tokens to
/// the backend via `LiveActivityRegistering`, and ends it on a terminal status.
///
/// Wiring (done once the widget target builds green): the order-tracking screen calls `start(...)` when an
/// order first reaches Confirmed/OnTheWay and `end(orderId:)` on Completed/Cancelled; app launch (post
/// login) calls `beginPushToStartRegistration()` so the SERVER can start activities on iOS 17.2+.
///
/// The backend registration itself is the one regen-gated piece: `LiveActivityRegistering`'s live
/// implementation calls the generated `CleansiaCustomerApi` LiveActivity client, which only exists after
/// the customer-mobile spec regen (T-0427 owner step). Until then a no-op keeps everything compiling and
/// the local activity still starts/ends — it just isn't server-pushed yet.
///
/// Floor is iOS 16.2 (the `ActivityContent` + push-token API); the app itself stays 16.0, so callers gate
/// on `#available(iOS 16.2, *)`. On a rare 16.0/16.1 device no activity starts — harmless, the order still
/// tracks in-app.
@available(iOS 16.2, *)
@MainActor
final class LiveActivityCoordinator {
    static let shared = LiveActivityCoordinator(registrar: NoopLiveActivityRegistering())

    private var registrar: LiveActivityRegistering
    private var started: [String: Activity<CleanOrderAttributes>] = [:] // orderId -> live activity
    private var tokenObservers: [String: Task<Void, Never>] = [:] // orderId -> pushTokenUpdates observer
    private var pushToStartObserver: Task<Void, Never>?

    init(registrar: LiveActivityRegistering) {
        self.registrar = registrar
    }

    /// Swap the no-op seam for the live backend registrar once the composition root has the session's
    /// device id + auth spine (`CustomerAppContainer`). Idempotent-safe: install before the first `start`.
    func install(registrar: LiveActivityRegistering) {
        self.registrar = registrar
    }

    /// Whether the user has Live Activities enabled for the app (the Settings toggle).
    var isEnabled: Bool {
        ActivityAuthorizationInfo().areActivitiesEnabled
    }

    /// Start (or reuse) the live activity for an order and register its push token so the backend can push
    /// status updates. Idempotent: if an activity for this order already exists, it does nothing (call
    /// `update` to rewrite the content-state of a running activity). The initial content reflects the
    /// order's CURRENT status, so opening an already-in-progress order renders "Cleaning in progress"
    /// rather than a stale "On the way".
    func start(orderId: String, orderNumber: String, status: String, scheduledStart: Date, scheduledEnd: Date) {
        guard isEnabled, existingActivity(orderId: orderId, orderNumber: orderNumber) == nil else { return }

        let attributes = CleanOrderAttributes(orderNumber: orderNumber)
        let initialState = CleanOrderAttributes.ContentState(
            v: 1, status: status, orderNumber: orderNumber,
            scheduledStart: scheduledStart, scheduledEnd: scheduledEnd
        )

        do {
            let activity = try Activity<CleanOrderAttributes>.request(
                attributes: attributes,
                content: ActivityContent(state: initialState, staleDate: scheduledEnd.addingTimeInterval(3600)),
                pushType: .token
            )
            started[orderId] = activity
            observePushToken(of: activity, orderId: orderId, orderNumber: orderNumber)
        } catch {
            // areActivitiesEnabled can race the request, or the per-app activity budget is exhausted.
            // Non-fatal: the order still tracks in-app; the activity simply isn't shown.
        }
    }

    /// Rewrite the content-state of the order's running activity (e.g. OnTheWay → InProgress). No-op if no
    /// activity is running for the order — reuses the same `existingActivity` identity as `start`, so it
    /// also drives a system-restored / server-started activity. This is the on-device path that keeps the
    /// Live Activity in sync while the app is active, independent of the (regen-gated) backend push channel.
    func update(orderId: String, orderNumber: String, status: String, scheduledStart: Date, scheduledEnd: Date) {
        guard let activity = existingActivity(orderId: orderId, orderNumber: orderNumber) else { return }
        let state = CleanOrderAttributes.ContentState(
            v: 1, status: status, orderNumber: orderNumber,
            scheduledStart: scheduledStart, scheduledEnd: scheduledEnd
        )
        Task {
            await activity.update(ActivityContent(state: state, staleDate: scheduledEnd.addingTimeInterval(3600)))
        }
    }

    /// End the order's live activity immediately and deregister its token.
    func end(orderId: String) {
        tokenObservers.removeValue(forKey: orderId)?.cancel()
        let live = started.removeValue(forKey: orderId)
        Task { [registrar] in
            if let live { await live.end(nil, dismissalPolicy: .default) }
            await registrar.deregister(orderId: orderId)
        }
    }

    /// Register the app's push-to-start token so the SERVER can start activities without the app running
    /// (iOS 17.2+). No-op on earlier OSes — activities there start only from the foreground via `start`.
    func beginPushToStartRegistration() {
        guard #available(iOS 17.2, *), pushToStartObserver == nil else { return }
        pushToStartObserver = Task { [registrar] in
            for await tokenData in Activity<CleanOrderAttributes>.pushToStartTokenUpdates {
                await registrar.registerPushToStart(token: hexString(tokenData))
            }
        }
    }

    // MARK: - Internals

    /// The activity already running for this order, if any. Keyed by `orderId` for the current session;
    /// falls back to matching a system-restored / server-started activity by its `orderNumber` — the only
    /// stable identity in `CleanOrderAttributes` (it deliberately carries no order id, per ADR-0029 D4).
    private func existingActivity(orderId: String, orderNumber: String) -> Activity<CleanOrderAttributes>? {
        if let live = started[orderId] { return live }
        // Don't match a system-restored activity on an empty order number — that would grab an
        // unrelated one. Only fall back when we have a real number to match on.
        guard !orderNumber.isEmpty else { return nil }
        return Activity<CleanOrderAttributes>.activities.first { $0.attributes.orderNumber == orderNumber }
    }

    private func observePushToken(of activity: Activity<CleanOrderAttributes>, orderId: String, orderNumber: String) {
        tokenObservers[orderId]?.cancel()
        tokenObservers[orderId] = Task { [registrar] in
            for await tokenData in activity.pushTokenUpdates {
                await registrar.register(orderId: orderId, orderNumber: orderNumber, token: hexString(tokenData))
            }
        }
    }
}

/// Lowercase hex of the raw APNs token — the wire form the backend `LiveActivityToken.Token` stores.
private func hexString(_ data: Data) -> String {
    data.map { String(format: "%02x", $0) }.joined()
}

/// The backend registration seam. The live implementation posts to `/api/LiveActivity/Register` and
/// `DELETE /api/LiveActivity/{orderId}` via the generated `CleansiaCustomerApi` client — added once the
/// customer-mobile spec is regenerated (T-0427). Token strings are lowercase hex of the raw APNs token.
protocol LiveActivityRegistering: Sendable {
    func register(orderId: String, orderNumber: String, token: String) async
    func registerPushToStart(token: String) async
    func deregister(orderId: String) async
}

/// Compiles today, does nothing — the local activity still starts/ends; it just isn't server-pushed until
/// the generated client lands and `LiveActivityCoordinator.shared` is swapped for the live registrar.
struct NoopLiveActivityRegistering: LiveActivityRegistering {
    func register(orderId _: String, orderNumber _: String, token _: String) async {}
    func registerPushToStart(token _: String) async {}
    func deregister(orderId _: String) async {}
}
