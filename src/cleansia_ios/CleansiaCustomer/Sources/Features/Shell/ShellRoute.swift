/// Every child route of the signed-in shell, registered ONCE on the single
/// shell-level `NavigationStack` (ADR-0022 — the erased `NavigationPath`
/// retires the iOS-16 sibling-typed-path crash class).
enum ShellRoute: Hashable, Codable {
    case orderDetail(String)
    case subscribePlus
    case membershipSuccess
    case recurringList
    case createRecurring(orderId: String?)
    case rewardsActivity
    case disputes
    /// Non-optional on purpose: a dispute is always ABOUT an order, and
    /// `CreateDisputeViewModel.submit` cannot post without one. Making the id
    /// optional is what let the disputes-list "+" push a form that could never
    /// be submitted — the type now makes that route unrepresentable.
    case createDispute(orderId: String)
    case disputeDetail(String)
    case addresses
    case editProfile(showBookingHint: Bool)
    case devices
    case notifications
    case security
    case language
    case appearance
    case help
    case deleteAccount
}
