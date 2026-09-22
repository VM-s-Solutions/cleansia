import CleansiaCore
import Combine
import Foundation

enum MarketState: Equatable {
    case loading
    /// The directory could not be read (or lists nothing): readers send no `countryId`, labels come
    /// from each payload, the chip and the picker are absent, nothing is persisted.
    case unavailable
    case resolved(selected: Market, markets: [Market])

    var selected: Market? {
        if case let .resolved(selected, _) = self { return selected }
        return nil
    }

    var markets: [Market] {
        if case let .resolved(_, markets) = self { return markets }
        return []
    }

    var countryId: String? {
        selected?.countryId
    }

    /// The chip and the picker exist only where there is a choice to make.
    var offersChoice: Bool {
        markets.count >= 2
    }

    /// The default market's currency is the platform default currency; nil when no row is flagged.
    var defaultCurrencyCode: String? {
        markets.first(where: \.isDefault)?.currencyCode
    }

    /// The insurance ceiling authored for a country, in that country's currency — nil when the
    /// country is not a listed market or no ceiling is authored, which renders the no-figure copy.
    func insurance(forCountryId countryId: String?) -> MarketMoney? {
        guard let countryId,
              let market = markets.first(where: { $0.countryId == countryId }),
              let amount = market.insuranceCoverageAmount
        else { return nil }
        return MarketMoney(amount: amount, currencyCode: market.currencyCode)
    }
}

enum MarketResolution {
    /// The stored code if it names a listed market, else the row the directory flags as default,
    /// else the first row. A stored code the list does not know is dropped, never kept.
    static func resolve(stored: String?, in markets: [Market]) -> Market? {
        if let stored, let match = markets.first(where: { $0.isoCode == stored }) {
            return match
        }
        return markets.first(where: \.isDefault) ?? markets.first
    }
}

/// The market a customer browses in: read at launch and on every Home entry, remembered on the
/// device, and the one seam every pre-address reader resolves its `countryId` through.
@MainActor
final class MarketStore: ObservableObject {
    @Published private(set) var state: MarketState = .loading

    let staleness: Staleness

    private let client: MarketClient
    private let preference: MarketPreferenceStore
    private var inFlight: Task<Void, Never>?

    init(client: MarketClient, preference: MarketPreferenceStore, staleness: Staleness = Staleness()) {
        self.client = client
        self.preference = preference
        self.staleness = staleness
    }

    var selected: Market? {
        state.selected
    }

    var markets: [Market] {
        state.markets
    }

    var statePublisher: AnyPublisher<MarketState, Never> {
        $state.eraseToAnyPublisher()
    }

    /// Single-flight: launch and the shell prefetch race for the same list, and the second caller
    /// joins the first. A failed read keeps the last list; before there is one it is the no-market
    /// state, retried on the next entry.
    func refresh() async {
        if let inFlight {
            await inFlight.value
            return
        }
        let load = Task { await fetch() }
        inFlight = load
        await load.value
        inFlight = nil
    }

    func refreshIfStale() async {
        guard staleness.isStale else { return }
        await refresh()
    }

    func select(isoCode: String) {
        guard case let .resolved(_, markets) = state,
              let market = markets.first(where: { $0.isoCode == isoCode })
        else { return }
        preference.setMarket(isoCode: market.isoCode)
        state = .resolved(selected: market, markets: markets)
    }

    private func fetch() async {
        switch await client.getMarkets() {
        case let .success(markets):
            guard let selected = MarketResolution.resolve(stored: preference.marketIsoCode, in: markets) else {
                state = .unavailable
                return
            }
            preference.setMarket(isoCode: selected.isoCode)
            state = .resolved(selected: selected, markets: markets)
            staleness.markFresh()
        case .failure:
            if case .loading = state {
                state = .unavailable
            }
        }
    }
}
