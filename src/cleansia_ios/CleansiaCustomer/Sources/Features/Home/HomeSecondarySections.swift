import CleansiaCore
import CleansiaCustomerApi
import SwiftUI

/// The 3 most recent bookings with a See-all link (`RecentBookingsSection`,
/// `HomeTab.kt:932-1055`).
struct RecentBookingsSection: View {
    @Environment(\.locale) private var locale
    let orders: [OrderListItem]
    let onOrderTap: (String) -> Void
    let onSeeAll: () -> Void

    var body: some View {
        VStack(alignment: .leading, spacing: 10) {
            HStack {
                HomeSectionTitle(text: L10n.Home.recentTitle)
                Spacer()
                Button(action: onSeeAll) {
                    Text(L10n.Home.recentSeeAll)
                        .font(CleansiaTypography.labelLarge)
                        .foregroundColor(CleansiaColors.primary)
                }
            }
            VStack(spacing: Spacing.xs) {
                ForEach(orders, id: \.id) { order in
                    RecentBookingRow(order: order) {
                        if let id = order.id { onOrderTap(id) }
                    }
                }
            }
        }
        .id(locale.identifier)
    }
}

private struct RecentBookingRow: View {
    @Environment(\.locale) private var locale
    let order: OrderListItem
    let onTap: () -> Void

    var body: some View {
        Button(action: onTap) {
            HStack(spacing: Spacing.s) {
                ZStack {
                    Circle()
                        .fill(CleansiaColors.primaryContainer)
                        .frame(width: 40, height: 40)
                    Image(systemName: "bubbles.and.sparkles")
                        .font(.system(size: 18))
                        .foregroundColor(CleansiaColors.primary)
                }
                VStack(alignment: .leading, spacing: Spacing.hair) {
                    HStack(spacing: Spacing.xs) {
                        Text(HomeSections.recentBookingTitle(
                            order,
                            fallback: L10n.Home.recentFallbackTitle,
                            languageCode: CatalogLocalization.languageCode(for: locale)
                        ))
                        .font(CleansiaTypography.titleMedium)
                        .foregroundColor(CleansiaColors.onSurface)
                        .lineLimit(1)
                        if let label = HomeSections.statusChipLabel(order) {
                            OrderStatusPill(label: label, color: OrderStatusPresentation.color(order.orderStatus))
                        }
                        Spacer(minLength: 0)
                    }
                    Text(recentSubtitle)
                        .font(CleansiaTypography.bodyMedium)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                }
                Image(systemName: "chevron.right")
                    .font(.system(size: 13, weight: .semibold))
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
            }
            .padding(14)
            .frame(maxWidth: .infinity, alignment: .leading)
            .background(CleansiaColors.surface, in: RoundedRectangle(cornerRadius: 14))
            .overlay(
                RoundedRectangle(cornerRadius: 14)
                    .stroke(CleansiaColors.outlineVariant, lineWidth: 1)
            )
        }
        .buttonStyle(.plain)
    }

    private var recentSubtitle: String {
        let when = OrdersFormat.dateTime(order.cleaningDateTime, locale: locale)
        let price = OrdersFormat.price(order.totalPrice ?? 0, currencyCode: order.currency?.code)
        return "\(when) · \(price)"
    }
}

/// Loyalty-tier progress toward the next tier (`MilestoneProgressCard`,
/// `HomeTab.kt:1074-1135`). Callers gate on `HomeSections.showMilestone`; the
/// body double-checks so it is safe to render directly.
struct MilestoneProgressCard: View {
    @Environment(\.locale) private var locale
    let account: LoyaltyAccount

    var body: some View {
        if let nextTier = LoyaltyTier(value: account.nextTier), let pointsToNext = account.pointsToNextTier {
            card(nextTier: nextTier, pointsToNext: pointsToNext)
                .id(locale.identifier)
        }
    }

    private func card(nextTier: LoyaltyTier, pointsToNext: Int) -> some View {
        let currentTier = LoyaltyTier(value: account.currentTier) ?? .bronzeCleaner
        let target = account.lifetimePoints + pointsToNext
        let progress = target > 0 ? min(max(Double(account.lifetimePoints) / Double(target), 0), 1) : 0

        return VStack(alignment: .leading, spacing: Spacing.xs) {
            HStack(spacing: Spacing.xs) {
                Image(systemName: "star")
                    .font(.system(size: 18))
                    .foregroundColor(CleansiaColors.warningStar)
                Text(L10n.Home.milestoneTitle(L10n.Rewards.tierLabel(currentTier)))
                    .font(CleansiaTypography.titleMedium)
                    .foregroundColor(CleansiaColors.onBackground)
                Spacer(minLength: Spacing.xs)
                Text(verbatim: "\(account.lifetimePoints)/\(target)")
                    .font(CleansiaTypography.labelLarge)
                    .foregroundColor(CleansiaColors.onBackground)
            }
            progressBar(progress)
            Text(L10n.Home.milestoneSubtitle(pointsToNext, L10n.Rewards.tierLabel(nextTier)))
                .font(CleansiaTypography.bodyMedium)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
        }
        .padding(Spacing.m)
        .frame(maxWidth: .infinity, alignment: .leading)
        .background(
            CleansiaColors.tertiaryContainer.opacity(0.4),
            in: RoundedRectangle(cornerRadius: 18)
        )
    }

    private func progressBar(_ progress: Double) -> some View {
        GeometryReader { geo in
            ZStack(alignment: .leading) {
                Capsule().fill(CleansiaColors.outlineVariant)
                Capsule()
                    .fill(CleansiaColors.warningStar)
                    .frame(width: geo.size.width * progress)
            }
        }
        .frame(height: 6)
    }
}

/// Static seasonal suggestion routing into the booking flow (`SeasonalCard`,
/// `HomeTab.kt:1140-1184`).
struct SeasonalCard: View {
    @Environment(\.locale) private var locale
    let onTap: () -> Void

    var body: some View {
        Button(action: onTap) {
            HStack(spacing: Spacing.s) {
                ZStack {
                    Circle()
                        .fill(CleansiaColors.secondary.opacity(0.15))
                        .frame(width: 44, height: 44)
                    Image(systemName: "calendar")
                        .font(.system(size: 20))
                        .foregroundColor(CleansiaColors.secondary)
                }
                VStack(alignment: .leading, spacing: Spacing.hair) {
                    Text(L10n.Home.seasonalTitle)
                        .font(CleansiaTypography.titleMedium)
                        .foregroundColor(CleansiaColors.onBackground)
                    Text(L10n.Home.seasonalSubtitle)
                        .font(CleansiaTypography.bodyMedium)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                        .multilineTextAlignment(.leading)
                }
                Spacer(minLength: Spacing.xs)
                Image(systemName: "arrow.right")
                    .font(.system(size: 16, weight: .semibold))
                    .foregroundColor(CleansiaColors.secondary)
            }
            .padding(Spacing.m)
            .frame(maxWidth: .infinity, alignment: .leading)
            .background(
                CleansiaColors.secondaryContainer.opacity(0.5),
                in: RoundedRectangle(cornerRadius: 18)
            )
        }
        .buttonStyle(.plain)
        .id(locale.identifier)
    }
}

/// First-paint placeholder mirroring the real layout's shapes
/// (`HomeSkeleton`, `HomeTab.kt:1208-1297`) with the same subtle pulse.
struct HomeSkeleton: View {
    @State private var pulsing = false

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            HStack(spacing: Spacing.s) {
                block(height: 40, radius: CornerRadius.extraSmall)
                Circle()
                    .fill(blockColor)
                    .frame(width: 40, height: 40)
            }
            .padding(.top, Spacing.m)
            .padding(.bottom, Spacing.s)

            block(height: 180, radius: 22)
            Spacer().frame(height: 28)

            block(height: 72, radius: CornerRadius.medium)
            Spacer().frame(height: 28)

            block(height: 20, radius: CornerRadius.extraSmall)
                .frame(width: 160)
            Spacer().frame(height: Spacing.s)
            HStack(spacing: 10) {
                ForEach(0 ..< 3, id: \.self) { _ in
                    block(height: 110, radius: 18)
                }
            }
            Spacer()
        }
        .padding(.horizontal, Spacing.ml)
        .onAppear {
            withAnimation(.easeInOut(duration: 0.9).repeatForever(autoreverses: true)) {
                pulsing = true
            }
        }
    }

    private var blockColor: Color {
        CleansiaColors.outlineVariant.opacity(pulsing ? 0.6 : 0.3)
    }

    private func block(height: CGFloat, radius: CGFloat) -> some View {
        RoundedRectangle(cornerRadius: radius)
            .fill(blockColor)
            .frame(maxWidth: .infinity)
            .frame(height: height)
    }
}

#if DEBUG
    struct HomeSecondarySections_Previews: PreviewProvider {
        static var sampleOrder: OrderListItem {
            OrderListItem(
                id: "o1",
                cleaningDateTime: Date(),
                totalPrice: 1290,
                orderStatus: Code(type: "OrderStatus", name: "Completed", value: 5),
                selectedPackages: [PackageListItem(id: "p1", name: "Standard cleaning")],
                currency: CurrencyListItem(code: "CZK"),
                selectedServices: [ServiceListItem(id: "s1", name: "Deep clean")]
            )
        }

        static var sampleAccount: LoyaltyAccount {
            LoyaltyAccount(
                currentTier: 1,
                lifetimePoints: 120,
                completedBookingsCount: 3,
                tierAchievedOn: nil,
                pointsToNextTier: 180,
                nextTier: 2,
                currentDiscountPercent: 0,
                currentDiscountMinOrderAmount: nil,
                currentPerks: []
            )
        }

        static var previews: some View {
            ScrollView {
                VStack(alignment: .leading, spacing: Spacing.l) {
                    RecentBookingsSection(orders: [sampleOrder], onOrderTap: { _ in }, onSeeAll: {})
                    MilestoneProgressCard(account: sampleAccount)
                    SeasonalCard(onTap: {})
                }
                .padding(Spacing.ml)
            }
            .background(CleansiaColors.background)
            .previewDisplayName("Recent + milestone + seasonal")

            HomeSkeleton()
                .background(CleansiaColors.background)
                .previewDisplayName("Skeleton")
        }
    }
#endif
