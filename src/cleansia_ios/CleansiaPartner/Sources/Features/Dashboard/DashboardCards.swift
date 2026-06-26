import CleansiaCore
import SwiftUI

struct WeeklyEarningsCard: View {
    let data: DashboardData
    let onClick: () -> Void

    var body: some View {
        Button(action: onClick) {
            HStack(alignment: .top, spacing: Spacing.s) {
                IconHalo(systemImage: "chart.line.uptrend.xyaxis")
                VStack(alignment: .leading, spacing: 2) {
                    HStack {
                        Text(L10n.Dashboard.earningsWeek)
                            .font(CleansiaTypography.labelMedium)
                            .foregroundColor(CleansiaColors.primary)
                        Spacer()
                        Text(jobsLine)
                            .font(CleansiaTypography.labelSmall)
                            .foregroundColor(CleansiaColors.onSurfaceVariant)
                    }
                    Text(money(data.weekEarnings))
                        .font(CleansiaTypography.titleLarge)
                        .foregroundColor(CleansiaColors.onSurface)
                        .lineLimit(1)
                    Text(subtitle)
                        .font(CleansiaTypography.labelSmall)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                        .lineLimit(1)
                    HStack(spacing: Spacing.xxs) {
                        Text(L10n.Dashboard.earningsViewDetails)
                            .font(CleansiaTypography.labelMedium)
                            .foregroundColor(CleansiaColors.primary)
                        Image(systemName: "arrow.right")
                            .font(.system(size: 12))
                            .foregroundColor(CleansiaColors.primary)
                    }
                    .padding(.top, Spacing.xs)
                }
            }
            .cardPadding()
        }
        .buttonStyle(.plain)
        .padding(.horizontal, Spacing.m)
    }

    private var jobsLine: String {
        data.weekCompletedCount == 0
            ? L10n.Dashboard.noCompletedYet
            : L10n.Dashboard.jobsDoneCount(data.weekCompletedCount)
    }

    private func money(_ amount: Double) -> String {
        DashboardFormat.money(amount, currencyCode: data.currencyCode)
    }

    private var subtitle: String {
        let today = data.todayEarnings > 0
            ? "\(L10n.Dashboard.earningsToday) \(money(data.todayEarnings))"
            : L10n.Dashboard.noJobsTodayShort
        guard data.averagePerJob > 0 else { return today }
        let avg = "\(L10n.Dashboard.avgPerJob) \(money(data.averagePerJob))"
        return "\(today)  ·  \(avg)"
    }
}

struct PayPeriodCard: View {
    let period: DashboardData.PayPeriod
    let currencyCode: String?
    let onClick: () -> Void

    private var progress: PayPeriodProgress {
        DashboardFormat.payPeriodProgress(start: period.start, end: period.end)
    }

    var body: some View {
        Button(action: onClick) {
            VStack(alignment: .leading, spacing: Spacing.xs) {
                HStack {
                    Text(L10n.Dashboard.currentPeriod)
                        .font(CleansiaTypography.labelMedium)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                    Spacer()
                    Text(L10n.Dashboard.payPeriodProgress(progress.day, progress.total))
                        .font(CleansiaTypography.labelSmall)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                }
                Text(DashboardFormat.plainMoney(period.earnings))
                    .font(CleansiaTypography.headlineMedium)
                    .foregroundColor(CleansiaColors.onSurface)
                ProgressView(value: progress.fraction)
                    .tint(CleansiaColors.primary)
                if let payout = period.nextPayoutDate {
                    Text(L10n.Dashboard.nextPayout(DashboardFormat.payoutDate(payout)))
                        .font(CleansiaTypography.labelSmall)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                }
            }
            .cardPadding()
        }
        .buttonStyle(.plain)
        .padding(.horizontal, Spacing.m)
    }
}

struct LastMonthCard: View {
    let data: DashboardData

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.m) {
            HStack {
                Text(L10n.Dashboard.lastMonthSection)
                    .font(CleansiaTypography.titleMedium)
                    .foregroundColor(CleansiaColors.onSurface)
                Spacer()
                if let delta = data.monthDeltaPercent {
                    MonthDeltaChip(percent: delta)
                }
            }
            HStack {
                MetricColumn(
                    value: DashboardFormat.plainMoney(data.lastMonthEarnings),
                    label: L10n.Dashboard.lastMonthEarnings
                )
                Spacer()
                MetricColumn(value: "\(data.lastMonthCompletedOrders)", label: L10n.Dashboard.lastMonthJobs)
                Spacer()
                RatingColumn(rating: data.averageRating, reviews: data.ratingCount)
            }
        }
        .cardPadding()
        .padding(.horizontal, Spacing.m)
    }
}

private struct MetricColumn: View {
    let value: String
    let label: String

    var body: some View {
        VStack(spacing: 2) {
            Text(value)
                .font(CleansiaTypography.titleLarge)
                .foregroundColor(CleansiaColors.onSurface)
            Text(label)
                .font(CleansiaTypography.labelSmall)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
        }
    }
}

private struct RatingColumn: View {
    let rating: Double?
    let reviews: Int

    private var caption: String {
        reviews > 0 ? L10n.Dashboard.ratingCount(reviews) : L10n.Dashboard.noRatingYet
    }

    var body: some View {
        VStack(spacing: 2) {
            HStack(spacing: Spacing.xxs) {
                Image(systemName: "star.fill")
                    .font(.system(size: 16))
                    .foregroundColor(CleansiaColors.primary)
                Text(DashboardFormat.rating(rating))
                    .font(CleansiaTypography.titleLarge)
                    .foregroundColor(CleansiaColors.onSurface)
            }
            Text(caption)
                .font(CleansiaTypography.labelSmall)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
        }
    }
}

private struct MonthDeltaChip: View {
    let percent: Int

    private var isUp: Bool {
        percent >= 0
    }

    private var color: Color {
        isUp ? CleansiaColors.primary : CleansiaColors.error
    }

    var body: some View {
        HStack(spacing: Spacing.xxs) {
            Image(systemName: isUp ? "arrow.up.right" : "arrow.down.right")
                .font(.system(size: 12))
            Text("\(isUp ? "+" : "")\(percent)%")
                .font(CleansiaTypography.labelSmall)
        }
        .foregroundColor(color)
        .padding(.horizontal, Spacing.xs)
        .padding(.vertical, Spacing.xxs)
        .background(color.opacity(0.12))
        .clipShape(Capsule())
    }
}

struct IconHalo: View {
    let systemImage: String

    var body: some View {
        Image(systemName: systemImage)
            .font(.system(size: 22))
            .foregroundColor(CleansiaColors.primary)
            .frame(width: 44, height: 44)
            .background(CleansiaColors.primaryContainer)
            .clipShape(Circle())
    }
}

extension View {
    func cardPadding() -> some View {
        padding(Spacing.m)
            .frame(maxWidth: .infinity, alignment: .leading)
            .background(CleansiaColors.surface)
            .clipShape(RoundedRectangle(cornerRadius: CornerRadius.medium))
            .overlay(
                RoundedRectangle(cornerRadius: CornerRadius.medium)
                    .stroke(CleansiaColors.outlineVariant, lineWidth: 1)
            )
    }
}
