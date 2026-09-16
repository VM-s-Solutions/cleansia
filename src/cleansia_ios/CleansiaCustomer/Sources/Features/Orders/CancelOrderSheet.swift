import CleansiaCore
import SwiftUI

private enum CancelReasonOption: CaseIterable {
    case scheduleChanged
    case bookedByMistake
    case priceTooHigh
    case foundAlternative
    case noLongerNeeded
    case other

    var code: String {
        switch self {
        case .scheduleChanged: "schedule_changed"
        case .bookedByMistake: "booked_by_mistake"
        case .priceTooHigh: "price_too_high"
        case .foundAlternative: "found_alternative"
        case .noLongerNeeded: "no_longer_needed"
        case .other: "other"
        }
    }

    var label: String {
        switch self {
        case .scheduleChanged: L10n.OrderCancel.reasonSchedule
        case .bookedByMistake: L10n.OrderCancel.reasonMistake
        case .priceTooHigh: L10n.OrderCancel.reasonPrice
        case .foundAlternative: L10n.OrderCancel.reasonAlternative
        case .noLongerNeeded: L10n.OrderCancel.reasonNotNeeded
        case .other: L10n.OrderCancel.reasonOther
        }
    }
}

/// Whether the destructive button is live. Hoisted out of the sheet because it is one `.disabled(...)`
/// argument no check can see: it holds both halves of a rule that pull opposite ways — a customer must
/// not commit to a cancellation before the card has said what it costs, and a fee-preview outage must
/// never be able to strand them on a booking they want gone.
enum CancelOrderConfirmGate {
    static func canConfirm(
        hasReason: Bool,
        needsNotes: Bool,
        notes: String,
        quoteIsLoading: Bool,
        isSubmitting: Bool
    ) -> Bool {
        guard hasReason, !quoteIsLoading, !isSubmitting else { return false }
        return !needsNotes || notes.trimmingCharacters(in: .whitespaces).count >= 3
    }

    /// The guest half of the rule. A guest has no account for a surprise fee to be reconciled against
    /// afterwards, so a guest commits only to a quote the server has actually priced for this booking —
    /// a preview outage holds the button instead of degrading, and the card says so.
    static func quoteIsUsable(_ quote: UiState<CancellationQuote>) -> Bool {
        guard let quote = quote.loadedValue else { return false }
        return !(quote.currencyCode?.isBlank ?? true)
    }
}

struct CancelOrderSheet: View {
    let quote: UiState<CancellationQuote>
    let currencyCode: String?
    let isSubmitting: Bool
    let errorMessage: String?
    let onReasonChanged: () -> Void
    let onRetryQuote: () -> Void
    let onConfirm: (String?) -> Void
    let onDismiss: () -> Void
    var requiresQuote = false
    /// A ceiling on the whole `code: notes` payload; nil keeps the notes' own limit. The guest command
    /// refuses a reason over 500 characters, and a refusal after the sheet is filled in is the worst
    /// moment to learn it.
    var reasonLimit: Int?

    @State private var selectedReason: CancelReasonOption?
    @State private var notes = ""

    private static let defaultNotesLimit = 2000

    private var canSubmit: Bool {
        CancelOrderConfirmGate.canConfirm(
            hasReason: selectedReason != nil,
            needsNotes: selectedReason == .other,
            notes: notes,
            quoteIsLoading: quote.isLoading,
            isSubmitting: isSubmitting
        ) && (!requiresQuote || CancelOrderConfirmGate.quoteIsUsable(quote))
    }

    private var notesLimit: Int {
        guard let reasonLimit else { return Self.defaultNotesLimit }
        return max(0, reasonLimit - (selectedReason?.code.count ?? 0) - 2)
    }

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: Spacing.m) {
                Text(L10n.OrderCancel.title)
                    .cleansiaFont(CleansiaTypography.headlineSmall)
                    .foregroundColor(CleansiaColors.onSurface)

                CancellationFeeCard(
                    model: CancellationFeeCardModel(quote, refundIsEstimate: requiresQuote),
                    currencyCode: currencyCode,
                    requiresQuote: requiresQuote,
                    onRetry: onRetryQuote
                )

                Text(L10n.OrderCancel.reasonPickerLabel)
                    .font(CleansiaTypography.titleMedium)
                    .foregroundColor(CleansiaColors.onSurface)

                ReasonChips(selected: $selectedReason, enabled: !isSubmitting) {
                    if notes.count > notesLimit { notes = String(notes.prefix(notesLimit)) }
                    if errorMessage?.isBlank == false { onReasonChanged() }
                }

                if let selectedReason {
                    NotesField(
                        notes: $notes,
                        isOther: selectedReason == .other,
                        enabled: !isSubmitting,
                        maxLength: notesLimit,
                        onChange: { if errorMessage?.isBlank == false { onReasonChanged() } }
                    )
                }

                if let errorMessage, !errorMessage.isBlank {
                    Text(errorMessage)
                        .font(CleansiaTypography.bodyMedium)
                        .foregroundColor(CleansiaColors.error)
                }

                CleansiaOutlinedButton(L10n.OrderCancel.keep, enabled: !isSubmitting, action: onDismiss)

                Button(action: submit) {
                    ZStack {
                        if isSubmitting {
                            // `error`, not `onError`: submitting sets canConfirm false, so this
                            // spinner draws inside the DISABLED branch, and an onError tint
                            // would be white on the near-white neutral fill in light mode.
                            ProgressView().tint(CleansiaColors.error)
                        } else {
                            Text(L10n.OrderCancel.confirm)
                                .font(CleansiaTypography.titleMedium)
                        }
                    }
                    .frame(maxWidth: .infinity, minHeight: 48)
                    // Disabled changes the colours; it does not fade the fill. The label was
                    // already full-strength onError, so fading only the red behind it left
                    // 2.1:1 in light mode and 1.1:1 in dark, where onError is itself a dark
                    // red. Same neutral mute as CleansiaDangerButton.
                    .foregroundColor(
                        canSubmit ? CleansiaColors.onError : CleansiaColors.onSurfaceVariant
                    )
                    .background(
                        canSubmit
                            ? CleansiaColors.error
                            : CleansiaColors.onSurfaceVariant.opacity(0.10)
                    )
                    .clipShape(Capsule())
                    // A 10% neutral over `surface` is nearly the sheet's own colour, so
                    // without a hairline the capsule loses its shape entirely.
                    .overlay(
                        Capsule().stroke(
                            canSubmit ? Color.clear : CleansiaColors.outlineVariant,
                            lineWidth: 1
                        )
                    )
                }
                // Without .plain the default style dims on top of the explicit colours and
                // re-fades the neutral this just chose.
                .buttonStyle(.plain)
                .disabled(!canSubmit)
            }
            .padding(Spacing.l)
        }
        .background(CleansiaColors.surface.ignoresSafeArea())
        .presentationDetents([.large])
        .presentationDragIndicator(.visible)
        .interactiveDismissDisabled(isSubmitting)
    }

    private func submit() {
        guard canSubmit, let selectedReason else { return }
        var payload = selectedReason.code
        let trimmed = notes.trimmingCharacters(in: .whitespacesAndNewlines)
        if !trimmed.isEmpty { payload += ": \(trimmed)" }
        onConfirm(payload)
    }
}

private struct ReasonChips: View {
    @Binding var selected: CancelReasonOption?
    let enabled: Bool
    let onSelect: () -> Void

    var body: some View {
        FlexibleReasonGrid(options: CancelReasonOption.allCases) { option in
            CleansiaChip(label: option.label, isSelected: selected == option, enabled: enabled) {
                selected = selected == option ? nil : option
                onSelect()
            }
        }
    }
}

private struct FlexibleReasonGrid: View {
    let options: [CancelReasonOption]
    let chip: (CancelReasonOption) -> CleansiaChip

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.xs) {
            ForEach(rows, id: \.self) { row in
                HStack(spacing: Spacing.xs) {
                    ForEach(row, id: \.self) { option in
                        chip(option)
                    }
                    Spacer(minLength: 0)
                }
            }
        }
    }

    private var rows: [[CancelReasonOption]] {
        stride(from: 0, to: options.count, by: 2).map {
            Array(options[$0 ..< min($0 + 2, options.count)])
        }
    }
}

private struct NotesField: View {
    @Binding var notes: String
    let isOther: Bool
    let enabled: Bool
    let maxLength: Int
    let onChange: () -> Void

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.xxs) {
            Text(isOther ? L10n.OrderCancel.notesRequiredLabel : L10n.OrderCancel.notesOptionalLabel)
                .font(CleansiaTypography.labelMedium)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
            TextEditor(text: $notes)
                .frame(minHeight: 88)
                .scrollContentBackground(.hidden)
                .padding(Spacing.xs)
                .background(CleansiaColors.surface)
                .overlay(
                    RoundedRectangle(cornerRadius: CornerRadius.medium)
                        .stroke(CleansiaColors.outlineVariant, lineWidth: 1)
                )
                .disabled(!enabled)
                .onChange(of: notes) { value in
                    if value.count > maxLength { notes = String(value.prefix(maxLength)) }
                    onChange()
                }
        }
    }
}
