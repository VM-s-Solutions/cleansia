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

    func selectProfile() {
        selection = .profile
    }
}

struct PartnerShellView: View {
    @StateObject private var model = ShellModel()
    @ObservedObject private var preferences: PreferencesModel
    @EnvironmentObject private var pushNavigation: PushNavigationModel
    @State private var deepLinkOrderId: String?
    @State private var deepLinkInvoiceId: String?
    @State private var dashboardPath: [DashboardRoute] = []
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

    private var dashboardTab: some View {
        NavigationStack(path: $dashboardPath) {
            DashboardView(
                client: container.dashboardClient,
                notificationBadge: container.notificationBadge,
                notificationFeedClient: container.notificationFeedClient,
                profileClient: container.profileClient,
                settings: container.appSettings,
                snackbar: container.snackbar,
                pendingOffers: container.pendingOffers,
                onOpenEarnings: { model.selectEarnings() },
                onOpenOrders: { model.selectOrders() },
                onOpenPendingOffers: { dashboardPath.append(.pendingOffers) },
                // Feed-row taps land exactly where a push tap does — the same
                // resolver, the same routing plan (FD-AC9).
                onNotificationDestination: { apply(PushTapRouting.plan(for: $0)) },
                onOpenProfile: { model.selectProfile() },
                // Documents are a SECTION of the profile screen on iOS, not a screen of their own as
                // on Android — so both tiles land on the same tab. Kept as a separate tile because the
                // owner asked for parity with Android's row; it is a real shortcut, just a shallower
                // one until documents get their own route.
                onOpenDocuments: { model.selectProfile() },
                // Android's Help tile is a stub too (`onHelp = { /* Phase 9 */ }`). Parity includes
                // the gap; a tile that silently does nothing is at least the same nothing.
                onOpenHelp: {}
            )
            .toolbar(.hidden, for: .navigationBar)
            .navigationDestination(for: DashboardRoute.self) { route in
                switch route {
                case .pendingOffers:
                    PendingOffersView(
                        store: container.pendingOffers,
                        client: container.orderClient,
                        staleness: container.ordersStaleness,
                        snackbar: container.snackbar,
                        // A confirmed offer is an ordinary job from that instant on, so it lands on
                        // the detail every other taken job lands on — and the offers screen it came
                        // from is popped out from under it.
                        onOpenOrder: { dashboardPath = [.orderDetail(orderId: $0)] }
                    )
                case let .orderDetail(orderId):
                    OrderDetailView(
                        orderId: orderId,
                        client: container.orderClient,
                        staleness: container.ordersStaleness,
                        checklistStore: container.cleaningChecklistStore,
                        snackbar: container.snackbar,
                        mapProvider: container.mapProvider,
                        pendingOffers: container.pendingOffers
                    )
                }
            }
        }
    }

    private var tabs: some View {
        TabView(selection: $model.selection) {
            dashboardTab
                .tabItem { Label(ShellTab.dashboard.label, systemImage: ShellTab.dashboard.systemImage) }
                .tag(ShellTab.dashboard)

            OrdersRootView(
                client: container.orderClient,
                staleness: container.ordersStaleness,
                checklistStore: container.cleaningChecklistStore,
                snackbar: container.snackbar,
                mapProvider: container.mapProvider,
                pendingOffers: container.pendingOffers,
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
