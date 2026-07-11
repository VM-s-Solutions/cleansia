enum CustomerShellTab: CaseIterable {
    case home
    case orders
    case book
    case rewards
    case profile

    /// The four real, labeled destinations in bar order. `.book` is the center
    /// placeholder slot the docked Book FAB reserves so the five bar elements
    /// space evenly — it never rests as a selection, so it is not a real tab.
    static let navigationTabs: [CustomerShellTab] = [.home, .orders, .rewards, .profile]

    var label: String {
        switch self {
        case .home: L10n.Shell.home
        case .orders: L10n.Shell.orders
        case .book: ""
        case .rewards: L10n.Shell.rewards
        case .profile: L10n.Shell.profile
        }
    }

    var systemImage: String {
        switch self {
        case .home: "house"
        case .orders: "doc.text"
        case .book: ""
        case .rewards: "gift"
        case .profile: "person"
        }
    }
}
