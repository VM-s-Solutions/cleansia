enum ShellTab: CaseIterable {
    case dashboard
    case orders
    case invoices
    case profile

    var label: String {
        switch self {
        case .dashboard: L10n.Shell.dashboard
        case .orders: L10n.Shell.orders
        case .invoices: L10n.Shell.invoices
        case .profile: L10n.Shell.profile
        }
    }

    var systemImage: String {
        switch self {
        case .dashboard: "square.grid.2x2"
        case .orders: "list.clipboard"
        case .invoices: "dollarsign.circle"
        case .profile: "person"
        }
    }
}
