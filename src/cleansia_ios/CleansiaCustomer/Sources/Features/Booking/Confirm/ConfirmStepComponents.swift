import CleansiaCore
import SwiftUI

struct ExtrasCard: View {
    let extras: [CatalogExtra]
    let selectedSlugs: Set<String>
    let currencyCode: String
    let onToggle: (String) -> Void

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.s) {
            Text(L10n.Booking.extrasHeader)
                .font(CleansiaTypography.titleMedium)
                .fontWeight(.semibold)
                .foregroundColor(CleansiaColors.onSurface)
            Text(L10n.Booking.extrasSubtitle)
                .font(CleansiaTypography.labelMedium)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
            ForEach(extras) { extra in
                ExtraRow(
                    extra: extra,
                    selected: selectedSlugs.contains(extra.slug),
                    currencyCode: currencyCode,
                    onToggle: { onToggle(extra.slug) }
                )
            }
        }
        .padding(Spacing.m)
        .cardSurface(cornerRadius: CornerRadius.large)
    }
}

private struct ExtraRow: View {
    let extra: CatalogExtra
    let selected: Bool
    let currencyCode: String
    let onToggle: () -> Void

    var body: some View {
        Button(action: onToggle) {
            HStack(spacing: Spacing.s) {
                VStack(alignment: .leading, spacing: 2) {
                    Text(extra.localizedName)
                        .font(CleansiaTypography.bodyMedium)
                        .fontWeight(.semibold)
                        .foregroundColor(CleansiaColors.onSurface)
                    if let description = extra.localizedDescription, !description.isBlank {
                        Text(description)
                            .font(CleansiaTypography.labelMedium)
                            .foregroundColor(CleansiaColors.onSurfaceVariant)
                    }
                }
                Spacer()
                Text(BookingPricing.formatTotal(extra.price, currencyCode: currencyCode))
                    .font(CleansiaTypography.bodyMedium)
                    .fontWeight(.semibold)
                    .foregroundColor(selected ? CleansiaColors.primary : CleansiaColors.onSurface)
            }
            .padding(.horizontal, Spacing.s)
            .padding(.vertical, Spacing.s)
            .overlay(
                RoundedRectangle(cornerRadius: CornerRadius.medium)
                    .stroke(
                        selected ? CleansiaColors.primary : CleansiaColors.outlineVariant,
                        lineWidth: selected ? 2 : 1
                    )
            )
        }
        .buttonStyle(.plain)
    }
}

struct SummaryCard: View {
    let state: BookingState
    let basePrice: Double
    let promoDiscount: Double
    let membershipDiscount: Double
    let tierDiscount: Double
    let combinedServerDiscount: Double
    let effectiveDiscount: Double
    let isExpress: Bool
    let surcharge: Double
    let finalTotal: Double
    let currencyCode: String

    private var showPromoLine: Bool {
        promoDiscount > 0 && promoDiscount > combinedServerDiscount
    }

    private var showMembershipLine: Bool {
        !showPromoLine && membershipDiscount > 0
    }

    private var showTierLine: Bool {
        !showPromoLine && tierDiscount > 0
    }

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.xs) {
            detailsSection
            Divider().padding(.vertical, Spacing.xs)
            totalsSection
        }
        .padding(Spacing.m)
        .cardSurface(cornerRadius: CornerRadius.large)
    }

    private var detailsSection: some View {
        VStack(alignment: .leading, spacing: Spacing.xxs) {
            Text(L10n.Booking.summaryDetailsLabel)
                .font(CleansiaTypography.labelMedium)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
            LabeledInfoRow(
                systemImage: "mappin.and.ellipse",
                label: L10n.Booking.summaryAddress,
                value: state.street.isBlank ? "—" : state.street
            )
            LabeledInfoRow(
                systemImage: "house",
                label: L10n.Booking.summaryProperty,
                value: L10n.Booking.summaryProperty(rooms: state.rooms, bathrooms: state.bathrooms)
            )
            LabeledInfoRow(
                systemImage: "calendar",
                label: L10n.Booking.summaryDate,
                value: state.selectedDate.isBlank ? "—" : state.selectedDate
            )
            LabeledInfoRow(
                systemImage: "clock",
                label: L10n.Booking.summaryTime,
                value: state.selectedTime.isBlank ? "—" : state.selectedTime
            )
        }
    }

    private var totalsSection: some View {
        VStack(spacing: Spacing.xxs) {
            AmountRow(label: L10n.Booking.summarySubtotal, value: money(basePrice), emphasis: .normal)
            if showPromoLine {
                AmountRow(
                    label: L10n.Booking.summaryPromoDiscount(state.promoCode),
                    value: "-\(money(promoDiscount))",
                    emphasis: .success
                )
            }
            if showMembershipLine {
                AmountRow(
                    label: L10n.Booking.summaryMembershipDiscount,
                    value: "-\(money(membershipDiscount))",
                    emphasis: .success
                )
            }
            if showTierLine {
                AmountRow(label: L10n.Booking.summaryTierDiscount, value: "-\(money(tierDiscount))", emphasis: .success)
            }
            if isExpress {
                AmountRow(label: L10n.Booking.summaryExpressSurcharge, value: "+\(money(surcharge))", emphasis: .normal)
            }
            AmountRow(label: L10n.Booking.summaryTotal, value: money(finalTotal), emphasis: .total)
        }
    }

    private func money(_ amount: Double) -> String {
        BookingPricing.formatTotal(amount, currencyCode: currencyCode)
    }
}

private enum AmountEmphasis {
    case normal
    case success
    case total
}

private struct AmountRow: View {
    let label: String
    let value: String
    let emphasis: AmountEmphasis

    var body: some View {
        HStack {
            Text(label)
                .font(labelFont)
                .foregroundColor(labelColor)
            Spacer()
            Text(value)
                .font(labelFont)
                .foregroundColor(valueColor)
        }
        .padding(.vertical, 2)
    }

    private var labelFont: Font {
        emphasis == .total ? CleansiaTypography.titleMedium : CleansiaTypography.bodyMedium
    }

    private var labelColor: Color {
        switch emphasis {
        case .normal: CleansiaColors.onSurfaceVariant
        case .success: CleansiaColors.primary
        case .total: CleansiaColors.onSurface
        }
    }

    private var valueColor: Color {
        switch emphasis {
        case .normal: CleansiaColors.onSurface
        case .success: CleansiaColors.primary
        case .total: CleansiaColors.primary
        }
    }
}

private struct LabeledInfoRow: View {
    let systemImage: String
    let label: String
    let value: String

    var body: some View {
        HStack(spacing: Spacing.xs) {
            Image(systemName: systemImage)
                .font(.system(size: 14))
                .foregroundColor(CleansiaColors.onSurfaceVariant)
            Text(label)
                .font(CleansiaTypography.labelMedium)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
                .frame(width: 76, alignment: .leading)
            Text(value)
                .font(CleansiaTypography.bodyMedium)
                .foregroundColor(CleansiaColors.onSurface)
                .lineLimit(2)
            Spacer()
        }
        .padding(.vertical, 2)
    }
}

struct PaymentOption: View {
    let systemImage: String
    let title: String
    let subtitle: String
    let selected: Bool
    let action: () -> Void

    var body: some View {
        Button(action: action) {
            HStack(spacing: Spacing.s) {
                ZStack {
                    Circle()
                        .fill(CleansiaColors.primary.opacity(0.15))
                        .frame(width: 40, height: 40)
                    Image(systemName: systemImage)
                        .font(.system(size: 18))
                        .foregroundColor(CleansiaColors.primary)
                }
                VStack(alignment: .leading, spacing: 2) {
                    Text(title)
                        .font(CleansiaTypography.titleMedium)
                        .foregroundColor(CleansiaColors.onSurface)
                    Text(subtitle)
                        .font(CleansiaTypography.labelMedium)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                }
                Spacer()
            }
            .padding(Spacing.m)
            .background(selected ? CleansiaColors.primaryContainer.opacity(0.35) : CleansiaColors.surface)
            .overlay(
                RoundedRectangle(cornerRadius: CornerRadius.medium)
                    .stroke(
                        selected ? CleansiaColors.primary : CleansiaColors.outlineVariant,
                        lineWidth: selected ? 2 : 1
                    )
            )
            .clipShape(RoundedRectangle(cornerRadius: CornerRadius.medium))
        }
        .buttonStyle(.plain)
    }
}

struct CodeEntryRow: View {
    let systemImage: String
    let title: String
    let appliedCode: String
    let clearLabel: String
    let appliedText: (String) -> String
    let onTap: () -> Void
    let onClear: () -> Void

    private var hasApplied: Bool {
        !appliedCode.isBlank
    }

    var body: some View {
        HStack(spacing: Spacing.s) {
            Button(action: onTap) {
                HStack(spacing: Spacing.s) {
                    ZStack {
                        Circle()
                            .fill(CleansiaColors.primary.opacity(0.15))
                            .frame(width: 36, height: 36)
                        Image(systemName: systemImage)
                            .font(.system(size: 18))
                            .foregroundColor(CleansiaColors.primary)
                    }
                    VStack(alignment: .leading, spacing: 2) {
                        Text(title)
                            .font(CleansiaTypography.titleMedium)
                            .foregroundColor(CleansiaColors.onSurface)
                        if hasApplied {
                            Text(appliedText(appliedCode.uppercased()))
                                .font(CleansiaTypography.labelMedium)
                                .foregroundColor(CleansiaColors.primary)
                        }
                    }
                    Spacer()
                    if !hasApplied {
                        Image(systemName: "chevron.right")
                            .font(.system(size: 14, weight: .semibold))
                            .foregroundColor(CleansiaColors.onSurfaceVariant)
                    }
                }
            }
            .buttonStyle(.plain)
            if hasApplied {
                Button(action: onClear) {
                    Image(systemName: "xmark")
                        .font(.system(size: 14, weight: .semibold))
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                }
                .accessibilityLabel(clearLabel)
            }
        }
        .padding(.horizontal, Spacing.m)
        .padding(.vertical, Spacing.s)
        .background(CleansiaColors.surface)
        .overlay(
            RoundedRectangle(cornerRadius: CornerRadius.medium)
                .stroke(CleansiaColors.outlineVariant, lineWidth: 1)
        )
        .clipShape(RoundedRectangle(cornerRadius: CornerRadius.medium))
    }
}

private extension View {
    func cardSurface(cornerRadius: CGFloat) -> some View {
        background(CleansiaColors.surface)
            .overlay(
                RoundedRectangle(cornerRadius: cornerRadius)
                    .stroke(CleansiaColors.outlineVariant, lineWidth: 1)
            )
            .clipShape(RoundedRectangle(cornerRadius: cornerRadius))
    }
}
