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
                if id == PreferencesLabels.systemLanguageId {
                    preferences.setSystemLanguage()
                } else {
                    preferences.setLanguage(id)
                }
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

    var body: some View {
        ScrollView {
            VStack(spacing: 0) {
                ForEach(options.indices, id: \.self) { index in
                    PreferenceRow(
                        option: options[index],
                        isSelected: options[index].id == selectedId,
                        onTap: { onSelect(options[index].id) }
                    )
                    if index < options.count - 1 {
                        Divider()
                            .background(CleansiaColors.outline.opacity(0.5))
                            .padding(.leading, Spacing.m)
                    }
                }
            }
            .background(CleansiaColors.surface)
            .clipShape(RoundedRectangle(cornerRadius: CornerRadius.large))
            .padding(Spacing.m)
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .background(CleansiaColors.background.ignoresSafeArea())
        .navigationTitle(title)
        .navigationBarTitleDisplayMode(.inline)
    }
}

private struct PreferenceRow: View {
    let option: PreferenceOption
    let isSelected: Bool
    let onTap: () -> Void

    var body: some View {
        Button(action: onTap) {
            HStack(spacing: Spacing.m) {
                Text(option.label)
                    .font(CleansiaTypography.bodyLarge)
                    .fontWeight(isSelected ? .semibold : .regular)
                    .foregroundColor(isSelected ? CleansiaColors.primary : CleansiaColors.onSurface)
                Spacer()
                if isSelected {
                    ZStack {
                        Circle()
                            .fill(CleansiaColors.primary)
                            .frame(width: 24, height: 24)
                        Image(systemName: "checkmark")
                            .font(.system(size: 13, weight: .semibold))
                            .foregroundColor(CleansiaColors.onPrimary)
                    }
                }
            }
            .padding(Spacing.m)
            .frame(maxWidth: .infinity)
            .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
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
