import SwiftUI

public struct BirthDateField: View {
    @Binding private var birthDate: Date?
    private let label: String
    private let placeholder: String
    private let errorText: String?

    @Environment(\.locale) private var locale
    @State private var showPicker = false

    public init(
        birthDate: Binding<Date?>,
        label: String,
        placeholder: String,
        errorText: String? = nil
    ) {
        _birthDate = birthDate
        self.label = label
        self.placeholder = placeholder
        self.errorText = errorText
    }

    private var isError: Bool {
        errorText != nil
    }

    private var displayText: String {
        guard let birthDate else { return placeholder }
        let formatter = DateFormatter()
        formatter.dateStyle = .medium
        formatter.locale = locale
        return formatter.string(from: birthDate)
    }

    public var body: some View {
        VStack(alignment: .leading, spacing: Spacing.xs) {
            Text(label)
                .font(CleansiaTypography.labelMedium)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
            Button {
                showPicker = true
            } label: {
                HStack {
                    Text(displayText)
                        .font(CleansiaTypography.bodyLarge)
                        .foregroundColor(birthDate == nil ? CleansiaColors.onSurfaceVariant : CleansiaColors.onSurface)
                    Spacer()
                    Image(systemName: "calendar")
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                }
                .padding(Spacing.m)
                .background(CleansiaColors.surface)
                .overlay(
                    RoundedRectangle(cornerRadius: CornerRadius.small)
                        .stroke(isError ? CleansiaColors.error : CleansiaColors.outline, lineWidth: 1)
                )
                .clipShape(RoundedRectangle(cornerRadius: CornerRadius.small))
            }
            .buttonStyle(.plain)

            if let errorText {
                Text(errorText)
                    .font(CleansiaTypography.labelSmall)
                    .foregroundColor(CleansiaColors.error)
                    .padding(.horizontal, Spacing.m)
            }
        }
        .sheet(isPresented: $showPicker) {
            DatePicker(
                label,
                selection: Binding(get: { birthDate ?? Date() }, set: { birthDate = $0 }),
                in: ...Date(),
                displayedComponents: .date
            )
            .datePickerStyle(.graphical)
            .padding()
            .presentationDetents([.medium])
        }
    }
}

#if DEBUG
    struct BirthDateField_Previews: PreviewProvider {
        static var previews: some View {
            StatefulPreviewWrapper(Date?.none) { binding in
                VStack(spacing: Spacing.m) {
                    BirthDateField(
                        birthDate: binding,
                        label: "Date of birth",
                        placeholder: "Pick a date"
                    )
                    BirthDateField(
                        birthDate: .constant(nil),
                        label: "Date of birth",
                        placeholder: "Pick a date",
                        errorText: "Date of birth is required"
                    )
                }
                .padding()
            }
        }
    }
#endif
