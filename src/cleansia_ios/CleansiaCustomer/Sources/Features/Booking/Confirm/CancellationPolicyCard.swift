import CleansiaCore
import SwiftUI

struct CancellationPolicyCard: View {
    let policy: CancellationPolicy

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.xs) {
            header
            if let plusHours = policy.plusFreeHours {
                Text(L10n.Booking.cancelPlusSubtitle(plusHours))
                    .font(CleansiaTypography.labelMedium)
                    .foregroundColor(CleansiaColors.primary)
            }
            PolicyTier(
                label: L10n.Booking.cancelTier1WhenPlus(policy.freeHours),
                value: L10n.Booking.cancelTier1Value,
                valueColor: CleansiaColors.successText
            )
            if policy.showMidTier {
                PolicyTier(
                    label: L10n.Booking.cancelTier2WhenRange(policy.penaltyHours, policy.freeHours),
                    value: L10n.Booking.cancelTier2Value,
                    valueColor: CleansiaColors.onSurface
                )
            }
            PolicyTier(
                label: L10n.Booking.cancelTier3WhenUnder(policy.penaltyHours),
                value: L10n.Booking.cancelTier3Value,
                valueColor: CleansiaColors.error
            )
        }
        .padding(Spacing.m)
        .background(CleansiaColors.surface)
        .overlay(
            RoundedRectangle(cornerRadius: CornerRadius.large)
                .stroke(CleansiaColors.outlineVariant, lineWidth: 1)
        )
        .clipShape(RoundedRectangle(cornerRadius: CornerRadius.large))
    }

    private var header: some View {
        HStack(spacing: Spacing.xs) {
            Image(systemName: "clock")
                .font(.system(size: 16))
                .foregroundColor(CleansiaColors.primary)
            Text(L10n.Booking.cancelTitle)
                .font(CleansiaTypography.titleMedium)
                .fontWeight(.semibold)
                .foregroundColor(CleansiaColors.onBackground)
            if policy.hasPlusPerk {
                Spacer()
                Text(L10n.Booking.cancelPlusBadge)
                    .font(CleansiaTypography.labelSmall)
                    .fontWeight(.bold)
                    .foregroundColor(CleansiaColors.primary)
                    .padding(.horizontal, Spacing.xs)
                    .padding(.vertical, 3)
                    .background(CleansiaColors.primary.opacity(0.12))
                    .clipShape(RoundedRectangle(cornerRadius: CornerRadius.small))
            }
        }
    }
}

private struct PolicyTier: View {
    let label: String
    let value: String
    let valueColor: Color

    var body: some View {
        HStack {
            Text(label)
                .font(CleansiaTypography.labelMedium)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
            Spacer()
            Text(value)
                .font(CleansiaTypography.labelMedium)
                .fontWeight(.semibold)
                .foregroundColor(valueColor)
        }
        .padding(.vertical, 3)
    }
}

struct TrustBadges: View {
    var body: some View {
        HStack(spacing: Spacing.s) {
            TrustBadge(systemImage: "checkmark.shield", text: L10n.Booking.trustInsured)
            Rectangle()
                .fill(CleansiaColors.outlineVariant)
                .frame(width: 1)
            TrustBadge(systemImage: "person.badge.shield.checkmark", text: L10n.Booking.trustVetted)
        }
        .fixedSize(horizontal: false, vertical: true)
        .padding(Spacing.m)
        .background(CleansiaColors.surface)
        .overlay(
            RoundedRectangle(cornerRadius: CornerRadius.large)
                .stroke(CleansiaColors.outlineVariant, lineWidth: 1)
        )
        .clipShape(RoundedRectangle(cornerRadius: CornerRadius.large))
    }
}

private struct TrustBadge: View {
    let systemImage: String
    let text: String

    var body: some View {
        HStack(spacing: Spacing.xs) {
            Image(systemName: systemImage)
                .font(.system(size: 18))
                .foregroundColor(CleansiaColors.successText)
            Text(text)
                .font(CleansiaTypography.labelMedium)
                .foregroundColor(CleansiaColors.onSurface)
                .lineLimit(2)
            Spacer(minLength: 0)
        }
        .frame(maxWidth: .infinity, alignment: .leading)
    }
}
