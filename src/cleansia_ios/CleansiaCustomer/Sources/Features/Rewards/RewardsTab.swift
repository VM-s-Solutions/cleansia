import CleansiaCore
import SwiftUI

struct RewardsTab: View {
    @StateObject private var vm: RewardsViewModel
    @Environment(\.snackbarController) private var snackbar
    // Re-localize on a runtime language switch: the reward cards take value-type inputs, so SwiftUI's
    // equality check skips re-invoking their body when only the bundle is repointed. Stamping the
    // locale identity forces the subtree to rebuild from cached data in the new language — the app's
    // standard pattern (see HomeTab). Without it, only the title (in this body) updated.
    @Environment(\.locale) private var locale
    let onOpenActivity: () -> Void

    init(
        loyaltyRepository: LoyaltyRepository,
        referralRepository: RewardsReferralRepository,
        snackbar: SnackbarController,
        onOpenActivity: @escaping () -> Void
    ) {
        _vm = StateObject(wrappedValue: RewardsViewModel(
            loyaltyRepository: loyaltyRepository,
            referralRepository: referralRepository,
            snackbar: snackbar
        ))
        self.onOpenActivity = onOpenActivity
    }

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            Text(L10n.Rewards.title)
                .font(CleansiaTypography.headlineMedium)
                .foregroundColor(CleansiaColors.onBackground)
                .padding(.horizontal, Spacing.ml)
                .padding(.vertical, Spacing.m)

            // .id on the content (not the outer VStack) so the cards rebuild from cached data in the
            // new language without restarting the .task / re-fetching.
            content
                .id(locale.identifier)
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .top)
        .background(CleansiaColors.background.ignoresSafeArea())
        .task { await vm.load() }
    }

    @ViewBuilder
    private var content: some View {
        switch vm.state {
        case .loading:
            ProgressView()
                .tint(CleansiaColors.primary)
                .frame(maxWidth: .infinity, maxHeight: .infinity)
        case .error:
            ScrollView {
                RewardsStateMessage(
                    systemImage: "wifi.slash",
                    message: L10n.Rewards.errorLoad
                ) { Task { await vm.refresh() } }
                    .frame(maxWidth: .infinity, minHeight: 360)
            }
            .refreshable { await vm.refresh() }
        case let .loaded(content):
            RewardsContentView(
                content: content,
                onCopyCode: copyCode,
                onOpenActivity: onOpenActivity
            )
            .refreshable { await vm.refresh() }
        }
    }

    private func copyCode(_ code: String) {
        UIPasteboard.general.string = code
        snackbar.showSuccess(L10n.Rewards.referralCopiedToast)
    }
}

struct RewardsContentView: View {
    let content: RewardsContent
    let onCopyCode: (String) -> Void
    let onOpenActivity: () -> Void

    private var currentTier: LoyaltyTier {
        LoyaltyTier(value: content.account.currentTier) ?? .bronzeCleaner
    }

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: Spacing.m) {
                TierHeroCard(tier: currentTier, account: content.account)
                ProgressCard(account: content.account)
                CurrentPerksCard(perks: content.account.currentPerks)
                TierLadderCard(tiers: content.tiers, current: currentTier)

                if let referral = content.referral, !referral.code.isEmpty {
                    InviteFriendsCard(referral: referral, onCopyCode: onCopyCode)
                }

                ActivityPreviewCard(activity: content.activityPreview, onOpenActivity: onOpenActivity)
            }
            .padding(.horizontal, Spacing.ml)
        }
    }
}

private struct TierHeroCard: View {
    let tier: LoyaltyTier
    let account: LoyaltyAccount

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.m) {
            HStack(spacing: Spacing.m) {
                Image(systemName: RewardsTierStyle.icon(tier))
                    .font(.system(size: 30))
                    .foregroundColor(.white)
                    .frame(width: 64, height: 64)
                    .background(Color.white.opacity(0.22), in: Circle())
                VStack(alignment: .leading, spacing: Spacing.hair) {
                    Text(L10n.Rewards.tierLabel(tier))
                        .font(CleansiaTypography.titleLarge)
                        .foregroundColor(.white)
                    Text(L10n.Rewards.lifetimePoints)
                        .font(CleansiaTypography.labelMedium)
                        .foregroundColor(.white.opacity(0.85))
                }
            }
            HStack(alignment: .lastTextBaseline, spacing: Spacing.xs) {
                Text(verbatim: "\(account.lifetimePoints)")
                    .font(CleansiaTypography.displayMedium)
                    .foregroundColor(.white)
                Text(L10n.Rewards.pointsUnit)
                    .font(CleansiaTypography.titleMedium)
                    .foregroundColor(.white.opacity(0.9))
            }
            Text(L10n.Rewards.bookingsCompleted(account.completedBookingsCount))
                .font(CleansiaTypography.bodyMedium)
                .foregroundColor(.white.opacity(0.85))
        }
        .padding(Spacing.ml)
        .frame(maxWidth: .infinity, alignment: .leading)
        .background(
            LinearGradient(
                colors: RewardsTierStyle.gradient(tier),
                startPoint: .topLeading,
                endPoint: .bottomTrailing
            ),
            in: RoundedRectangle(cornerRadius: CornerRadius.large)
        )
    }
}

private struct ProgressCard: View {
    let account: LoyaltyAccount

    var body: some View {
        RewardsCard {
            if let threshold = LoyaltyPresentation.nextThreshold(account),
               let progress = LoyaltyPresentation.progressFraction(account),
               let nextTier = LoyaltyTier(value: account.nextTier)
            {
                Text(L10n.Rewards.progressToNext(
                    account.lifetimePoints,
                    threshold,
                    L10n.Rewards.tierLabel(nextTier)
                ))
                .font(CleansiaTypography.bodyMedium)
                .foregroundColor(CleansiaColors.onSurface)
                ProgressView(value: progress)
                    .tint(CleansiaColors.primary)
            } else {
                Label(L10n.Rewards.maxTierReached, systemImage: "star.fill")
                    .font(CleansiaTypography.titleMedium)
                    .foregroundColor(CleansiaColors.onSurface)
            }
        }
    }
}

private struct CurrentPerksCard: View {
    let perks: [TierPerk]

    var body: some View {
        RewardsCard {
            Text(L10n.Rewards.currentPerksTitle)
                .font(CleansiaTypography.titleMedium)
                .foregroundColor(CleansiaColors.onBackground)
            ForEach(Array(LoyaltyPresentation.effectivePerks(perks).enumerated()), id: \.offset) { _, perk in
                HStack(spacing: Spacing.s) {
                    Image(systemName: "checkmark.circle.fill")
                        .font(.system(size: 18))
                        .foregroundColor(CleansiaColors.primary)
                    Text(L10n.Rewards.perkLabel(perk.labelKey))
                        .font(CleansiaTypography.bodyMedium)
                        .foregroundColor(CleansiaColors.onSurface)
                }
            }
        }
    }
}

struct RewardsCard<Content: View>: View {
    @ViewBuilder let content: Content

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.s) {
            content
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .padding(Spacing.m)
        .background(CleansiaColors.surface, in: RoundedRectangle(cornerRadius: CornerRadius.medium))
        .overlay(
            RoundedRectangle(cornerRadius: CornerRadius.medium)
                .stroke(CleansiaColors.outlineVariant, lineWidth: 1)
        )
    }
}

enum RewardsTierStyle {
    static func icon(_ tier: LoyaltyTier) -> String {
        switch tier {
        case .bronzeCleaner: "square.stack.3d.up"
        case .silverMopper: "medal"
        case .goldPolisher: "trophy"
        case .platinumSparkler: "diamond"
        }
    }

    static func gradient(_ tier: LoyaltyTier) -> [Color] {
        switch tier {
        case .bronzeCleaner: [Color(red: 0.57, green: 0.25, blue: 0.05), Color(red: 0.76, green: 0.40, blue: 0.10)]
        case .silverMopper: [Color(red: 0.28, green: 0.33, blue: 0.41), Color(red: 0.58, green: 0.64, blue: 0.72)]
        case .goldPolisher: [Color(red: 0.71, green: 0.33, blue: 0.04), Color(red: 0.96, green: 0.62, blue: 0.04)]
        case .platinumSparkler: [Color(red: 0.43, green: 0.16, blue: 0.85), Color(red: 0.66, green: 0.55, blue: 0.98)]
        }
    }
}

#if DEBUG
    struct RewardsContentView_Previews: PreviewProvider {
        static var previews: some View {
            RewardsContentView(
                content: RewardsContent(
                    account: LoyaltyAccount(
                        currentTier: 2,
                        lifetimePoints: 600,
                        completedBookingsCount: 6,
                        tierAchievedOn: nil,
                        pointsToNextTier: 400,
                        nextTier: 3,
                        currentDiscountPercent: 0.05,
                        currentDiscountMinOrderAmount: 1000,
                        currentPerks: [TierPerk(icon: nil, labelKey: "loyalty.perks.priority_support")]
                    ),
                    tiers: [
                        TierInfo(
                            tier: 1,
                            lifetimePointsThreshold: 0,
                            discountPercent: 0,
                            minimumOrderAmountForDiscount: nil,
                            perks: []
                        ),
                        TierInfo(
                            tier: 2,
                            lifetimePointsThreshold: 500,
                            discountPercent: 0.05,
                            minimumOrderAmountForDiscount: 1000,
                            perks: []
                        ),
                        TierInfo(
                            tier: 3,
                            lifetimePointsThreshold: 1000,
                            discountPercent: 0.10,
                            minimumOrderAmountForDiscount: nil,
                            perks: []
                        )
                    ],
                    referral: ReferralAccount(
                        code: "ABC123",
                        timesUsed: 0,
                        qualifiedCount: 1,
                        acceptedCount: 2,
                        pointsPerReferral: 150
                    ),
                    activityPreview: [LoyaltyActivityItem(
                        type: 1,
                        points: 100,
                        source: 1,
                        orderId: "o1",
                        orderDisplayNumber: "1042",
                        occurredOn: Date()
                    )]
                ),
                onCopyCode: { _ in },
                onOpenActivity: {}
            )
            .background(CleansiaColors.background)
        }
    }
#endif
