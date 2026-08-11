import SwiftUI

public struct BirthDateField: View {
    @Binding private var birthDate: Date?
    private let label: String
    private let placeholder: String
    private let helper: String?
    private let errorText: String?

    @Environment(\.locale) private var locale
    @State private var showPicker = false

    public init(
        birthDate: Binding<Date?>,
        label: String,
        placeholder: String,
        helper: String? = nil,
        errorText: String? = nil
    ) {
        _birthDate = birthDate
        self.label = label
        self.placeholder = placeholder
        self.helper = helper
        self.errorText = errorText
    }

    private var isError: Bool {
        errorText != nil
    }

    private var displayText: String {
        guard let birthDate else { return placeholder }
        return CalendarDay.text(birthDate, locale: locale)
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

            if let supporting = errorText ?? helper {
                Text(supporting)
                    .font(CleansiaTypography.labelSmall)
                    .foregroundColor(isError ? CleansiaColors.error : CleansiaColors.onSurfaceVariant)
                    .padding(.horizontal, Spacing.m)
            }
        }
        .sheet(isPresented: $showPicker) {
            // Picked, shown and stored in one zone. The picker keeps the time of day it was seeded
            // with, so a day chosen in the handset's calendar and a day decoded off the wire are
            // otherwise different instants that encode as different days.
            DatePicker(
                label,
                selection: CalendarDay.pickerBinding($birthDate),
                in: ...Date(),
                displayedComponents: .date
            )
            .datePickerStyle(.graphical)
            .environment(\.calendar, CalendarDay.calendar)
            .environment(\.timeZone, CalendarDay.calendar.timeZone)
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
                        placeholder: "Pick a date",
                        helper: "Optional — helps us tailor your offers"
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
