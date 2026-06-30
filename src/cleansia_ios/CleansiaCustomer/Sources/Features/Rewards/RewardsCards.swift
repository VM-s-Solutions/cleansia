import CleansiaCore
import SwiftUI

struct TierLadderCard: View {
    let tiers: [TierInfo]
    let current: LoyaltyTier

    private var sorted: [TierInfo] {
        tiers.sorted { $0.tier < $1.tier }
    }

    var body: some View {
        RewardsCard {
            Text(L10n.Rewards.tierLadderTitle)
                .font(CleansiaTypography.titleMedium)
                .foregroundColor(CleansiaColors.onBackground)
            if sorted.isEmpty {
                Text(L10n.Rewards.errorLoad)
                    .font(CleansiaTypography.bodyMedium)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
            } else {
                ForEach(sorted, id: \.tier) { tierInfo in
                    if let tier = LoyaltyTier(value: tierInfo.tier) {
                        TierLadderRow(tierInfo: tierInfo, tier: tier, current: current)
                    }
                }
            }
        }
    }
}

private struct TierLadderRow: View {
    let tierInfo: TierInfo
    let tier: LoyaltyTier
    let current: LoyaltyTier

    private var discountText: String {
        switch LoyaltyPresentation.discountSummary(tierInfo) {
        case .noDiscount: L10n.Rewards.noDiscountYet
        case let .basic(percent): L10n.Rewards.discountBasic(percent)
        case let .minOrder(percent, minOrder): L10n.Rewards.discountMinOrder(percent, minOrder)
        }
    }

    var body: some View {
        HStack(spacing: Spacing.s) {
            Image(systemName: RewardsTierStyle.icon(tier))
                .font(.system(size: 18))
                .foregroundColor(.white)
                .frame(width: 40, height: 40)
                .background(
                    LinearGradient(
                        colors: RewardsTierStyle.gradient(tier),
                        startPoint: .topLeading,
                        endPoint: .bottomTrailing
                    ),
                    in: Circle()
                )
            VStack(alignment: .leading, spacing: Spacing.hair) {
                HStack(spacing: Spacing.xxs) {
                    Text(L10n.Rewards.tierLabel(tier))
                        .font(CleansiaTypography.titleMedium)
                        .foregroundColor(CleansiaColors.onSurface)
                    Text(verbatim: "·")
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                    Text(L10n.Rewards.thresholdPoints(tierInfo.lifetimePointsThreshold))
                        .font(CleansiaTypography.labelSmall)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                }
                Text(discountText)
                    .font(CleansiaTypography.bodyMedium)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
            }
            Spacer()
            TierStatusBadge(status: LoyaltyPresentation.status(for: tier, current: current))
        }
    }
}

private struct TierStatusBadge: View {
    let status: TierStatus

    var body: some View {
        switch status {
        case .current:
            Text(L10n.Rewards.statusCurrent)
                .font(CleansiaTypography.labelSmall)
                .foregroundColor(CleansiaColors.primary)
                .padding(.horizontal, Spacing.xs)
                .padding(.vertical, Spacing.xxs)
                .background(CleansiaColors.primary.opacity(0.14), in: Capsule())
        case .unlocked:
            Image(systemName: "checkmark.circle.fill")
                .font(.system(size: 20))
                .foregroundColor(CleansiaColors.successText)
                .accessibilityLabel(Text(L10n.Rewards.statusUnlocked))
        case .locked:
            Image(systemName: "lock.fill")
                .font(.system(size: 18))
                .foregroundColor(CleansiaColors.onSurfaceVariant)
                .accessibilityLabel(Text(L10n.Rewards.statusLocked))
        }
    }
}

struct InviteFriendsCard: View {
    let referral: ReferralAccount
    let onCopyCode: (String) -> Void

    private var statsLine: String {
        switch LoyaltyPresentation.referralStats(accepted: referral.acceptedCount, qualified: referral.qualifiedCount) {
        case .empty: L10n.Rewards.referralStatsEmpty
        case let .waiting(accepted): L10n.Rewards.referralStatsWaiting(accepted)
        case let .qualified(accepted, qualified): L10n.Rewards.referralStatsQualified(accepted, qualified)
        }
    }

    var body: some View {
        RewardsCard {
            HStack(spacing: Spacing.s) {
                Image(systemName: "gift")
                    .font(.system(size: 18))
                    .foregroundColor(CleansiaColors.primary)
                    .frame(width: 36, height: 36)
                    .background(CleansiaColors.primary.opacity(0.14), in: Circle())
                Text(L10n.Rewards.referralSectionTitle)
                    .font(CleansiaTypography.titleMedium)
                    .foregroundColor(CleansiaColors.onBackground)
            }
            Text(L10n.Rewards.referralSubtitle)
                .font(CleansiaTypography.bodyMedium)
                .foregroundColor(CleansiaColors.onSurfaceVariant)

            HStack(spacing: Spacing.s) {
                Button {
                    onCopyCode(referral.code)
                } label: {
                    Text(referral.code)
                        .font(CleansiaTypography.headlineSmall)
                        .foregroundColor(CleansiaColors.primary)
                        .frame(maxWidth: .infinity)
                        .padding(.vertical, Spacing.m)
                        .background(
                            CleansiaColors.primary.opacity(0.10),
                            in: RoundedRectangle(cornerRadius: CornerRadius.small)
                        )
                        .overlay(
                            RoundedRectangle(cornerRadius: CornerRadius.small)
                                .stroke(CleansiaColors.primary.opacity(0.30), lineWidth: 1)
                        )
                }
                .buttonStyle(.plain)

                Button {
                    onCopyCode(referral.code)
                } label: {
                    Image(systemName: "doc.on.doc")
                        .font(.system(size: 18))
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                        .frame(width: 48, height: 48)
                        .background(
                            CleansiaColors.surfaceVariant,
                            in: RoundedRectangle(cornerRadius: CornerRadius.small)
                        )
                }
                .buttonStyle(.plain)
                .accessibilityLabel(Text(L10n.Rewards.referralCopyButton))
            }

            Text(statsLine)
                .font(CleansiaTypography.bodyMedium)
                .foregroundColor(CleansiaColors.onSurfaceVariant)

            ShareLink(item: RewardsShare.message(code: referral.code)) {
                HStack(spacing: Spacing.xs) {
                    Image(systemName: "square.and.arrow.up")
                        .font(.system(size: 16))
                    Text(L10n.Rewards.referralShareButton)
                        .font(CleansiaTypography.titleMedium)
                }
                .foregroundColor(CleansiaColors.onPrimary)
                .frame(maxWidth: .infinity, minHeight: 48)
                .background(CleansiaColors.primary, in: Capsule())
            }
        }
    }
}

struct ActivityPreviewCard: View {
    let activity: [LoyaltyActivityItem]
    let onOpenActivity: () -> Void

    var body: some View {
        RewardsCard {
            Text(L10n.Rewards.activityTitle)
                .font(CleansiaTypography.titleMedium)
                .foregroundColor(CleansiaColors.onBackground)
            if activity.isEmpty {
                Text(L10n.Rewards.emptyActivity)
                    .font(CleansiaTypography.bodyMedium)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
            } else {
                ForEach(activity) { item in
                    RewardsActivityRow(item: item)
                }
                Button(action: onOpenActivity) {
                    Text(L10n.Rewards.activityViewAll)
                        .font(CleansiaTypography.labelLarge)
                        .foregroundColor(CleansiaColors.primary)
                }
                .buttonStyle(.plain)
                .padding(.top, Spacing.xxs)
            }
        }
    }
}
