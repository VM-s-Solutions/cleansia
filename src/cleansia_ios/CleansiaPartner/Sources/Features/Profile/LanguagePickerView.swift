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
                + PreferencesLabels.languages.map { PreferenceOption(id: $0.tag, label: $0.label) },
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

struct PreferenceOption: Identifiable, Equatable {
    let id: String
    let label: String
}

private struct PreferencePickerList: View {
    let title: String
    let options: [PreferenceOption]
    let selectedId: String
    let onSelect: (String) -> Void

    /// A native inset-grouped list: the row press highlight, separators, Dynamic Type row metrics and
    /// the iOS 26 list look come with it. The brand background and surface stay.
    var body: some View {
        List {
            Section {
                ForEach(options) { option in
                    let isSelected = option.id == selectedId
                    Button {
                        onSelect(option.id)
                    } label: {
                        HStack(spacing: Spacing.m) {
                            Text(option.label)
                                .font(CleansiaTypography.bodyLarge)
                                .foregroundColor(CleansiaColors.onSurface)
                            Spacer()
                            if isSelected {
                                Image(systemName: "checkmark")
                                    .font(.system(size: 15, weight: .semibold))
                                    .foregroundColor(CleansiaColors.primary)
                            }
                        }
                        .contentShape(Rectangle())
                    }
                    .listRowBackground(CleansiaColors.surface)
                    .accessibilityAddTraits(isSelected ? .isSelected : [])
                }
            }
        }
        .listStyle(.insetGrouped)
        .scrollContentBackground(.hidden)
        .background(CleansiaColors.background.ignoresSafeArea())
        .navigationTitle(title)
        .navigationBarTitleDisplayMode(.inline)
    }
}

#if DEBUG
    struct PreferencePickerList_Previews: PreviewProvider {
        static var previews: some View {
            NavigationStack {
                PreferencePickerList(
                    title: "Language",
                    options: PreferencesLabels.languages.map { PreferenceOption(id: $0.tag, label: $0.label) },
                    selectedId: "cs",
                    onSelect: { _ in }
                )
            }
        }
    }
#endif
