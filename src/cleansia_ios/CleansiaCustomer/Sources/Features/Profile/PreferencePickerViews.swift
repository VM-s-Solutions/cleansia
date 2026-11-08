import CleansiaCore
import SwiftUI

struct LanguagePickerView: View {
    @ObservedObject var preferences: CustomerPreferencesModel
    let onSelected: () -> Void

    var body: some View {
        PreferencePickerList(
            title: L10n.Preferences.language,
            options: [PreferenceOption(
                id: CustomerPreferencesLabels.systemLanguageId,
                label: L10n.Preferences.languageSystem
            )]
                + LanguageLabels.languages.map { PreferenceOption(id: $0.tag, label: $0.label) },
            selectedId: preferences.isFollowingSystemLanguage
                ? CustomerPreferencesLabels.systemLanguageId
                : preferences.languageTag,
            onSelect: { id in
                if id == CustomerPreferencesLabels.systemLanguageId {
                    preferences.setSystemLanguage()
                } else {
                    preferences.setLanguage(id)
                }
                onSelected()
            }
        )
    }
}

/// The Language picker's twin over the same list: one row per listed market, "Česko · CZK", the
/// chosen one checked. Selecting writes the preference and every market reader re-dispatches.
struct MarketPickerView: View {
    @ObservedObject var market: MarketStore
    @Environment(\.locale) private var locale
    let onSelected: () -> Void

    var body: some View {
        PreferencePickerList(
            title: L10n.Preferences.market,
            options: market.markets.map {
                PreferenceOption(id: $0.isoCode, label: MarketPickerLabel.row($0, locale: locale))
            },
            selectedId: market.selected?.isoCode ?? "",
            onSelect: { isoCode in
                market.select(isoCode: isoCode)
                onSelected()
            }
        )
    }
}

enum MarketPickerLabel {
    static func row(_ market: Market, locale: Locale) -> String {
        "\(market.localizedName(for: locale)) · \(market.currencyCode)"
    }
}

struct AppearancePickerView: View {
    @ObservedObject var preferences: CustomerPreferencesModel
    let onSelected: () -> Void

    var body: some View {
        PreferencePickerList(
            title: L10n.Preferences.appearance,
            options: CustomerPreferencesLabels.themes.map {
                PreferenceOption(id: $0.rawValue, label: CustomerPreferencesLabels.themeLabel($0))
            },
            selectedId: preferences.theme.rawValue,
            onSelect: { rawValue in
                if let theme = Theme(rawValue: rawValue) {
                    preferences.setTheme(theme)
                }
                onSelected()
            }
        )
    }
}
