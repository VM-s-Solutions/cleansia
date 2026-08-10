import CleansiaCore
import CleansiaPartnerApi
import SwiftUI

struct PendingOffersView: View {
    @StateObject private var vm: PendingOffersViewModel
    @State private var pendingDecline: PendingOfferItem?
    private let onOpenOrder: (String) -> Void

    init(
        store: PendingOffersStore,
        client: PartnerOrderClient,
        staleness: OrdersStaleness,
        snackbar: SnackbarController,
        onOpenOrder: @escaping (String) -> Void
    ) {
        _vm = StateObject(
            wrappedValue: PendingOffersViewModel(
                store: store,
                client: client,
                staleness: staleness,
                snackbar: snackbar
            )
        )
        self.onOpenOrder = onOpenOrder
    }

    var body: some View {
        ZStack {
            PendingOffersContent(
                state: vm.state,
                inFlight: inFlight,
                onRetry: { Task { await vm.load() } },
                onConfirm: { offer in Task { await vm.confirm(offer) } },
                onDeclineRequested: { pendingDecline = $0 }
            )
            if let pendingDecline {
                OfferDeclineDialog(
                    onConfirm: {
                        let offer = pendingDecline
                        self.pendingDecline = nil
                        Task { await vm.decline(offer) }
                    },
                    onDismiss: { self.pendingDecline = nil }
                )
            }
            if let refusal {
                OfferRefusalDialog(refusal: refusal, onDismiss: vm.dismissRefusal)
            }
        }
        .navigationTitle(L10n.Offers.title)
        .navigationBarTitleDisplayMode(.inline)
        .toolbar(.hidden, for: .tabBar)
        .background(CleansiaColors.background.ignoresSafeArea())
        .task { await vm.load() }
        .onReceive(vm.confirmed) { onOpenOrder($0) }
    }

    private var refusal: OfferRefusal? {
        guard let message = vm.actionState.errorMessage, let attempt = vm.attempt else { return nil }
        return OfferRefusal(displayOrderNumber: attempt.displayOrderNumber, reason: message)
    }

    private var inFlight: OfferAttempt? {
        vm.actionState.isSubmitting ? vm.attempt : nil
    }
}

struct PendingOffersContent: View {
    let state: UiState<[PendingOfferItem]>
    var inFlight: OfferAttempt?
    var now: Date = .init()
    var onRetry: () -> Void = {}
    var onConfirm: (PendingOfferItem) -> Void = { _ in }
    var onDeclineRequested: (PendingOfferItem) -> Void = { _ in }

    var body: some View {
        switch state {
        case .loading:
            ProgressView()
                .frame(maxWidth: .infinity, maxHeight: .infinity)
        case let .error(error):
            OffersErrorView(error: error, onRetry: onRetry)
        case let .loaded(offers):
            if offers.isEmpty {
                MascotEmptyState(image: Mascot.leaning.image, text: L10n.Offers.empty, verticallyCentered: true)
            } else {
                offerList(offers)
            }
        }
    }

    private func offerList(_ offers: [PendingOfferItem]) -> some View {
        ScrollView {
            VStack(alignment: .leading, spacing: Spacing.m) {
                Text(L10n.Offers.subtitle)
                    .font(CleansiaTypography.bodyMedium)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
                    .fixedSize(horizontal: false, vertical: true)
                ForEach(offers, id: \.id) { offer in
                    PendingOfferCard(
                        offer: offer,
                        now: now,
                        inFlightAction: inFlight?.orderId == offer.id ? inFlight?.action : nil,
                        actionsLocked: inFlight != nil,
                        onConfirm: { onConfirm(offer) },
                        onDecline: { onDeclineRequested(offer) }
                    )
                }
            }
            .padding(Spacing.m)
        }
    }
}

private struct OffersErrorView: View {
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

private struct PendingOfferCard: View {
    @Environment(\.locale) private var locale
    let offer: PendingOfferItem
    let now: Date
    let inFlightAction: OfferAction?
    let actionsLocked: Bool
    let onConfirm: () -> Void
    let onDecline: () -> Void

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.s) {
            ReservedForYouRow(respondByUtc: offer.respondByUtc, now: now)

            HStack(alignment: .top) {
                VStack(alignment: .leading, spacing: 2) {
                    Text(OrdersFormat.relativeDateTime(offer.cleaningDateTime, locale: locale))
                        .font(CleansiaTypography.titleMedium)
                        .foregroundColor(CleansiaColors.onSurface)
                    if let number = offer.displayOrderNumber, !number.isBlank {
                        Text(number)
                            .font(CleansiaTypography.labelSmall)
                            .foregroundColor(CleansiaColors.onSurfaceVariant)
                    }
                }
                Spacer()
                Text(OrdersFormat.money(offer.totalPrice ?? 0, symbol: offer.currencyCode))
                    .font(CleansiaTypography.titleLarge)
                    .foregroundColor(CleansiaColors.primary)
            }

            // City and a truncated postcode — the pre-acceptance ceiling for every cleaner-facing
            // surface. The server sends nothing finer and the screen asks for nothing finer.
            if let address = offer.customerAddressApproximate, !address.isBlank {
                Label(address, systemImage: "mappin.and.ellipse")
                    .font(CleansiaTypography.bodyMedium)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
            }

            if let scope = scopeLine {
                Text(scope)
                    .font(CleansiaTypography.labelMedium)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
            }

            CleansiaPrimaryButton(
                L10n.Offers.confirm,
                loading: inFlightAction == .confirm,
                enabled: !actionsLocked,
                action: onConfirm
            )
            HStack {
                Spacer()
                CleansiaTextLink(L10n.Offers.decline, action: onDecline)
                    .disabled(actionsLocked)
                Spacer()
            }
        }
        .cardPadding()
    }

    private var scopeLine: String? {
        let rooms = offer.rooms ?? 0
        let baths = offer.bathrooms ?? 0
        let parts = [
            rooms > 0 ? OrdersFormat.rooms(rooms) : nil,
            baths > 0 ? OrdersFormat.baths(baths) : nil
        ].compactMap { $0 }
        return parts.isEmpty ? nil : parts.joined(separator: " · ")
    }
}

#if DEBUG
    extension PendingOfferItem {
        static var preview: PendingOfferItem {
            PendingOfferItem(
                id: "1",
                displayOrderNumber: "CL-2026-0042",
                cleaningDateTime: Date(timeIntervalSince1970: 1_786_000_000),
                estimatedTime: 180,
                respondByUtc: Date(timeIntervalSince1970: 1_785_950_000),
                customerAddressApproximate: "Praha 4 · 14000",
                rooms: 3,
                bathrooms: 1,
                totalPrice: 1850,
                currencyCode: "CZK"
            )
        }
    }

    struct PendingOffersContent_Previews: PreviewProvider {
        static var previews: some View {
            Group {
                PendingOffersContent(
                    state: .loaded([.preview]),
                    now: Date(timeIntervalSince1970: 1_785_900_000)
                )
                .previewDisplayName("Loaded")

                PendingOffersContent(state: .loaded([]))
                    .previewDisplayName("Empty")

                PendingOffersContent(state: .error(ApiError(httpStatus: 500)))
                    .previewDisplayName("Error")

                PendingOffersContent(state: .loading)
                    .previewDisplayName("Loading")
            }
            .background(CleansiaColors.background)
        }
    }
#endif
