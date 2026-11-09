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
        return CalendarDay.text(birthDate, locale: locale)
    }

    public var body: some View {
        VStack(alignment: .leading, spacing: Spacing.xs) {
            Text(label)
                .font(CleansiaTypography.labelMedium)
                .foregroundColor(isError ? CleansiaColors.error : CleansiaColors.onSurfaceVariant)
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
                .clipShape(RoundedRectangle(cornerRadius: CornerRadius.small))
                .overlay(
                    RoundedRectangle(cornerRadius: CornerRadius.small)
                        .stroke(isError ? CleansiaColors.error : CleansiaColors.outline, lineWidth: 1)
                )
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
            BirthDateWheelSheet(birthDate: $birthDate, title: label)
        }
    }
}

/// The half-height wheels a birth date is picked on. A wheel that moves stores its day, so Done only closes, and a
/// sheet closed untouched leaves an empty field empty.
public struct BirthDateWheelSheet: View {
    @Binding private var birthDate: Date?
    private let title: String
    private let note: String?
    @Environment(\.dismiss) private var dismiss

    public init(birthDate: Binding<Date?>, title: String, note: String? = nil) {
        _birthDate = birthDate
        self.title = title
        self.note = note
    }

    public var body: some View {
        VStack(alignment: .leading, spacing: Spacing.xs) {
            HStack {
                Text(title)
                    .cleansiaFont(CleansiaTypography.headlineSmall)
                    .foregroundColor(CleansiaColors.onSurface)
                Spacer()
                Button { dismiss() } label: {
                    Text(CoreL10n.localized("birth_date_field.done"))
                        .font(CleansiaTypography.titleLarge)
                        .foregroundColor(CleansiaColors.primaryText)
                        .frame(minWidth: 44, minHeight: 44)
                        .contentShape(Rectangle())
                }
                .buttonStyle(.plain)
            }
            if let note {
                Text(note)
                    .font(CleansiaTypography.bodyMedium)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
            }
            // Picked, shown and stored in one zone. The picker keeps the time of day it was seeded
            // with, so a day chosen in the handset's calendar and a day decoded off the wire are
            // otherwise different instants that encode as different days.
            DatePicker(
                title,
                selection: CalendarDay.pickerBinding($birthDate),
                in: ...Date(),
                displayedComponents: .date
            )
            // Wheels, not a month grid: an adult's birth date is decades back, a few spins on a wheel
            // and many page flips on a calendar.
            .datePickerStyle(.wheel)
            .labelsHidden()
            .environment(\.calendar, CalendarDay.calendar)
            .environment(\.timeZone, CalendarDay.calendar.timeZone)
            .frame(maxWidth: .infinity)
        }
        .padding(.horizontal, Spacing.ml)
        .padding(.top, Spacing.ml)
        .frame(maxHeight: .infinity, alignment: .top)
        .background(CleansiaColors.surface)
        .presentationDetents([.medium])
        .presentationDragIndicator(.visible)
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
