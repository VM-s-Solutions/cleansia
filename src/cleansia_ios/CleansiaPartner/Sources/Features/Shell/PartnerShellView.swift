import CleansiaCore
import SwiftUI

@MainActor
final class ShellModel: ViewModel {
    @Published var selection: ShellTab = .dashboard

    func selectOrders() {
        selection = .orders
    }

    func selectEarnings() {
        selection = .invoices
    }
}

struct PartnerShellView: View {
    @StateObject private var model = ShellModel()
    @ObservedObject private var preferences: PreferencesModel
    @EnvironmentObject private var pushNavigation: PushNavigationModel
    @State private var deepLinkOrderId: String?
    @State private var deepLinkInvoiceId: String?
    private let container: PartnerAppContainer
    private let onSignedOut: () -> Void

    init(
        container: PartnerAppContainer,
        preferences: PreferencesModel,
        onSignedOut: @escaping () -> Void
    ) {
        self.container = container
        self.preferences = preferences
        self.onSignedOut = onSignedOut
    }

    var body: some View {
        tabs
            .onChange(of: pushNavigation.pendingDestination) { destination in
                guard let destination else { return }
                apply(PushTapRouting.plan(for: destination))
                _ = pushNavigation.consume()
            }
            .onAppear {
                if let destination = pushNavigation.pendingDestination {
                    apply(PushTapRouting.plan(for: destination))
                    _ = pushNavigation.consume()
                }
            }
    }

    private func apply(_ plan: PushTapRouting.Plan) {
        if plan.selectEarningsTab {
            model.selectEarnings()
            deepLinkInvoiceId = plan.invoiceId
        } else {
            model.selectOrders()
            deepLinkOrderId = plan.orderId
        }
    }

    private var tabs: some View {
        TabView(selection: $model.selection) {
            DashboardView(
                client: container.dashboardClient,
                notificationBadge: container.notificationBadge,
                notificationFeedClient: container.notificationFeedClient,
                snackbar: container.snackbar,
                onOpenEarnings: { model.selectEarnings() },
                onOpenOrders: { model.selectOrders() },
                // Feed-row taps land exactly where a push tap does — the same
                // resolver, the same routing plan (FD-AC9).
                onNotificationDestination: { apply(PushTapRouting.plan(for: $0)) }
            )
            .tabItem { Label(ShellTab.dashboard.label, systemImage: ShellTab.dashboard.systemImage) }
            .tag(ShellTab.dashboard)

            OrdersRootView(
                client: container.orderClient,
                staleness: container.ordersStaleness,
                checklistStore: container.cleaningChecklistStore,
                snackbar: container.snackbar,
                mapProvider: container.mapProvider,
                deepLinkOrderId: $deepLinkOrderId
            )
            .tabItem { Label(ShellTab.orders.label, systemImage: ShellTab.orders.systemImage) }
            .tag(ShellTab.orders)

            EarningsView(
                dashboardClient: container.dashboardClient,
                payrollClient: container.payrollClient,
                invoicesStaleness: container.invoicesStaleness,
                snackbar: container.snackbar,
                deepLinkInvoiceId: $deepLinkInvoiceId
            )
            .tabItem { Label(ShellTab.invoices.label, systemImage: ShellTab.invoices.systemImage) }
            .tag(ShellTab.invoices)

            ProfileView(
                client: container.profileClient,
                devicesClient: container.devicesClient,
                authClient: container.authClient,
                snackbar: container.snackbar,
                geocoding: container.geocodingService,
                mapProvider: container.mapProvider,
                serviceArea: container.serviceArea,
                preferences: preferences,
                onSignedOut: onSignedOut
            )
            .tabItem { Label(ShellTab.profile.label, systemImage: ShellTab.profile.systemImage) }
            .tag(ShellTab.profile)
        }
        .tint(CleansiaColors.primary)
    }
}

#if DEBUG
    struct PartnerShellView_Previews: PreviewProvider {
        static var previews: some View {
            TabView {
                PlaceholderDestination(systemImage: ShellTab.dashboard.systemImage, text: "Dashboard")
                    .tabItem { Label(ShellTab.dashboard.label, systemImage: ShellTab.dashboard.systemImage) }
                PlaceholderDestination(systemImage: ShellTab.orders.systemImage, text: "Orders")
                    .tabItem { Label(ShellTab.orders.label, systemImage: ShellTab.orders.systemImage) }
                PlaceholderDestination(systemImage: ShellTab.invoices.systemImage, text: "Earnings")
                    .tabItem { Label(ShellTab.invoices.label, systemImage: ShellTab.invoices.systemImage) }
                PlaceholderDestination(systemImage: ShellTab.profile.systemImage, text: "Profile")
                    .tabItem { Label(ShellTab.profile.label, systemImage: ShellTab.profile.systemImage) }
            }
            .tint(CleansiaColors.primary)
        }
    }
#endif
