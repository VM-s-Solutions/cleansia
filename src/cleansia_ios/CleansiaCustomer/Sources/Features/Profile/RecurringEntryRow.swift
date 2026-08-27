import CleansiaCore
import SwiftUI

/// The profile tab's route into recurring bookings.
///
/// **Shown unconditionally, and that is the point.** A lapsed membership does not stop a live
/// schedule, so this is the route to the stop button; and for a customer who has never subscribed,
/// `RecurringBookingsScreen` already answers with `PlusGate` rather than an empty list, which makes
/// this row the entry to the Plus upsell. iOS previously had no row at all, so neither case was
/// reachable from the profile tab.
///
/// Built to the Android twin's numbers: 36 icon disc at `primary` 15%, 20 glyph, 14 corner radius,
/// 14/12 padding, 12 gap.
struct RecurringEntryRow: View {
    let onTap: () -> Void

    private static let disc: CGFloat = 36
    private static let radius: CGFloat = 14

    var body: some View {
        Button(action: onTap) {
            HStack(spacing: Spacing.s) {
                ZStack {
                    Circle()
                        .fill(CleansiaColors.primary.opacity(0.15))
                        .frame(width: Self.disc, height: Self.disc)
                    Image(systemName: "calendar")
                        .font(.system(size: 20, weight: .medium))
                        .foregroundColor(CleansiaColors.primary)
                }

                VStack(alignment: .leading, spacing: Spacing.hair) {
                    Text(L10n.Recurring.profileRowTitle)
                        .font(CleansiaTypography.titleMedium)
                        .foregroundColor(CleansiaColors.onSurface)
                        .multilineTextAlignment(.leading)
                    Text(L10n.Recurring.profileRowSubtitle)
                        .font(CleansiaTypography.bodyMedium)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                        .multilineTextAlignment(.leading)
                }
                .frame(maxWidth: .infinity, alignment: .leading)

                Image(systemName: "chevron.right")
                    .font(.system(size: 14, weight: .semibold))
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
            }
            .padding(.horizontal, 14)
            .padding(.vertical, Spacing.s)
            .background(CleansiaColors.surface)
            .overlay(
                RoundedRectangle(cornerRadius: Self.radius)
                    .stroke(CleansiaColors.outlineVariant, lineWidth: 1)
            )
            .clipShape(RoundedRectangle(cornerRadius: Self.radius))
        }
        .buttonStyle(.plain)
        .accessibilityElement(children: .combine)
        .accessibilityAddTraits(.isButton)
    }
}

#if DEBUG
    struct RecurringEntryRow_Previews: PreviewProvider {
        static var previews: some View {
            RecurringEntryRow(onTap: {})
                .padding()
                .background(CleansiaColors.background)
        }
    }
#endif
