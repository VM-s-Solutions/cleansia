import Foundation

/// Typed destinations pushed onto the Orders `NavigationStack` (the
/// intra-audience push, ADR-0020). The real `OrderDetailView` replaces the
/// placeholder detail behind `.detail`.
enum OrderRoute: Hashable {
    case detail(orderId: String)
}
