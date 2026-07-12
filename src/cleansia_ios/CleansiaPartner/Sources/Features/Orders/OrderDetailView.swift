import CleansiaCore
import SwiftUI

struct OrderDetailView: View {
    @StateObject private var vm: OrderDetailViewModel
    @StateObject private var checklistVM: CleaningChecklistViewModel
    @StateObject private var notesVM: OrderNotesViewModel
    @StateObject private var photosVM: OrderPhotosViewModel
    @State private var snapAnchor: SnapAnchor = .peek
    private let mapProvider: MapProvider

    init(
        orderId: String,
        client: PartnerOrderClient,
        staleness: OrdersStaleness,
        checklistStore: CleaningChecklistStore,
        snackbar: SnackbarController,
        mapProvider: MapProvider
    ) {
        _vm = StateObject(
            wrappedValue: OrderDetailViewModel(
                orderId: orderId,
                client: client,
                staleness: staleness,
                snackbar: snackbar
            )
        )
        _checklistVM = StateObject(
            wrappedValue: CleaningChecklistViewModel(orderId: orderId, store: checklistStore)
        )
        _notesVM = StateObject(
            wrappedValue: OrderNotesViewModel(orderId: orderId, client: client, snackbar: snackbar)
        )
        _photosVM = StateObject(
            wrappedValue: OrderPhotosViewModel(orderId: orderId, client: client, snackbar: snackbar)
        )
        self.mapProvider = mapProvider
    }

    var body: some View {
        content
            .navigationBarTitleDisplayMode(.inline)
            .toolbar(.hidden, for: .tabBar)
            .task { await vm.load() }
            .task { await photosVM.load() }
            .onReceive(notesVM.mutated) { Task { await vm.load() } }
            .onReceive(photosVM.mutated) { Task { await vm.load() } }
    }

    @ViewBuilder
    private var content: some View {
        switch vm.state {
        case .loading:
            ProgressView()
                .frame(maxWidth: .infinity, maxHeight: .infinity)
                .background(CleansiaColors.background.ignoresSafeArea())
        case let .error(error):
            OrderDetailErrorView(error: error) { Task { await vm.load() } }
        case let .loaded(order):
            loadedShell(order)
        }
    }

    private func loadedShell(_ order: OrderDetail) -> some View {
        SnapSheet(anchor: $snapAnchor) {
            mapBackdrop(order)
        } content: {
            OrderDetailContent(
                order: order,
                primaryAction: vm.primaryAction,
                inFlightAction: vm.inFlightAction,
                onConfirm: { action in Task { await vm.dispatch(action) } },
                checklistVM: checklistVM,
                notesVM: notesVM,
                photosVM: photosVM
            )
        }
    }

    @ViewBuilder
    private func mapBackdrop(_ order: OrderDetail) -> some View {
        // The View never imports MapKit — the provider encapsulates it. On
        // Cancelled (no coords / never-visited) a plain surface stands in for
        // the map.
        if order.canShowMap, let coordinate = order.coordinate {
            mapProvider.fullBleedMap(coordinate: coordinate)
        } else {
            CleansiaColors.primaryContainer
        }
    }
}

private struct OrderDetailErrorView: View {
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
        .background(CleansiaColors.background.ignoresSafeArea())
    }
}

#if DEBUG
    struct OrderDetailView_Previews: PreviewProvider {
        static var previews: some View {
            Group {
                stateView(.loading).previewDisplayName("Loading")
                stateView(.error(ApiError(httpStatus: 500))).previewDisplayName("Error")
                OrderDetailContent(
                    order: .preview,
                    checklistVM: .preview,
                    notesVM: .preview,
                    photosVM: .preview
                )
                .previewDisplayName("Loaded content")
            }
        }

        @ViewBuilder
        private static func stateView(_ state: UiState<OrderDetail>) -> some View {
            switch state {
            case .loading:
                ProgressView().frame(maxWidth: .infinity, maxHeight: .infinity)
            case let .error(error):
                OrderDetailErrorView(error: error, onRetry: {})
            case .loaded:
                EmptyView()
            }
        }
    }
#endif
