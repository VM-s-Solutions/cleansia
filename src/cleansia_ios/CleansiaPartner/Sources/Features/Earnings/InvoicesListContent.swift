import CleansiaCore
import CleansiaPartnerApi
import SwiftUI

struct InvoicesListContent: View {
    let invoices: [Invoice]
    let onOpenInvoice: (String) -> Void

    var body: some View {
        if invoices.isEmpty {
            // `.refreshable` reaches only a scroll view, so the empty list rests in one the height of the screen.
            GeometryReader { pane in
                ScrollView {
                    MascotEmptyState(image: Mascot.leaning.image, text: L10n.Invoices.empty, verticallyCentered: true)
                        .frame(maxWidth: .infinity, minHeight: pane.size.height)
                }
            }
        } else {
            ScrollView {
                VStack(spacing: Spacing.m) {
                    let paid = invoices.paid
                    if !paid.isEmpty {
                        InvoicesSummaryCard(paid: paid)
                    }
                    ForEach(invoices) { invoice in
                        InvoiceCard(invoice: invoice, onOpen: onOpenInvoice)
                    }
                }
                .padding(.horizontal, Spacing.m)
                .padding(.vertical, Spacing.m)
            }
        }
    }
}

private struct InvoicesSummaryCard: View {
    let paid: [Invoice]

    var body: some View {
        HStack(spacing: Spacing.m) {
            IconHalo(systemImage: "creditcard")
            VStack(alignment: .leading, spacing: Spacing.hair) {
                Text(L10n.Invoices.summaryLabel)
                    .font(CleansiaTypography.labelMedium)
                    .foregroundColor(CleansiaColors.primaryText)
                ForEach(Array(paid.totalsByCurrency.enumerated()), id: \.offset) { _, total in
                    Text(EarningsFormat.decimalMoney(total.amount, currencyCode: total.currencyCode))
                        .cleansiaFont(CleansiaTypography.headlineMedium)
                        .foregroundColor(CleansiaColors.onSurface)
                }
                Text(L10n.Invoices.summaryCount(paid.count))
                    .font(CleansiaTypography.labelSmall)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
            }
            Spacer(minLength: 0)
        }
        .cardPadding()
    }
}

private struct InvoiceCard: View {
    @Environment(\.locale) private var locale
    let invoice: Invoice
    let onOpen: (String) -> Void

    private var dateLine: String? {
        if let paid = EarningsFormat.shortDate(invoice.paidAt, locale: locale) {
            return L10n.Invoices.cardPaidOn(paid)
        }
        if let generated = EarningsFormat.shortDate(invoice.generatedAt, locale: locale) {
            return L10n.Invoices.cardGeneratedOn(generated)
        }
        return nil
    }

    var body: some View {
        Button(action: open) {
            VStack(alignment: .leading, spacing: Spacing.m) {
                header
                EarningsDivider()
                totalRow
                if let footer = footerLine {
                    footer
                }
            }
            .cardPadding()
        }
        .buttonStyle(.plain)
    }

    private func open() {
        onOpen(invoice.id)
    }

    private var header: some View {
        HStack(spacing: Spacing.m) {
            IconHalo(systemImage: "doc.text")
            VStack(alignment: .leading, spacing: Spacing.hair) {
                Text(invoice.invoiceNumber ?? "—")
                    .font(CleansiaTypography.titleMedium)
                    .foregroundColor(CleansiaColors.onSurface)
                if let period = EarningsFormat.period(
                    start: invoice.payPeriodStart,
                    end: invoice.payPeriodEnd,
                    locale: locale
                ) {
                    Text(period)
                        .font(CleansiaTypography.labelSmall)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                }
            }
            Spacer(minLength: 0)
            InvoiceStatusBadge(status: invoice.status)
        }
    }

    private var totalRow: some View {
        HStack(alignment: .center) {
            VStack(alignment: .leading, spacing: Spacing.hair) {
                Text(L10n.Invoices.cardTotal)
                    .font(CleansiaTypography.labelMedium)
                    .foregroundColor(CleansiaColors.primaryText)
                Text(EarningsFormat.decimalMoney(invoice.totalAmount, currencyCode: invoice.currencyCode))
                    .font(CleansiaTypography.titleLarge)
                    .foregroundColor(CleansiaColors.onSurface)
            }
            Spacer()
            Image(systemName: "chevron.right")
                .foregroundColor(CleansiaColors.onSurfaceVariant)
        }
    }

    @ViewBuilder
    private var footerLine: (some View)? {
        let jobs = invoice.totalOrders
        let date = dateLine
        if jobs > 0 || date != nil {
            HStack {
                if jobs > 0 {
                    MetaLabel(icon: "doc.text", text: L10n.Invoices.cardJobsCount(jobs))
                }
                Spacer()
                if let date {
                    MetaLabel(icon: "calendar", text: date)
                }
            }
        }
    }
}

private struct MetaLabel: View {
    let icon: String
    let text: String

    var body: some View {
        HStack(spacing: Spacing.xxs) {
            Image(systemName: icon).font(.system(size: 12))
            Text(text).font(CleansiaTypography.labelSmall)
        }
        .foregroundColor(CleansiaColors.onSurfaceVariant)
    }
}

extension [Invoice] {
    /// Total earned counts an invoice once it is Paid: until then it is not money received.
    var paid: [Invoice] {
        filter { $0.status == ._3 }
    }

    /// One total per currency, in the order the list first names each: a koruna and a euro invoice are never one sum.
    var totalsByCurrency: [(currencyCode: String?, amount: Double)] {
        reduce(into: []) { totals, invoice in
            if let index = totals.firstIndex(where: { $0.currencyCode == invoice.currencyCode }) {
                totals[index].amount += invoice.totalAmount
            } else {
                totals.append((invoice.currencyCode, invoice.totalAmount))
            }
        }
    }
}
