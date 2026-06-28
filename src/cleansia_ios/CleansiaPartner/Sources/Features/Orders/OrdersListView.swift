import CleansiaCore
import CleansiaPartnerApi
import SwiftUI

struct OrdersRootView: View {
    @StateObject private var vm: OrdersListViewModel
    @State private var path: [OrderRoute] = []
    @Binding private var deepLinkOrderId: String?
    private let client: PartnerOrderClient
    private let staleness: OrdersStaleness
    private let checklistStore: CleaningChecklistStore
    private let snackbar: SnackbarController
    private let mapProvider: MapProvider

    init(
        client: PartnerOrderClient,
        staleness: OrdersStaleness,
        checklistStore: CleaningChecklistStore,
        snackbar: SnackbarController,
        mapProvider: MapProvider,
        deepLinkOrderId: Binding<String?> = .constant(nil)
    ) {
        _vm = StateObject(
            wrappedValue: OrdersListViewModel(client: client, staleness: staleness, snackbar: snackbar)
        )
        _deepLinkOrderId = deepLinkOrderId
        self.client = client
        self.staleness = staleness
        self.checklistStore = checklistStore
        self.snackbar = snackbar
        self.mapProvider = mapProvider
    }

    var body: some View {
        NavigationStack(path: $path) {
            OrdersListView(vm: vm)
                .navigationDestination(for: OrderRoute.self) { route in
                    switch route {
                    case let .detail(orderId):
                        OrderDetailView(
                            orderId: orderId,
                            client: client,
                            staleness: staleness,
                            checklistStore: checklistStore,
                            snackbar: snackbar,
                            mapProvider: mapProvider
                        )
                    }
                }
        }
        .onReceive(vm.navigateToDetail) { orderId in
            path.append(.detail(orderId: orderId))
        }
        .onChange(of: deepLinkOrderId) { orderId in
            guard orderId != nil else { return }
            path = PushTapRouting.appendingDeepLink(orderId, to: path)
            deepLinkOrderId = nil
        }
        .onAppear {
            guard deepLinkOrderId != nil else { return }
            path = PushTapRouting.appendingDeepLink(deepLinkOrderId, to: path)
            deepLinkOrderId = nil
        }
    }
}

struct OrdersListView: View {
    @ObservedObject var vm: OrdersListViewModel

    var body: some View {
        VStack(spacing: 0) {
            header
            tabPicker
            if let inProgress = vm.inProgressOrder {
                InProgressBanner(order: inProgress) { open(inProgress) }
                    .padding(.horizontal, Spacing.m)
                    .padding(.top, Spacing.xs)
            }
            paneContent
        }
        .background(CleansiaColors.background.ignoresSafeArea())
        .navigationBarTitleDisplayMode(.inline)
        .toolbar(.hidden, for: .navigationBar)
        .task { await vm.onAppear() }
    }

    private var header: some View {
        HStack {
            Text(L10n.Orders.title)
                .font(CleansiaTypography.headlineMedium)
                .foregroundColor(CleansiaColors.onBackground)
            Spacer()
        }
        .padding(.horizontal, Spacing.m)
        .padding(.vertical, Spacing.m)
    }

    private var tabPicker: some View {
        Picker("", selection: tabBinding) {
            Text(L10n.Orders.available).tag(OrdersTab.available)
            Text(L10n.Orders.active).tag(OrdersTab.active)
            Text(L10n.Orders.history).tag(OrdersTab.history)
        }
        .pickerStyle(.segmented)
        .padding(.horizontal, Spacing.m)
    }

    private var tabBinding: Binding<OrdersTab> {
        Binding(
            get: { vm.tab },
            set: { newTab in Task { await vm.selectTab(newTab) } }
        )
    }

    @ViewBuilder
    private var paneContent: some View {
        switch vm.currentState {
        case .loading:
            ProgressView()
                .frame(maxWidth: .infinity, maxHeight: .infinity)
        case let .error(error):
            OrdersErrorView(error: error) { Task { await vm.userRefresh() } }
        case .loaded:
            OrdersPaneView(vm: vm, onOpen: open)
                .refreshable { await vm.userRefresh() }
        }
    }

    private func open(_ order: OrderListItem) {
        guard let id = order.id else { return }
        vm.openDetail(id)
    }
}

private struct OrdersErrorView: View {
    let error: ApiError
    let onRetry: () -> Void

    var body: some View {
        VStack(spacing: Spacing.m) {
            Image(systemName: "exclamationmark.triangle")
                .font(.system(size: 40))
                .foregroundColor(CleansiaColors.error)
            Text(ApiErrorLocalizer().message(for: error))
                .font(CleansiaTypography.bodyMedium)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
                .multilineTextAlignment(.center)
            CleansiaOutlinedButton(L10n.retry, size: .medium, action: onRetry)
                .fixedSize()
        }
        .padding(Spacing.xl)
        .frame(maxWidth: .infinity, maxHeight: .infinity)
    }
}

#if DEBUG
    struct OrdersListView_Previews: PreviewProvider {
        static var previews: some View {
            Group {
                stateView(.loading).previewDisplayName("Loading")
                stateView(.error(ApiError(httpStatus: 500))).previewDisplayName("Error")
            }
        }

        @ViewBuilder
        private static func stateView(_ state: UiState<[OrderListItem]>) -> some View {
            switch state {
            case .loading:
                ProgressView().frame(maxWidth: .infinity, maxHeight: .infinity)
            case let .error(error):
                OrdersErrorView(error: error, onRetry: {})
            case .loaded:
                EmptyView()
            }
        }
    }
#endif
