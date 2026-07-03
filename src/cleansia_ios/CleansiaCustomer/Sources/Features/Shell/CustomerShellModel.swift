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

    /// Pill taps animate the pager — the `animateScrollToPage` parity
    /// (`MainShell.kt:97-99`).
    func select(_ tab: CustomerShellTab) {
        withAnimation { selection = tab }
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

    func openEditProfile() {
        selection = .profile
        path = NavigationPath([ShellRoute.editProfile])
    }

    func pop() {
        if !path.isEmpty { path.removeLast() }
    }
}
