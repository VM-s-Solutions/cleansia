import CleansiaCore
import SwiftUI

@MainActor
final class CustomerShellModel: ViewModel {
    @Published var selection: CustomerShellTab = .home
    @Published var path = NavigationPath()
    @Published var isBookingPresented = false
    @Published var isAddressManagerPresented = false

    /// The order whose detail should raise the review sheet on arrival.
    ///
    /// Model state rather than a `ShellRoute` payload deliberately. Adding an associated value to
    /// `.orderDetail` would be compile-enforced but would churn all thirteen construction sites — eight
    /// of them test equality assertions — for a flag that is false on every path but this one. Cleared
    /// as soon as the detail consumes it, so a later manual visit to the same order does not re-open.
    @Published var reviewPromptOrderId: String?

    private var lastRealTab: CustomerShellTab = .home

    func book() {
        isBookingPresented = true
    }

    /// Resolves a stock `TabView` selection change. The center `.book` slot is a
    /// placeholder the docked FAB reserves for even spacing, never a real
    /// destination: selecting it snaps back to the tab the user came from and
    /// reports `true` so the caller opens booking — the bar center mirrors the
    /// FAB. Real tabs pass through and are remembered as the snap-back target.
    func resolveSelection() -> Bool {
        guard selection == .book else {
            lastRealTab = selection
            return false
        }
        selection = lastRealTab
        return true
    }

    /// Programmatic cross-tab jumps (Home's "see all orders" → Orders, the
    /// referral card → Rewards). Tab-bar taps drive the selection binding
    /// directly through the stock `TabView`.
    func select(_ tab: CustomerShellTab) {
        selection = tab
    }

    /// The T-0313 success→OrderDetail fold: jump to the Orders tab and open the
    /// new order's detail (Orders didn't exist when the success screen shipped).
    func openOrder(_ orderId: String) {
        selection = .orders
        path = NavigationPath([ShellRoute.orderDetail(orderId)])
    }

    /// The completion prompt's landing: open the order's own detail with the review sheet raised.
    ///
    /// Routed to the detail rather than hosted as a shell sheet because that screen already owns the
    /// review state, the submit path and the success/close wiring — a second host would be a second
    /// copy of all three.
    func openOrderForReview(_ orderId: String) {
        reviewPromptOrderId = orderId
        openOrder(orderId)
    }

    func consumeReviewPrompt() {
        reviewPromptOrderId = nil
    }

    func openOrders() {
        selection = .orders
        path = NavigationPath()
    }

    func openEditProfile(showBookingHint: Bool = false) {
        selection = .profile
        path = NavigationPath([ShellRoute.editProfile(showBookingHint: showBookingHint)])
    }

    /// A push-notification tap replaces whatever the user was doing: any open
    /// modal sheet is dismissed (the pushed destination would otherwise be
    /// invisible under it), then the plan's tab + stack land the destination.
    func applyPushTap(_ plan: CustomerPushTapRouting.Plan) {
        isBookingPresented = false
        isAddressManagerPresented = false
        selection = plan.tab
        path = NavigationPath(plan.routes)
    }

    func pop() {
        if !path.isEmpty { path.removeLast() }
    }
}
