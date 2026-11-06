import CleansiaCore
import SwiftUI

struct DirtinessStep: View {
    @ObservedObject var viewModel: BookingViewModel
    @Environment(\.accessibilityReduceMotion) private var reduceMotion

    var body: some View {
        ScrollViewReader { proxy in
            ScrollView {
                DirtinessPicker(selected: viewModel.state.dirtiness, onSelect: viewModel.setDirtiness)
                    .padding(.horizontal, Spacing.m)
                    .padding(.vertical, Spacing.xs)
            }
            .onChange(of: viewModel.state.dirtiness) { level in
                guard let level else { return }
                Task {
                    await Task.yield()
                    withAnimation(reduceMotion ? nil : .default) {
                        proxy.scrollTo(level)
                    }
                }
            }
        }
    }
}

/// The three levels, mildest first, each with when it applies; only the picked one lists what it looks like at
/// home, so the step fits a small phone. The advice to pick the higher one when torn follows them. Shared by the
/// booking wizard and the recurring form.
struct DirtinessPicker: View {
    let selected: Dirtiness?
    let onSelect: (Dirtiness) -> Void

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.xs) {
            ForEach(Dirtiness.allCases, id: \.self) { level in
                DirtinessCard(level: level, selected: level == selected) {
                    onSelect(level)
                }
                .id(level)
            }
            PaymentNote(systemImage: "info.circle", text: L10n.Booking.dirtinessHint)
        }
    }
}

private struct DirtinessCard: View {
    let level: Dirtiness
    let selected: Bool
    let onTap: () -> Void

    var body: some View {
        Button(action: onTap) {
            HStack(alignment: .firstTextBaseline, spacing: Spacing.xs) {
                Image(systemName: selected ? "checkmark.circle.fill" : "circle")
                    .foregroundColor(selected ? CleansiaColors.primary : CleansiaColors.onSurfaceVariant)
                VStack(alignment: .leading, spacing: Spacing.xxs) {
                    HStack(alignment: .firstTextBaseline, spacing: Spacing.xs) {
                        Text(L10n.Booking.dirtinessName(level))
                            .font(CleansiaTypography.titleMedium)
                            .foregroundColor(CleansiaColors.onSurface)
                        Spacer(minLength: Spacing.xs)
                        Text(L10n.Booking.dirtinessRate(level))
                            .font(CleansiaTypography.labelLarge)
                            .foregroundColor(CleansiaColors.primaryText)
                    }
                    Text(L10n.Booking.dirtinessLead(level))
                        .font(CleansiaTypography.bodySmall)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                        .fixedSize(horizontal: false, vertical: true)
                    if selected {
                        signs
                    }
                }
            }
            .padding(Spacing.s)
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

    private var signs: some View {
        VStack(alignment: .leading, spacing: Spacing.hair) {
            ForEach(L10n.Booking.dirtinessSigns(level), id: \.self) { sign in
                HStack(alignment: .firstTextBaseline, spacing: Spacing.xs) {
                    Image(systemName: "circle.fill")
                        .font(.system(size: 5))
                        .foregroundColor(CleansiaColors.primary)
                    Text(sign)
                        .font(CleansiaTypography.bodySmall)
                        .foregroundColor(CleansiaColors.onSurface)
                        .fixedSize(horizontal: false, vertical: true)
                }
            }
        }
        .padding(.top, Spacing.hair)
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
