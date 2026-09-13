import CleansiaCore
import Foundation

/// The market directory as the register form sees it. The picker exists only where there is a choice
/// to make; with no directory the form sends no `countryId` and the server registers the cleaner with
/// the default market's operating company.
enum RegisterMarketState: Equatable {
    case loading
    case unavailable
    case resolved(selected: RegisterMarket, markets: [RegisterMarket])

    var selected: RegisterMarket? {
        if case let .resolved(selected, _) = self { return selected }
        return nil
    }

    var markets: [RegisterMarket] {
        if case let .resolved(_, markets) = self { return markets }
        return []
    }

    var countryId: String? {
        selected?.countryId
    }

    var offersChoice: Bool {
        markets.count >= 2
    }

    /// The directory's default market, else its first row; nil for an empty directory.
    static func preselect(_ markets: [RegisterMarket]) -> RegisterMarketState {
        guard let selected = markets.first(where: \.isDefault) ?? markets.first else { return .unavailable }
        return .resolved(selected: selected, markets: markets)
    }

    func selecting(countryId: String?) -> RegisterMarketState {
        guard case let .resolved(_, markets) = self,
              let chosen = markets.first(where: { $0.countryId == countryId })
        else { return self }
        return .resolved(selected: chosen, markets: markets)
    }
}

enum RegisterMarketLabel {
    /// "Česko · CZK" — the currency the cleaner will be paid in rides beside the country's name.
    static func row(_ market: RegisterMarket, languageTag: String = CoreL10n.languageTag) -> String {
        "\(market.localizedName(languageTag: languageTag)) · \(market.currencyCode)"
    }
}
