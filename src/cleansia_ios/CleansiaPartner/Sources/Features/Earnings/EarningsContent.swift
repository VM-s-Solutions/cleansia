import CleansiaCore
import CleansiaPartnerApi
import SwiftUI

struct EarningsContent: View {
    let stats: DashboardStats
    let cashHeld: [CashHeld]
    let onOpenInvoices: () -> Void

    var body: some View {
        ScrollView {
            VStack(spacing: Spacing.m) {
                HeadlineEarningsCard(stats: stats)
                BreakdownGrid(stats: stats)
                if let period = PayPeriodWindow(stats: stats) {
                    PayPeriodCardView(period: period)
                }
                if !cashHeld.isEmpty {
                    CashHeldCard(cashHeld: cashHeld)
                }
                InvoicesEntryCard(onClick: onOpenInvoices)
            }
            .padding(.horizontal, Spacing.m)
            .padding(.vertical, Spacing.m)
        }
    }
}

private struct HeadlineEarningsCard: View {
    let stats: DashboardStats

    private var isEstimate: Bool {
        stats.latestInvoiceStatus?.isEmpty ?? true
    }

    var body: some View {
        HStack(spacing: Spacing.m) {
            IconHalo(systemImage: "creditcard")
            VStack(alignment: .leading, spacing: Spacing.hair) {
                Text(L10n.Earnings.currentPeriod)
                    .font(CleansiaTypography.labelMedium)
                    .foregroundColor(CleansiaColors.primary)
                Text(EarningsFormat.wholeMoney(stats.currentPeriodEarnings, currencyCode: stats.currencyCode))
                    .cleansiaFont(CleansiaTypography.headlineMedium)
                    .foregroundColor(CleansiaColors.onSurface)
                if isEstimate {
                    Text(L10n.Earnings.estimateHelper)
                        .font(CleansiaTypography.labelSmall)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                }
            }
            Spacer(minLength: 0)
        }
        .cardPadding()
    }
}

private struct BreakdownGrid: View {
    let stats: DashboardStats

    var body: some View {
        VStack(spacing: 0) {
            BreakdownRow(
                label: L10n.Earnings.today,
                value: money(stats.todayEarnings),
                secondary: L10n.Earnings.jobsDoneCount(stats.todayCompletedCount)
            )
            EarningsDivider()
            BreakdownRow(
                label: L10n.Earnings.thisWeek,
                value: money(stats.weekEarnings),
                secondary: L10n.Earnings.jobsDoneCount(stats.weekCompletedCount)
            )
            EarningsDivider()
            BreakdownRow(
                label: L10n.Earnings.lastMonth,
                value: money(stats.lastMonthEarnings),
                secondary: L10n.Earnings.jobsDoneCount(stats.lastMonthCompletedOrders)
            )
        }
        .cardPadding()
    }

    private func money(_ amount: Double) -> String {
        EarningsFormat.wholeMoney(amount, currencyCode: stats.currencyCode)
    }
}

private struct BreakdownRow: View {
    let label: String
    let value: String
    let secondary: String

    var body: some View {
        HStack(alignment: .center) {
            VStack(alignment: .leading, spacing: Spacing.hair) {
                Text(label)
                    .font(CleansiaTypography.bodyLarge)
                    .foregroundColor(CleansiaColors.onSurface)
                Text(secondary)
                    .font(CleansiaTypography.labelSmall)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
            }
            Spacer()
            Text(value)
                .font(CleansiaTypography.titleMedium)
                .foregroundColor(CleansiaColors.onSurface)
        }
        .padding(.vertical, Spacing.s)
    }
}

struct PayPeriodWindow {
    let start: Date
    let end: Date
    let nextPayout: Date?
    let currencyCode: String?

    init?(stats: DashboardStats) {
        guard let start = stats.currentPayPeriodStart, let end = stats.currentPayPeriodEnd else { return nil }
        self.start = start
        self.end = end
        nextPayout = stats.nextPayoutDate
        currencyCode = stats.currencyCode
    }

    var daysRemaining: Int {
        let days = Calendar.current.dateComponents(
            [.day],
            from: Calendar.current.startOfDay(for: Date()),
            to: Calendar.current.startOfDay(for: end)
        ).day ?? 0
        return max(days, 0)
    }
}

private struct PayPeriodCardView: View {
    @Environment(\.locale) private var locale
    let period: PayPeriodWindow

    var body: some View {
        VStack(spacing: 0) {
            HStack(spacing: Spacing.m) {
                IconHalo(systemImage: "calendar")
                VStack(alignment: .leading, spacing: Spacing.hair) {
                    Text(L10n.Earnings.payPeriod)
                        .font(CleansiaTypography.labelMedium)
                        .foregroundColor(CleansiaColors.primary)
                    Text(rangeLine)
                        .font(CleansiaTypography.titleMedium)
                        .foregroundColor(CleansiaColors.onSurface)
                    Text(L10n.Earnings.daysRemaining(period.daysRemaining))
                        .font(CleansiaTypography.labelSmall)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                }
                Spacer(minLength: 0)
            }
            if let payout = period.nextPayout {
                EarningsDivider().padding(.vertical, Spacing.m)
                HStack(spacing: Spacing.xs) {
                    Image(systemName: "checkmark.circle")
                        .font(.system(size: 18))
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                    Text(L10n.Earnings.nextPayout)
                        .font(CleansiaTypography.bodyMedium)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                    Spacer()
                    Text(EarningsFormat.shortDate(payout, locale: locale) ?? "")
                        .font(CleansiaTypography.bodyMedium)
                        .foregroundColor(CleansiaColors.onSurface)
                }
            }
        }
        .cardPadding()
    }

    private var rangeLine: String {
        let start = EarningsFormat.shortDate(period.start, locale: locale) ?? ""
        let end = EarningsFormat.shortDate(period.end, locale: locale) ?? ""
        return "\(start) – \(end)"
    }
}

/// The company cash the cleaner took at the door and has not handed over, per currency. Above the
/// company's cap the server keeps cash jobs off their board, and this card is where they read why.
private struct CashHeldCard: View {
    let cashHeld: [CashHeld]

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            HStack(spacing: Spacing.m) {
                IconHalo(systemImage: "banknote")
                VStack(alignment: .leading, spacing: Spacing.hair) {
                    Text(L10n.Earnings.cashHeldTitle)
                        .font(CleansiaTypography.labelMedium)
                        .foregroundColor(CleansiaColors.primary)
                    Text(L10n.Earnings.cashHeldSubtitle)
                        .font(CleansiaTypography.labelSmall)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                }
                Spacer(minLength: 0)
            }
            ForEach(Array(cashHeld.enumerated()), id: \.offset) { _, held in
                EarningsDivider().padding(.vertical, Spacing.m)
                HStack(alignment: .firstTextBaseline) {
                    Text(EarningsFormat.decimalMoney(held.amount, currencyCode: held.currencyCode))
                        .font(CleansiaTypography.titleMedium)
                        .foregroundColor(CleansiaColors.onSurface)
                    Spacer()
                    if let cap = held.floatCap {
                        Text(L10n.Earnings.cashHeldLimit(
                            EarningsFormat.decimalMoney(cap, currencyCode: held.currencyCode)
                        ))
                        .font(CleansiaTypography.labelSmall)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                    }
                }
                if held.cashJobsHidden {
                    Text(L10n.Earnings.cashHeldJobsHidden)
                        .font(CleansiaTypography.labelSmall)
                        .foregroundColor(CleansiaColors.error)
                        .padding(.top, Spacing.xs)
                }
            }
        }
        .cardPadding()
    }
}

private struct InvoicesEntryCard: View {
    let onClick: () -> Void

    var body: some View {
        Button(action: onClick) {
            HStack(spacing: Spacing.m) {
                IconHalo(systemImage: "doc.text")
                VStack(alignment: .leading, spacing: Spacing.hair) {
                    Text(L10n.Earnings.viewInvoices)
                        .font(CleansiaTypography.bodyLarge)
                        .foregroundColor(CleansiaColors.onSurface)
                    Text(L10n.Earnings.viewInvoicesSubtitle)
                        .font(CleansiaTypography.labelSmall)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                }
                Spacer(minLength: 0)
                Image(systemName: "chevron.right")
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
            }
            .cardPadding()
        }
        .buttonStyle(.plain)
    }
}

struct EarningsDivider: View {
    var body: some View {
        Rectangle()
            .fill(CleansiaColors.outlineVariant.opacity(0.5))
            .frame(height: 1)
            .frame(maxWidth: .infinity)
    }
}
