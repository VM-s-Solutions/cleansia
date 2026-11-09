import SwiftUI

public struct PreferenceOption: Identifiable, Equatable {
    public let id: String
    public let label: String

    public init(id: String, label: String) {
        self.id = id
        self.label = label
    }
}

public struct PreferencePickerList: View {
    let title: String
    let options: [PreferenceOption]
    let selectedId: String
    let onSelect: (String) -> Void

    public init(title: String, options: [PreferenceOption], selectedId: String, onSelect: @escaping (String) -> Void) {
        self.title = title
        self.options = options
        self.selectedId = selectedId
        self.onSelect = onSelect
    }

    /// A native inset-grouped list: the row press highlight, separators, Dynamic Type row metrics and
    /// the iOS 26 list look come with it. The brand background and surface stay.
    public var body: some View {
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
                    options: LanguageLabels.languages.map { PreferenceOption(id: $0.tag, label: $0.label) },
                    selectedId: "cs",
                    onSelect: { _ in }
                )
            }
        }
    }
#endif
