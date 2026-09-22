import CleansiaCore
import Combine
import Foundation
@testable import CleansiaCustomer

final class FakeMarketClient: MarketClient, @unchecked Sendable {
    var result: ApiResult<[Market]>
    private(set) var callCount = 0
    /// A hook the single-flight test uses to hold a read open.
    var gate: (() async -> Void)?

    init(result: ApiResult<[Market]> = .success(MarketFixtures.two)) {
        self.result = result
    }

    func getMarkets() async -> ApiResult<[Market]> {
        callCount += 1
        await gate?()
        return result
    }
}

final class FakeMarketPreferenceStore: MarketPreferenceStore {
    var marketIsoCode: String?
    private(set) var writes: [String?] = []

    init(marketIsoCode: String? = nil) {
        self.marketIsoCode = marketIsoCode
    }

    func setMarket(isoCode: String) {
        marketIsoCode = isoCode
        writes.append(isoCode)
    }

    func clearMarket() {
        marketIsoCode = nil
        writes.append(nil)
    }
}

enum MarketFixtures {
    static func market(
        countryId: String,
        isoCode: String,
        isoAlpha2: String,
        name: String,
        currencyCode: String,
        isDefault: Bool = false,
        noShowCredit: Double? = nil,
        insuranceCoverageAmount: Double? = nil,
        translations: [String: CatalogTranslation] = [:]
    ) -> Market {
        Market(
            countryId: countryId,
            isoCode: isoCode,
            isoAlpha2: isoAlpha2,
            name: name,
            translations: translations,
            currencyCode: currencyCode,
            isDefault: isDefault,
            noShowCredit: noShowCredit,
            insuranceCoverageAmount: insuranceCoverageAmount
        )
    }

    static let czechia = market(
        countryId: "cze",
        isoCode: "CZE",
        isoAlpha2: "CZ",
        name: "Czechia",
        currencyCode: "CZK",
        isDefault: true,
        noShowCredit: 250,
        insuranceCoverageAmount: 1_000_000,
        translations: ["cs": CatalogTranslation(name: "Česko", description: nil)]
    )

    static let slovakia = market(
        countryId: "svk",
        isoCode: "SVK",
        isoAlpha2: "SK",
        name: "Slovakia",
        currencyCode: "EUR",
        translations: ["cs": CatalogTranslation(name: "Slovensko", description: nil)]
    )

    static let one = [czechia]
    static let two = [czechia, slovakia]

    @MainActor
    static func store(
        _ result: ApiResult<[Market]> = .success(two),
        stored: String? = nil
    ) -> (MarketStore, FakeMarketClient, FakeMarketPreferenceStore) {
        let client = FakeMarketClient(result: result)
        let preference = FakeMarketPreferenceStore(marketIsoCode: stored)
        return (MarketStore(client: client, preference: preference), client, preference)
    }

    /// A store already resolved onto `markets` with `selected` — the shape every reader test starts from.
    @MainActor
    static func resolved(_ markets: [Market] = two, selected: Market = czechia) async -> MarketStore {
        let (store, _, _) = store(.success(markets), stored: selected.isoCode)
        await store.refresh()
        return store
    }

    @MainActor
    static func unavailable() async -> MarketStore {
        let (store, _, _) = store(.failure(ApiError(httpStatus: 500)))
        await store.refresh()
        return store
    }
}
