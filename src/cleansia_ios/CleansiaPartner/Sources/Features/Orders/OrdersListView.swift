import CleansiaCore
import CleansiaPartnerApi
import SwiftUI

struct OrdersRootView: View {
    @StateObject private var vm: OrdersListViewModel
    @State private var path = NavigationPath()
    @Binding private var deepLinkRoute: OrderRoute?
    private let client: PartnerOrderClient
    private let profileClient: PartnerProfileClient
    private let staleness: OrdersStaleness
    private let checklistStore: CleaningChecklistStore
    private let snackbar: SnackbarController
    private let pendingOffers: PendingOffersStore
    private let boardStore: OrdersBoardStore?
    private let onOpenEarnings: () -> Void

    init(
        client: PartnerOrderClient,
        profileClient: PartnerProfileClient,
        staleness: OrdersStaleness,
        checklistStore: CleaningChecklistStore,
        snackbar: SnackbarController,
        pendingOffers: PendingOffersStore,
        payrollClient: PartnerPayrollClient? = nil,
        boardStore: OrdersBoardStore? = nil,
        deepLinkRoute: Binding<OrderRoute?> = .constant(nil),
        onOpenEarnings: @escaping () -> Void = {}
    ) {
        _vm = StateObject(wrappedValue: OrdersListViewModel(
            client: client,
            staleness: staleness,
            snackbar: snackbar,
            payroll: payrollClient,
            pendingOffers: pendingOffers,
            boardStore: boardStore
        ))
        _deepLinkRoute = deepLinkRoute
        self.client = client
        self.profileClient = profileClient
        self.staleness = staleness
        self.checklistStore = checklistStore
        self.snackbar = snackbar
        self.pendingOffers = pendingOffers
        self.boardStore = boardStore
        self.onOpenEarnings = onOpenEarnings
    }

    var body: some View {
        NavigationStack(path: $path) {
            OrdersListView(vm: vm, onOpenEarnings: onOpenEarnings)
                .navigationDestination(for: OrderRoute.self) { route in
                    switch route {
                    case let .detail(orderId, showRemovalReason):
                        OrderDetailView(
                            orderId: orderId,
                            client: client,
                            staleness: staleness,
                            checklistStore: checklistStore,
                            snackbar: snackbar,
                            pendingOffers: pendingOffers,
                            boardStore: boardStore,
                            showRemovalReason: showRemovalReason,
                            onOpenLegalDocuments: { path.append(OrderRoute.legalDocuments) },
                            onOpenJob: { path.append(OrderRoute.detail(orderId: $0)) },
                            onBrowseJobs: {
                                path = NavigationPath()
                                Task { await vm.selectTab(.available) }
                            },
                            onOpenEarnings: onOpenEarnings
                        )
                    case .legalDocuments:
                        LegalDocumentsView(client: profileClient, snackbar: snackbar)
                    }
                }
        }
        .sheet(item: contractRequest) { request in
            WorkContractSheet(
                request: request,
                client: client,
                board: vm.boardTake,
                onDismiss: vm.dismissContract,
                onOutcome: { outcome in Task { await vm.onWorkContractOutcome(outcome) } },
                onOpenLegalDocuments: {
                    vm.dismissContract()
                    path.append(OrderRoute.legalDocuments)
                },
                onOpenJob: vm.openTakenJob
            )
        }
        .offerDeclineAlert(
            vm.declinePrompt,
            onDismiss: { vm.declinePrompt = nil },
            onConfirm: { offer in Task { await vm.decline(offer) } }
        )
        .offerRefusalAlert(vm.offerRefusal) { vm.offerRefusal = nil }
        .onReceive(vm.navigateToDetail) { orderId in
            path.append(OrderRoute.detail(orderId: orderId))
        }
        .onChange(of: deepLinkRoute) { route in
            guard let route else { return }
            path.append(route)
            deepLinkRoute = nil
        }
        .onAppear {
            guard let route = deepLinkRoute else { return }
            path.append(route)
            deepLinkRoute = nil
        }
    }

    private var contractRequest: Binding<WorkContractRequest?> {
        Binding(
            get: { vm.contractRequest },
            set: { if $0 == nil { vm.dismissContract() } }
        )
    }
}

/// The Orders tab: the header, the three-way switcher with the server's counts, and the chosen tab. The header is
/// pinned on the ground and takes a rule once the pane scrolls under it. A 30-second timeline re-reads the clock for
/// everything that depends on it — live flags, ended slots, the hero's line and its set-off gate, reservations that
/// lapse.
struct OrdersListView: View {
    @ObservedObject var vm: OrdersListViewModel
    var onOpenEarnings: () -> Void = {}
    @AccessibilityFocusState private var focusedSegment: OrdersTab?
    @State private var scrolled = false

    var body: some View {
        VStack(spacing: 0) {
            header
            TimelineView(.periodic(from: .now, by: 30)) { _ in
                paneContent
            }
        }
        .readableWidth()
        .background(CleansiaColors.background.ignoresSafeArea())
        .onPreferenceChange(OrdersScrolledKey.self) { scrolled = $0 }
        .navigationBarTitleDisplayMode(.inline)
        .toolbar(.hidden, for: .navigationBar)
        .background(InteractivePopGestureEnabler())
        .task { await vm.onAppear() }
        .onChange(of: vm.contractRequest == nil) { closed in
            if closed, vm.focusSwitcherAfterTake { focusedSegment = .available }
        }
    }

    private var header: some View {
        VStack(alignment: .leading, spacing: Spacing.xs) {
            Text(L10n.Orders.title)
                .cleansiaFont(CleansiaTypography.headlineMedium)
                .foregroundColor(CleansiaColors.onBackground)
                .accessibilityAddTraits(.isHeader)
            OrdersTabSwitcher(
                selected: vm.tab,
                counts: vm.badgeCount,
                focus: $focusedSegment
            ) { tab in
                Task { await vm.selectTab(tab) }
            }
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .padding(.horizontal, Spacing.m)
        .padding(.top, Spacing.xxs)
        .padding(.bottom, 7)
        .background(CleansiaColors.background)
        .overlay(alignment: .bottom) {
            Rectangle().fill(CleansiaColors.outlineVariant).frame(height: 1).opacity(scrolled ? 1 : 0)
        }
        .zIndex(1)
    }

    @ViewBuilder
    private var paneContent: some View {
        switch vm.tab {
        case .available: AvailablePane(vm: vm)
        case .active: ActivePane(vm: vm)
        case .history: HistoryPane(vm: vm, onOpenEarnings: onOpenEarnings)
        }
    }
}
