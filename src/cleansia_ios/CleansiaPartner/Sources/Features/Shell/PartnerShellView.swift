import CleansiaCore
import SwiftUI

@MainActor
final class ShellModel: ViewModel {
    @Published var selection: ShellTab = .dashboard

    func selectOrders() {
        selection = .orders
    }
}

struct PartnerShellView: View {
    @StateObject private var model = ShellModel()
    private let container: PartnerAppContainer

    init(container: PartnerAppContainer) {
        self.container = container
    }

    var body: some View {
        TabView(selection: $model.selection) {
            DashboardView(
                client: container.dashboardClient,
                onOpenOrders: { model.selectOrders() }
            )
            .tabItem { Label(ShellTab.dashboard.label, systemImage: ShellTab.dashboard.systemImage) }
            .tag(ShellTab.dashboard)

            PlaceholderTab(tab: .orders, ticket: "T-0307")
                .tag(ShellTab.orders)

            PlaceholderTab(tab: .invoices, ticket: "T-0309")
                .tag(ShellTab.invoices)

            PlaceholderTab(tab: .profile, ticket: "T-0310")
                .tag(ShellTab.profile)
        }
        .tint(CleansiaColors.primary)
    }
}

private struct PlaceholderTab: View {
    let tab: ShellTab
    let ticket: String

    var body: some View {
        PlaceholderDestination(
            systemImage: tab.systemImage,
            text: L10n.Shell.placeholderComingSoon(tab.label, ticket)
        )
        .tabItem { Label(tab.label, systemImage: tab.systemImage) }
    }
}

#if DEBUG
    struct PartnerShellView_Previews: PreviewProvider {
        static var previews: some View {
            TabView {
                PlaceholderDestination(systemImage: ShellTab.dashboard.systemImage, text: "Dashboard")
                    .tabItem { Label(ShellTab.dashboard.label, systemImage: ShellTab.dashboard.systemImage) }
                PlaceholderDestination(systemImage: ShellTab.orders.systemImage, text: "Orders — coming in T-0307")
                    .tabItem { Label(ShellTab.orders.label, systemImage: ShellTab.orders.systemImage) }
                PlaceholderDestination(systemImage: ShellTab.invoices.systemImage, text: "Invoices — coming in T-0309")
                    .tabItem { Label(ShellTab.invoices.label, systemImage: ShellTab.invoices.systemImage) }
                PlaceholderDestination(systemImage: ShellTab.profile.systemImage, text: "Profile — coming in T-0310")
                    .tabItem { Label(ShellTab.profile.label, systemImage: ShellTab.profile.systemImage) }
            }
            .tint(CleansiaColors.primary)
        }
    }
#endif
