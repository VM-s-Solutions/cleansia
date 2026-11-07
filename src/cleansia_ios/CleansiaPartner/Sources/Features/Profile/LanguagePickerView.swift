import CleansiaCore
import SwiftUI

struct LanguagePickerView: View {
    @ObservedObject var preferences: PreferencesModel
    let onSelected: () -> Void

    var body: some View {
        PreferencePickerList(
            title: L10n.Profile.language,
            // "System" (follow device locale) first, then the explicit languages
            // (Gate-DP parity with the Android LanguagePickerScreen).
            options: [PreferenceOption(id: PreferencesLabels.systemLanguageId, label: L10n.Profile.languageSystem)]
                + LanguageLabels.languages.map { PreferenceOption(id: $0.tag, label: $0.label) },
            // System selected when no explicit tag is persisted.
            selectedId: preferences.isFollowingSystemLanguage
                ? PreferencesLabels.systemLanguageId
                : preferences.languageTag,
            onSelect: { id in
                preferences.selectLanguage(id: id)
                onSelected()
            }
        )
    }
}

struct ThemePickerView: View {
    @ObservedObject var preferences: PreferencesModel
    let onSelected: () -> Void

    var body: some View {
        PreferencePickerList(
            title: L10n.Profile.theme,
            options: PreferencesLabels.themes.map {
                PreferenceOption(id: $0.rawValue, label: PreferencesLabels.themeLabel($0))
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
