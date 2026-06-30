import CleansiaCore
import CleansiaCustomerApi
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

struct CancelOrderSheet: View {
    let order: OrderItem
    let isSubmitting: Bool
    let errorMessage: String?
    let onReasonChanged: () -> Void
    let onConfirm: (String?) -> Void
    let onDismiss: () -> Void

    @State private var selectedReason: CancelReasonOption?
    @State private var notes = ""

    private let maxReasonLength = 2000

    private var canSubmit: Bool {
        guard let selectedReason else { return false }
        return selectedReason != .other || notes.trimmingCharacters(in: .whitespaces).count >= 3
    }

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: Spacing.m) {
                Text(L10n.OrderCancel.title)
                    .font(CleansiaTypography.headlineSmall)
                    .foregroundColor(CleansiaColors.onSurface)

                FeePreviewCard(order: order)

                Text(L10n.OrderCancel.reasonPickerLabel)
                    .font(CleansiaTypography.titleMedium)
                    .foregroundColor(CleansiaColors.onSurface)

                ReasonChips(selected: $selectedReason, enabled: !isSubmitting) {
                    if errorMessage?.isBlank == false { onReasonChanged() }
                }

                if let selectedReason {
                    NotesField(
                        notes: $notes,
                        isOther: selectedReason == .other,
                        enabled: !isSubmitting,
                        maxLength: maxReasonLength,
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
                            ProgressView().tint(CleansiaColors.onError)
                        } else {
                            Text(L10n.OrderCancel.confirm)
                                .font(CleansiaTypography.titleMedium)
                        }
                    }
                    .frame(maxWidth: .infinity, minHeight: 48)
                    .foregroundColor(CleansiaColors.onError)
                    .background(CleansiaColors.error.opacity(canSubmit && !isSubmitting ? 1 : 0.4))
                    .clipShape(Capsule())
                }
                .disabled(!canSubmit || isSubmitting)
            }
            .padding(Spacing.l)
        }
        .background(CleansiaColors.surface.ignoresSafeArea())
        .presentationDetents([.large])
        .presentationDragIndicator(.visible)
        .interactiveDismissDisabled(isSubmitting)
    }

    private func submit() {
        guard canSubmit, !isSubmitting, let selectedReason else { return }
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
            ChipButton(label: option.label, selected: selected == option, enabled: enabled) {
                selected = selected == option ? nil : option
                onSelect()
            }
        }
    }
}

private struct FlexibleReasonGrid: View {
    let options: [CancelReasonOption]
    let chip: (CancelReasonOption) -> ChipButton

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

private struct ChipButton: View {
    let label: String
    let selected: Bool
    let enabled: Bool
    let action: () -> Void

    var body: some View {
        Button(action: action) {
            Text(label)
                .font(CleansiaTypography.bodyMedium)
                .foregroundColor(selected ? CleansiaColors.primary : CleansiaColors.onSurface)
                .padding(.horizontal, Spacing.s)
                .padding(.vertical, Spacing.xs)
                .background(selected ? CleansiaColors.primary.opacity(0.12) : CleansiaColors.surface, in: Capsule())
                .overlay(
                    Capsule().stroke(
                        selected ? CleansiaColors.primary : CleansiaColors.outlineVariant,
                        lineWidth: selected ? 1.5 : 1
                    )
                )
        }
        .buttonStyle(.plain)
        .disabled(!enabled)
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

private struct FeePreviewCard: View {
    let order: OrderItem

    private var tier: CancellationFeeTier {
        CancellationFeePreview.tier(
            cleaningAt: order.cleaningDateTime,
            createdAt: order.createdOn,
            totalPrice: order.totalPrice ?? 0,
            now: Date()
        )
    }

    private var presentation: FeePresentation {
        switch tier {
        case .oops:
            FeePresentation(color: CleansiaColors.primary, symbol: "checkmark.circle", title: L10n.OrderCancel.feeOops)
        case .free:
            FeePresentation(color: CleansiaColors.primary, symbol: "checkmark.circle", title: L10n.OrderCancel.feeFree)
        case let .half(refund):
            FeePresentation(
                color: CleansiaColors.warningStar,
                symbol: "exclamationmark.triangle",
                title: halfFeeTitle(refund: refund)
            )
        case .full:
            FeePresentation(
                color: CleansiaColors.error,
                symbol: "exclamationmark.triangle",
                title: L10n.OrderCancel.fee100
            )
        case .neutral:
            FeePresentation(
                color: CleansiaColors.onSurfaceVariant,
                symbol: "exclamationmark.triangle",
                title: L10n.OrderCancel.feeNeutral
            )
        }
    }

    private func halfFeeTitle(refund: Double) -> String {
        L10n.OrderCancel.fee50(OrdersFormat.price(refund, currencyCode: order.currency?.code))
    }

    var body: some View {
        HStack(alignment: .top, spacing: Spacing.s) {
            Image(systemName: presentation.symbol)
                .font(.system(size: 20))
                .foregroundColor(presentation.color)
            VStack(alignment: .leading, spacing: Spacing.xxs) {
                Text(presentation.title)
                    .font(CleansiaTypography.titleMedium)
                    .foregroundColor(CleansiaColors.onSurface)
                Text(L10n.OrderCancel.feeEstimateNote)
                    .font(CleansiaTypography.bodyMedium)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
            }
            Spacer()
        }
        .padding(Spacing.s)
        .frame(maxWidth: .infinity, alignment: .leading)
        .background(presentation.color.opacity(0.10), in: RoundedRectangle(cornerRadius: CornerRadius.medium))
        .overlay(
            RoundedRectangle(cornerRadius: CornerRadius.medium)
                .stroke(presentation.color.opacity(0.35), lineWidth: 1)
        )
    }
}

private struct FeePresentation {
    let color: Color
    let symbol: String
    let title: String
}
