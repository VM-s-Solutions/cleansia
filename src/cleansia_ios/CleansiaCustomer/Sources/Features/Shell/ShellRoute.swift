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
    case createDispute(orderId: String?)
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
