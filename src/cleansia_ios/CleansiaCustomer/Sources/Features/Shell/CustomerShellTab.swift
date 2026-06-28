enum CustomerShellTab: CaseIterable {
    case home
    case orders
    case rewards
    case profile

    var label: String {
        switch self {
        case .home: L10n.Shell.home
        case .orders: L10n.Shell.orders
        case .rewards: L10n.Shell.rewards
        case .profile: L10n.Shell.profile
        }
    }

    var systemImage: String {
        switch self {
        case .home: "house"
        case .orders: "doc.text"
        case .rewards: "gift"
        case .profile: "person"
        }
    }
}
