import SwiftUI

public struct CleansiaDropdownOption: Identifiable, Equatable {
    public let id: String
    public let label: String

    public init(id: String, label: String) {
        self.id = id
        self.label = label
    }
}

public struct CleansiaDropdown: View {
    @Binding private var selectedId: String?
    private let options: [CleansiaDropdownOption]
    private let label: String
    private let helper: String?
    private let errorText: String?
    private let placeholder: String?
    private let enabled: Bool
    private let searchable: Bool

    @State private var sheetOpen = false

    public init(
        selectedId: Binding<String?>,
        options: [CleansiaDropdownOption],
        label: String,
        helper: String? = nil,
        errorText: String? = nil,
        placeholder: String? = nil,
        enabled: Bool = true,
        searchable: Bool = false
    ) {
        _selectedId = selectedId
        self.options = options
        self.label = label
        self.helper = helper
        self.errorText = errorText
        self.placeholder = placeholder
        self.enabled = enabled
        self.searchable = searchable
    }

    private var isError: Bool { errorText != nil }
    private var selected: CleansiaDropdownOption? { options.first { $0.id == selectedId } }

    public var body: some View {
        VStack(alignment: .leading, spacing: Spacing.xxs) {
            Button {
                if enabled { sheetOpen = true }
            } label: {
                VStack(alignment: .leading, spacing: 2) {
                    Text(label)
                        .font(CleansiaTypography.labelMedium)
                        .foregroundColor(labelColor)
                    HStack {
                        Text(selected?.label ?? placeholder ?? "")
                            .font(CleansiaTypography.bodyLarge)
                            .foregroundColor(valueColor)
                            .frame(maxWidth: .infinity, alignment: .leading)
                        Image(systemName: "chevron.down")
                            .font(.system(size: 14))
                            .foregroundColor(CleansiaColors.onSurfaceVariant)
                    }
                }
                .padding(.horizontal, Spacing.m)
                .padding(.vertical, 10)
                .frame(maxWidth: .infinity)
                .overlay(
                    RoundedRectangle(cornerRadius: CornerRadius.small)
                        .stroke(isError ? CleansiaColors.error : CleansiaColors.outline, lineWidth: 1)
                )
            }
            .buttonStyle(.plain)
            .disabled(!enabled)

            if let supporting = errorText ?? helper {
                Text(supporting)
                    .font(CleansiaTypography.labelSmall)
                    .foregroundColor(isError ? CleansiaColors.error : CleansiaColors.onSurfaceVariant)
                    .padding(.horizontal, Spacing.m)
            }
        }
        .sheet(isPresented: $sheetOpen) {
            DropdownSheet(
                title: label,
                options: options,
                selectedId: selectedId,
                searchable: searchable,
                onSelect: { id in
                    selectedId = id
                    sheetOpen = false
                }
            )
            .presentationDetents([.medium, .large])
        }
    }

    private var labelColor: Color {
        if isError { return CleansiaColors.error }
        return enabled ? CleansiaColors.onSurfaceVariant : CleansiaColors.onSurfaceVariant.opacity(0.6)
    }

    private var valueColor: Color {
        if selected != nil { return CleansiaColors.onSurface }
        return CleansiaColors.onSurfaceVariant.opacity(0.6)
    }
}

private struct DropdownSheet: View {
    let title: String
    let options: [CleansiaDropdownOption]
    let selectedId: String?
    let searchable: Bool
    let onSelect: (String) -> Void

    @State private var query = ""

    private var filtered: [CleansiaDropdownOption] {
        guard searchable, !query.trimmingCharacters(in: .whitespaces).isEmpty else { return options }
        return options.filter { $0.label.range(of: query, options: .caseInsensitive) != nil }
    }

    var body: some View {
        NavigationStack {
            List(filtered) { option in
                Button {
                    onSelect(option.id)
                } label: {
                    HStack {
                        Text(option.label)
                            .font(CleansiaTypography.bodyLarge)
                            .fontWeight(option.id == selectedId ? .semibold : .regular)
                            .foregroundColor(option.id == selectedId ? CleansiaColors.primary : CleansiaColors.onSurface)
                        Spacer()
                        if option.id == selectedId {
                            Image(systemName: "checkmark").foregroundColor(CleansiaColors.primary)
                        }
                    }
                }
            }
            .listStyle(.plain)
            .navigationTitle(title)
            .navigationBarTitleDisplayMode(.inline)
            .modifier(SearchableIfNeeded(enabled: searchable, query: $query))
        }
    }
}

private struct SearchableIfNeeded: ViewModifier {
    let enabled: Bool
    @Binding var query: String

    func body(content: Content) -> some View {
        if enabled {
            content.searchable(text: $query)
        } else {
            content
        }
    }
}

#if DEBUG
struct CleansiaDropdown_Previews: PreviewProvider {
    static var previews: some View {
        StatefulPreviewWrapper(String?.some("cz")) { binding in
            CleansiaDropdown(
                selectedId: binding,
                options: [
                    .init(id: "cz", label: "Czechia"),
                    .init(id: "sk", label: "Slovakia"),
                ],
                label: "Country",
                searchable: true
            )
            .padding()
        }
    }
}
#endif
