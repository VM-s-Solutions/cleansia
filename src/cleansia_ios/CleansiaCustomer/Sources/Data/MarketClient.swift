import CleansiaCore
import CleansiaCustomerApi
import Foundation

/// A country a customer may browse in, with the currency its prices are stated in and the two copy
/// figures authored for it (`Market/GetOverview`).
struct Market: Equatable, Identifiable {
    let countryId: String
    /// ISO 3166 alpha-3 — the value the preference persists and compares against, never rendered.
    let isoCode: String
    let isoAlpha2: String
    let name: String
    let translations: [String: CatalogTranslation]
    let currencyCode: String
    let isDefault: Bool
    let noShowCredit: Double?
    let insuranceCoverageAmount: Double?

    var id: String {
        countryId
    }

    /// "CZ · CZK" — the country code is the glyph and the currency code the unit; no flag.
    var chipLabel: String {
        "\(isoAlpha2) · \(currencyCode)"
    }

    func localizedName(for locale: Locale) -> String {
        CatalogLocalization.name(
            translations: translations,
            fallback: name,
            languageCode: CatalogLocalization.languageCode(for: locale)
        )
    }
}

/// A figure with the unit it is a number in.
struct MarketMoney: Equatable {
    let amount: Double
    let currencyCode: String
}

protocol MarketClient: Sendable {
    func getMarkets() async -> ApiResult<[Market]>
}

struct LiveMarketClient: MarketClient {
    func getMarkets() async -> ApiResult<[Market]> {
        await apiResult(mapError: ApiError.fromGenerated) {
            try await CustomerMarketAPI.marketGetOverview().map { try Market($0) }
        }
    }
}

/// **Refuse the list.** The rows are the choices a customer picks a market from, and every reader
/// sends the picked row's `countryId` and prints its codes: a row with none of them is a market that
/// cannot be chosen, priced or named, and a directory missing one market is a different directory.
/// A refused list is the no-market state, which is exactly what the app renders when the directory
/// cannot be read at all.
extension Market {
    init(_ dto: MarketListItem) throws {
        countryId = try dto.countryId.requireNonBlank("countryId")
        isoCode = try dto.isoCode.requireNonBlank("isoCode")
        isoAlpha2 = try dto.isoAlpha2.requireNonBlank("isoAlpha2")
        name = try dto.name.requireNonBlank("name")
        translations = dto.translations?.mapValues {
            CatalogTranslation(name: $0.name ?? "", description: $0.description)
        } ?? [:]
        currencyCode = try dto.currencyCode.requireNonBlank("currencyCode")
        isDefault = try dto.isDefault.require("isDefault")
        noShowCredit = dto.noShowCredit
        insuranceCoverageAmount = dto.insuranceCoverageAmount
    }
}
