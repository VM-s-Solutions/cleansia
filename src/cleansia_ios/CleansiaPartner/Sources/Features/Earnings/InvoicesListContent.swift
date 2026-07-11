import CleansiaCore
import CleansiaPartnerApi
import SwiftUI

struct InvoicesListContent: View {
    let invoices: [EmployeeInvoiceDto]
    let onOpenInvoice: (String) -> Void

    var body: some View {
        if invoices.isEmpty {
            MascotEmptyState(image: Mascot.leaning.image, text: L10n.Invoices.empty, verticallyCentered: true)
        } else {
            ScrollView {
                VStack(spacing: Spacing.m) {
                    InvoicesSummaryCard(invoices: invoices)
                    // Explicit `id:` — the CI-pinned openapi-generator (7.10.0, Android-matching) does not
                    // emit Identifiable on generated models; newer local generators do. Relying on the
                    // conformance compiles locally and breaks CI.
                    ForEach(invoices, id: \.id) { invoice in
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
    let invoices: [EmployeeInvoiceDto]

    private var total: Double {
        invoices.reduce(0) { $0 + ($1.totalAmount ?? 0) }
    }

    private var currencyCode: String? {
        invoices.lazy.compactMap(\.currencyCode).first
    }

    var body: some View {
        HStack(spacing: Spacing.m) {
            IconHalo(systemImage: "creditcard")
            VStack(alignment: .leading, spacing: Spacing.hair) {
                Text(L10n.Invoices.summaryLabel)
                    .font(CleansiaTypography.labelMedium)
                    .foregroundColor(CleansiaColors.primary)
                Text(EarningsFormat.decimalMoney(total, currencyCode: currencyCode))
                    .font(CleansiaTypography.headlineMedium)
                    .foregroundColor(CleansiaColors.onSurface)
                Text(L10n.Invoices.summaryCount(invoices.count))
                    .font(CleansiaTypography.labelSmall)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
            }
            Spacer(minLength: 0)
        }
        .cardPadding()
    }
}

private struct InvoiceCard: View {
    let invoice: EmployeeInvoiceDto
    let onOpen: (String) -> Void

    private var dateLine: String? {
        if let paid = EarningsFormat.shortDate(invoice.paidAt) {
            return L10n.Invoices.cardPaidOn(paid)
        }
        if let generated = EarningsFormat.shortDate(invoice.generatedAt) {
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
        .disabled(invoice.id == nil)
    }

    private func open() {
        invoice.id.map(onOpen)
    }

    private var header: some View {
        HStack(spacing: Spacing.m) {
            IconHalo(systemImage: "doc.text")
            VStack(alignment: .leading, spacing: Spacing.hair) {
                Text(invoice.invoiceNumber ?? "—")
                    .font(CleansiaTypography.titleMedium)
                    .foregroundColor(CleansiaColors.onSurface)
                if let label = invoice.payPeriodLabel, !label.isEmpty {
                    Text(label)
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
                    .foregroundColor(CleansiaColors.primary)
                Text(EarningsFormat.decimalMoney(invoice.totalAmount ?? 0, currencyCode: invoice.currencyCode))
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
        let jobs = invoice.totalOrders ?? 0
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
