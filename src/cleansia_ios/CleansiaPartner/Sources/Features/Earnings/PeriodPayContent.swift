import CleansiaCore
import CleansiaPartnerApi
import SwiftUI

struct PeriodPayContent: View {
    let summary: PeriodPaySummaryDto
    let currencyCode: String?

    var body: some View {
        ScrollView {
            VStack(spacing: Spacing.m) {
                HeroCard(summary: summary, currencyCode: currencyCode)
                BreakdownCard(summary: summary, currencyCode: currencyCode)
                JobsCard(orderPays: summary.orderPays ?? [], currencyCode: currencyCode)
            }
            .padding(.horizontal, Spacing.m)
            .padding(.vertical, Spacing.m)
        }
    }
}

private struct HeroCard: View {
    let summary: PeriodPaySummaryDto
    let currencyCode: String?

    var body: some View {
        HStack(spacing: Spacing.m) {
            IconHalo(systemImage: "creditcard")
            VStack(alignment: .leading, spacing: Spacing.hair) {
                Text(L10n.PeriodPay.heroLabel)
                    .font(CleansiaTypography.labelMedium)
                    .foregroundColor(CleansiaColors.primary)
                Text(EarningsFormat.decimalMoney(summary.grandTotal ?? 0, currencyCode: currencyCode))
                    .font(CleansiaTypography.headlineMedium)
                    .foregroundColor(CleansiaColors.onSurface)
                if let label = summary.payPeriodLabel, !label.isEmpty {
                    Text(label)
                        .font(CleansiaTypography.labelSmall)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                }
                if let count = summary.totalOrders, count > 0 {
                    Text(L10n.PeriodPay.jobsCount(count))
                        .font(CleansiaTypography.labelSmall)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                }
            }
            Spacer(minLength: 0)
        }
        .cardPadding()
    }
}

private struct BreakdownCard: View {
    let summary: PeriodPaySummaryDto
    let currencyCode: String?

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.xs) {
            Text(L10n.PeriodPay.breakdownSection)
                .font(CleansiaTypography.labelMedium)
                .foregroundColor(CleansiaColors.primary)

            MoneyRow(label: L10n.PeriodPay.base, amount: summary.totalBasePay ?? 0, currencyCode: currencyCode)
            if let extras = summary.totalExtrasPay, extras != 0 {
                MoneyRow(label: L10n.PeriodPay.extras, amount: extras, currencyCode: currencyCode)
            }
            if let expenses = summary.totalExpensesPay, expenses != 0 {
                MoneyRow(label: L10n.PeriodPay.expenses, amount: expenses, currencyCode: currencyCode)
            }
            if let bonus = summary.totalBonusPay, bonus != 0 {
                MoneyRow(label: L10n.PeriodPay.bonus, amount: bonus, currencyCode: currencyCode)
            }
            if let deduction = summary.totalDeductionPay, deduction != 0 {
                MoneyRow(label: L10n.PeriodPay.deductions, amount: -deduction, currencyCode: currencyCode)
            }

            EarningsDivider().padding(.vertical, Spacing.xs)

            MoneyRow(
                label: L10n.PeriodPay.total,
                amount: summary.grandTotal ?? 0,
                currencyCode: currencyCode,
                bold: true
            )
        }
        .cardPadding()
    }
}

private struct JobsCard: View {
    let orderPays: [OrderEmployeePayDto]
    let currencyCode: String?

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.xs) {
            Text(L10n.PeriodPay.jobsSection)
                .font(CleansiaTypography.labelMedium)
                .foregroundColor(CleansiaColors.primary)

            if orderPays.isEmpty {
                Text(L10n.PeriodPay.empty)
                    .font(CleansiaTypography.bodyMedium)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
            } else {
                ForEach(Array(orderPays.enumerated()), id: \.offset) { index, line in
                    if index > 0 {
                        EarningsDivider().padding(.vertical, Spacing.xs)
                    }
                    JobRow(line: line, currencyCode: currencyCode)
                }
            }
        }
        .cardPadding()
    }
}

private struct JobRow: View {
    @Environment(\.locale) private var locale
    let line: OrderEmployeePayDto
    let currencyCode: String?

    var body: some View {
        HStack(spacing: Spacing.s) {
            Image(systemName: "doc.text")
                .font(.system(size: 18))
                .foregroundColor(CleansiaColors.onSurfaceVariant)
            VStack(alignment: .leading, spacing: Spacing.hair) {
                Text(orderNumber)
                    .font(CleansiaTypography.bodyLarge)
                    .foregroundColor(CleansiaColors.onSurface)
                if let date = EarningsFormat.shortDate(line.createdOn, locale: locale) {
                    Text(date)
                        .font(CleansiaTypography.labelSmall)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                }
            }
            Spacer()
            Text(EarningsFormat.decimalMoney(line.totalPay ?? 0, currencyCode: currencyCode))
                .font(CleansiaTypography.titleMedium)
                .foregroundColor(CleansiaColors.onSurface)
        }
    }

    private var orderNumber: String {
        if let number = line.orderNumber, !number.isEmpty { return number }
        return "—"
    }
}

private struct MoneyRow: View {
    let label: String
    let amount: Double
    let currencyCode: String?
    var bold = false

    var body: some View {
        HStack {
            Text(label)
                .font(bold ? CleansiaTypography.bodyLarge : CleansiaTypography.bodyMedium)
                .foregroundColor(bold ? CleansiaColors.onSurface : CleansiaColors.onSurfaceVariant)
            Spacer()
            Text(EarningsFormat.decimalMoney(amount, currencyCode: currencyCode))
                .font(bold ? CleansiaTypography.titleMedium : CleansiaTypography.bodyMedium)
                .foregroundColor(CleansiaColors.onSurface)
        }
        .padding(.vertical, Spacing.xxs)
    }
}
