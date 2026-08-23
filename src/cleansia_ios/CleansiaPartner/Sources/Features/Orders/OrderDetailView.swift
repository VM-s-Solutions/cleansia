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
        mapProvider: MapProvider,
        pendingOffers: PendingOffersStore
    ) {
        _vm = StateObject(
            wrappedValue: OrderDetailViewModel(
                orderId: orderId,
                client: client,
                staleness: staleness,
                snackbar: snackbar,
                pendingOffers: pendingOffers
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
            .task(id: vm.canReadPhotos) { await photosVM.load(isAuthorized: vm.canReadPhotos) }
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
        } ornament: {
            MascotPuck(OrderDetailMascotArt.art(for: order.status))
        } content: {
            OrderDetailContent(
                order: order,
                primaryAction: vm.primaryAction,
                inFlightAction: vm.inFlightAction,
                preferredOffer: vm.preferredOffer,
                refusal: vm.refusal,
                onConfirm: { action in Task { await vm.dispatch(action) } },
                onDeclineOffer: { Task { await vm.declinePreferredOffer() } },
                onDismissRefusal: vm.dismissActionError,
                checklistVM: checklistVM,
                notesVM: notesVM,
                photosVM: photosVM
            )
        }
    }

    @ViewBuilder
    private func mapBackdrop(_ order: OrderDetail) -> some View {
        // The View never imports MapKit — the provider encapsulates it.
        if let coordinate = order.mapCoordinate {
            mapProvider.fullBleedMap(coordinate: coordinate)
        } else {
            ApproximateAreaBackdrop(zone: order.location.line, anchor: snapAnchor)
        }
    }
}

/// Stands in for the map when no coordinate was released to this caller. The withheld point is not a
/// failure — the server nulls the address for a cleaner who has not taken the job — but an empty pane
/// says "the map broke", so this names the coarse zone that *did* arrive and why the pin is missing.
private struct ApproximateAreaBackdrop: View {
    @Environment(\.locale) private var locale
    let zone: String?
    /// The sheet's CURRENT position, so the legend centres in the strip that is actually visible.
    let anchor: SnapAnchor

    var body: some View {
        // Centred in the strip the sheet leaves uncovered — measured from where the sheet ACTUALLY is,
        // not from where it opens. This was pinned to `SnapAnchor.peek`, so dragging the sheet down (or
        // tapping the handle, which toggles to `.mapFocus`) grew the visible area from a quarter of the
        // screen to seventy percent while the legend stayed centred in the old quarter — pinned to the
        // top of a pane with a lot of empty blue beneath it.
        //
        // It animates with the sheet because `anchor` is the same state the sheet is driven by.
        GeometryReader { geometry in
            ZStack(alignment: .top) {
                CleansiaColors.primaryContainer
                legend
                    .padding(.horizontal, Spacing.xl)
                    .frame(
                        width: geometry.size.width,
                        height: geometry.size.height * (1 - anchor.coveredFraction)
                    )
            }
        }
        .animation(.easeInOut(duration: 0.2), value: anchor)
        .id(locale.identifier)
    }

    private var legend: some View {
        VStack(spacing: Spacing.xs) {
            Image(systemName: "mappin.and.ellipse")
                .font(.system(size: 32))
                .foregroundColor(CleansiaColors.onPrimaryContainer)
            if let zone, !zone.isBlank {
                Text(zone)
                    .font(CleansiaTypography.titleMedium)
                    .foregroundColor(CleansiaColors.onPrimaryContainer)
            }
            Text(L10n.Orders.mapApproximateArea)
                .font(CleansiaTypography.bodyMedium)
                .foregroundColor(CleansiaColors.onPrimaryContainer.opacity(0.8))
                .multilineTextAlignment(.center)
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
