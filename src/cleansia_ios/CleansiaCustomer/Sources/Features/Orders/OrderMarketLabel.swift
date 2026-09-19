import CleansiaCore
import Foundation

/// "Slovakia · EUR" — the market an order was booked in, from the order's own `countryId` and currency.
/// The browsing selection is never consulted: a past booking keeps its market when the customer
/// switches to another one, and a country the directory no longer lists reads as unavailable rather
/// than as whatever is selected now.
enum OrderMarketLabel {
    static func marketName(countryId: String?, markets: MarketState, locale: Locale) -> String? {
        guard let countryId, let market = markets.markets.first(where: { $0.countryId == countryId }) else {
            return nil
        }
        let translated = market.translations[CatalogLocalization.languageCode(for: locale)]?.name
        if let translated, !translated.isBlank { return translated }
        return market.name.isBlank ? nil : market.name
    }

    static func text(countryId: String?, currencyCode: String?, markets: MarketState, locale: Locale) -> String {
        let country = marketName(countryId: countryId, markets: markets, locale: locale) ?? L10n.Orders.marketUnknown
        let currency = currencyCode.flatMap { $0.isBlank ? nil : $0 } ?? "—"
        return L10n.Orders.marketLabel(country, currency)
    }
}
