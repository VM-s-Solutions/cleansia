import CleansiaCore
import SwiftUI

@MainActor
final class CustomerShellModel: ViewModel {
    @Published var selection: CustomerShellTab = .home
    @Published var path = NavigationPath()
    @Published var isBookingPresented = false
    @Published var isAddressManagerPresented = false

    func book() {
        isBookingPresented = true
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

    func openOrders() {
        selection = .orders
        path = NavigationPath()
    }

    func openEditProfile(showBookingHint: Bool = false) {
        selection = .profile
        path = NavigationPath([ShellRoute.editProfile(showBookingHint: showBookingHint)])
    }

    func pop() {
        if !path.isEmpty { path.removeLast() }
    }
}
