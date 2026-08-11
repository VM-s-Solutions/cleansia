import CleansiaCore
import CleansiaPartnerApi
import Combine
import SwiftUI

enum PendingOffersCardUiState: Equatable {
    case hidden
    case visible(count: Int, soonestRespondBy: Date?)
}

/// A reservation is rare and time-limited, so it gets no tab of its own: a permanent Offers tab would
/// charge every cleaner an empty state every day for something most of them will see a handful of
/// times a year. It rides the dashboard instead — the one screen everybody opens — and disappears
/// entirely when nothing is waiting, the same shape the one-time radius prompt uses.
@MainActor
final class PendingOffersCardViewModel: ViewModel {
    @Published private(set) var state: PendingOffersCardUiState = .hidden

    private let store: PendingOffersStore
    private var cancellables: Set<AnyCancellable> = []

    init(store: PendingOffersStore) {
        self.store = store
        super.init()
        store.$offers
            .sink { [weak self] offers in self?.resolveState(offers) }
            .store(in: &cancellables)
    }

    func load() async {
        guard store.isStale else { return }
        _ = await store.refresh()
    }

    private func resolveState(_ offers: [PendingOfferItem]) {
        guard !offers.isEmpty else {
            state = .hidden
            return
        }
        state = .visible(
            count: offers.count,
            soonestRespondBy: PendingOfferPresentation.soonestOffer(offers)?.respondByUtc
        )
    }
}

struct PendingOffersCard: View {
    @StateObject private var vm: PendingOffersCardViewModel
    private let onOpenOffers: () -> Void

    init(store: PendingOffersStore, onOpenOffers: @escaping () -> Void) {
        _vm = StateObject(wrappedValue: PendingOffersCardViewModel(store: store))
        self.onOpenOffers = onOpenOffers
    }

    var body: some View {
        Group {
            if case let .visible(count, soonestRespondBy) = vm.state {
                PendingOffersCardContent(
                    count: count,
                    soonestRespondBy: soonestRespondBy,
                    onOpenOffers: onOpenOffers
                )
            }
        }
        .task { await vm.load() }
    }
}

struct PendingOffersCardContent: View {
    let count: Int
    let soonestRespondBy: Date?
    let onOpenOffers: () -> Void
    var now: Date = .init()

    var body: some View {
        Button(action: onOpenOffers) {
            VStack(alignment: .leading, spacing: Spacing.xs) {
                HStack {
                    Text(L10n.Offers.cardTitle)
                        .font(CleansiaTypography.titleMedium)
                        .foregroundColor(CleansiaColors.onSurface)
                    Spacer()
                    if count > 1 {
                        Text(L10n.Offers.cardMore(count - 1))
                            .font(CleansiaTypography.labelMedium)
                            .foregroundColor(CleansiaColors.onSurfaceVariant)
                    }
                }
                HStack(spacing: Spacing.xs) {
                    ReservedForYouRow(respondByUtc: soonestRespondBy, now: now)
                    Text(L10n.Offers.cardCta)
                        .font(CleansiaTypography.labelLarge)
                        .foregroundColor(CleansiaColors.primary)
                    Image(systemName: "arrow.right")
                        .font(.system(size: 13))
                        .foregroundColor(CleansiaColors.primary)
                }
            }
            .padding(Spacing.m)
            .frame(maxWidth: .infinity, alignment: .leading)
            .background(CleansiaColors.primary.opacity(0.08))
            .clipShape(RoundedRectangle(cornerRadius: 18))
            .padding(.horizontal, Spacing.m)
        }
        .buttonStyle(.plain)
    }
}

#if DEBUG
    struct PendingOffersCardContent_Previews: PreviewProvider {
        static var previews: some View {
            PendingOffersCardContent(
                count: 2,
                soonestRespondBy: Date(timeIntervalSince1970: 1_785_950_000),
                onOpenOffers: {},
                now: Date(timeIntervalSince1970: 1_785_900_000)
            )
            .padding(.vertical, Spacing.m)
            .background(CleansiaColors.background)
            .previewLayout(.sizeThatFits)
        }
    }
#endif
