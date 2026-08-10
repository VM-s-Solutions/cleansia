import Foundation

/// Typed destinations pushed onto the Orders `NavigationStack` (the
/// intra-audience push, ADR-0020). The real `OrderDetailView` replaces the
/// placeholder detail behind `.detail`.
enum OrderRoute: Hashable {
    case detail(orderId: String)
}

/// The dashboard tab's own stack. Jobs a customer asked for this cleaner by name are pushed rather
/// than given a bottom-tab of their own: a reservation is rare and time-limited, and a permanent tab
/// would show an empty state to every cleaner every day for it.
enum DashboardRoute: Hashable {
    case pendingOffers
    case orderDetail(orderId: String)
}
