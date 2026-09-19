import CleansiaCore
import CleansiaPartnerApi
import Foundation

/// A country an operating company serves, as the partner host lists it (`Market/GetOverview`). The
/// register form offers these and nothing else: `Country/GetServiced` still lists a serviced country
/// nobody operates, and a cleaner registered against one would be refused at the first write.
struct RegisterMarket: Equatable, Identifiable {
    let countryId: String
    let isoCode: String
    let name: String
    let translations: [String: String]
    let currencyCode: String
    let isDefault: Bool

    var id: String {
        countryId
    }

    /// The country's name in the language the cleaner chose, else the seed's English name — the
    /// resolution `CountryListItem.localizedName` applies, keyed on `CoreL10n.languageTag` for the
    /// same reason: the in-app language switch never reaches the process locale.
    func localizedName(languageTag: String = CoreL10n.languageTag) -> String {
        let translated = UserDefaultsAppSettingsStore.bareLanguageCode(languageTag)
            .flatMap { translations[$0] }
            .flatMap { $0.isBlank ? nil : $0 }
        return translated ?? name
    }
}

protocol PartnerMarketClient: Sendable {
    func getMarkets() async -> ApiResult<[RegisterMarket]>
}

struct LivePartnerMarketClient: PartnerMarketClient {
    func getMarkets() async -> ApiResult<[RegisterMarket]> {
        await apiResult(mapError: ApiError.fromGenerated) {
            try await PartnerMarketAPI.marketGetOverview().map { try RegisterMarket($0) }
        }
    }
}

/// Refuse the list: every row is a choice the form sends as `countryId`, and a row that cannot be
/// chosen or named is a directory the picker must not render.
extension RegisterMarket {
    init(_ dto: MarketListItem) throws {
        countryId = try dto.countryId.requireNonBlank("countryId")
        isoCode = try dto.isoCode.requireNonBlank("isoCode")
        name = try dto.name.requireNonBlank("name")
        translations = dto.translations?.compactMapValues(\.name) ?? [:]
        currencyCode = try dto.currencyCode.requireNonBlank("currencyCode")
        isDefault = try dto.isDefault.require("isDefault")
    }
}
