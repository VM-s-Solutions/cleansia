import CleansiaCore
import SwiftUI

struct DirtinessStep: View {
    @ObservedObject var viewModel: BookingViewModel

    var body: some View {
        ScrollView {
            DirtinessPicker(selected: viewModel.state.dirtiness, onSelect: viewModel.setDirtiness)
                .padding(Spacing.l)
        }
    }
}

/// The three levels with what each looks like at home, mildest first, under the advice to pick the
/// higher one when torn. Shared by the booking wizard and the recurring form.
struct DirtinessPicker: View {
    let selected: Dirtiness?
    let onSelect: (Dirtiness) -> Void

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.s) {
            PaymentNote(systemImage: "info.circle", text: L10n.Booking.dirtinessHint)
            ForEach(Dirtiness.allCases, id: \.self) { level in
                DirtinessCard(level: level, selected: level == selected) {
                    onSelect(level)
                }
            }
        }
    }
}

private struct DirtinessCard: View {
    let level: Dirtiness
    let selected: Bool
    let onTap: () -> Void

    var body: some View {
        Button(action: onTap) {
            VStack(alignment: .leading, spacing: Spacing.xs) {
                HStack(spacing: Spacing.s) {
                    Image(systemName: selected ? "checkmark.circle.fill" : "circle")
                        .foregroundColor(selected ? CleansiaColors.primary : CleansiaColors.onSurfaceVariant)
                    Text(L10n.Booking.dirtinessName(level))
                        .font(CleansiaTypography.titleMedium)
                        .foregroundColor(CleansiaColors.onSurface)
                    Spacer(minLength: Spacing.xs)
                    Text(L10n.Booking.dirtinessRate(level))
                        .font(CleansiaTypography.labelLarge)
                        .foregroundColor(CleansiaColors.primary)
                }
                Text(L10n.Booking.dirtinessLead(level))
                    .font(CleansiaTypography.bodyMedium)
                    .foregroundColor(CleansiaColors.onSurface)
                    .fixedSize(horizontal: false, vertical: true)
                ForEach(L10n.Booking.dirtinessSigns(level), id: \.self) { sign in
                    HStack(alignment: .firstTextBaseline, spacing: Spacing.xs) {
                        Image(systemName: "circle.fill")
                            .font(.system(size: 5))
                            .foregroundColor(CleansiaColors.onSurfaceVariant)
                        Text(sign)
                            .font(CleansiaTypography.bodyMedium)
                            .foregroundColor(CleansiaColors.onSurfaceVariant)
                            .fixedSize(horizontal: false, vertical: true)
                    }
                }
            }
            .padding(Spacing.m)
            .frame(maxWidth: .infinity, alignment: .leading)
            .background(CleansiaColors.surface, in: RoundedRectangle(cornerRadius: CornerRadius.medium))
            .overlay(
                RoundedRectangle(cornerRadius: CornerRadius.medium)
                    .stroke(
                        selected ? CleansiaColors.primary : CleansiaColors.outlineVariant,
                        lineWidth: selected ? 2 : 1
                    )
            )
            .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
        .accessibilityAddTraits(selected ? [.isSelected] : [])
    }
}

#if DEBUG
    struct DirtinessStep_Previews: PreviewProvider {
        static var previews: some View {
            Group {
                DirtinessPicker(selected: nil, onSelect: { _ in })
                    .previewDisplayName("Nothing picked")
                DirtinessPicker(selected: .increased, onSelect: { _ in })
                    .previewDisplayName("Increased")
            }
            .padding(Spacing.l)
            .background(CleansiaColors.background)
        }
    }
#endif
