import CleansiaCore
import CleansiaPartnerApi
import SwiftUI
#if canImport(UIKit)
    import UIKit
#endif

struct InvoiceDetailContent: View {
    let invoice: EmployeeInvoiceDetailDto
    let canOpenPdf: Bool
    let isDownloading: Bool
    let onOpenPeriodPay: ((String, String?) -> Void)?
    let onOpenPdf: () -> Void
    let onCopy: (String) -> Void

    var body: some View {
        ScrollView {
            VStack(spacing: Spacing.m) {
                HeroCard(invoice: invoice)
                BreakdownCard(invoice: invoice)
                PeriodCard(invoice: invoice, onOpenPeriodPay: onOpenPeriodPay)
                ReferencesCard(invoice: invoice, onCopy: onCopy)
                NotesCard(invoice: invoice)
                if canOpenPdf {
                    CleansiaPrimaryButton(
                        L10n.Invoices.openPdf,
                        trailingIcon: "doc.text",
                        loading: isDownloading,
                        enabled: !isDownloading,
                        action: onOpenPdf
                    )
                    .padding(.top, Spacing.xs)
                }
            }
            .padding(.horizontal, Spacing.m)
            .padding(.vertical, Spacing.m)
        }
    }
}

private struct HeroCard: View {
    let invoice: EmployeeInvoiceDetailDto

    var body: some View {
        HStack(spacing: Spacing.m) {
            IconHalo(systemImage: "creditcard")
            VStack(alignment: .leading, spacing: Spacing.hair) {
                Text(L10n.Invoices.heroTotal)
                    .font(CleansiaTypography.labelMedium)
                    .foregroundColor(CleansiaColors.primary)
                Text(EarningsFormat.decimalMoney(invoice.totalAmount ?? 0, currencyCode: invoice.currencyCode))
                    .font(CleansiaTypography.headlineMedium)
                    .foregroundColor(CleansiaColors.onSurface)
                if let number = invoice.invoiceNumber, !number.isEmpty {
                    Text(number)
                        .font(CleansiaTypography.labelSmall)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                }
            }
            Spacer(minLength: 0)
            InvoiceStatusBadge(status: invoice.status)
        }
        .cardPadding()
    }
}

private struct BreakdownCard: View {
    let invoice: EmployeeInvoiceDetailDto

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.xs) {
            Text(L10n.Invoices.breakdownSection)
                .font(CleansiaTypography.labelMedium)
                .foregroundColor(CleansiaColors.primary)

            MoneyRow(label: L10n.Invoices.subtotal, amount: invoice.subTotal ?? 0, code: invoice.currencyCode)
            if let bonus = invoice.bonusAmount, bonus != 0 {
                MoneyRow(label: L10n.Invoices.bonus, amount: bonus, code: invoice.currencyCode)
            }
            if let deduction = invoice.deductionAmount, deduction != 0 {
                MoneyRow(label: L10n.Invoices.deductions, amount: -deduction, code: invoice.currencyCode)
            }

            EarningsDivider().padding(.vertical, Spacing.xs)

            MoneyRow(
                label: L10n.Invoices.total,
                amount: invoice.totalAmount ?? 0,
                code: invoice.currencyCode,
                bold: true
            )
        }
        .cardPadding()
    }
}

private struct PeriodCard: View {
    let invoice: EmployeeInvoiceDetailDto
    let onOpenPeriodPay: ((String, String?) -> Void)?

    private var drill: (() -> Void)? {
        guard let open = onOpenPeriodPay,
              let periodId = invoice.payPeriodId, !periodId.isEmpty else { return nil }
        return { open(periodId, invoice.currencyCode) }
    }

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            HStack(spacing: Spacing.m) {
                IconHalo(systemImage: "calendar")
                VStack(alignment: .leading, spacing: Spacing.hair) {
                    Text(L10n.Invoices.periodLabel)
                        .font(CleansiaTypography.labelMedium)
                        .foregroundColor(CleansiaColors.primary)
                    Text(invoice.payPeriodLabel?.nilIfEmpty ?? "—")
                        .font(CleansiaTypography.titleMedium)
                        .foregroundColor(CleansiaColors.onSurface)
                    if let orders = invoice.totalOrders, orders > 0 {
                        Text(L10n.Invoices.periodJobs(orders))
                            .font(CleansiaTypography.labelSmall)
                            .foregroundColor(CleansiaColors.onSurfaceVariant)
                    }
                }
                Spacer(minLength: 0)
            }

            if hasAnyDate {
                EarningsDivider().padding(.vertical, Spacing.m)
                dateRows
            }

            if let drill {
                EarningsDivider().padding(.top, Spacing.m)
                Button(action: drill) {
                    HStack {
                        Text(L10n.Invoices.viewPeriodPay)
                            .font(CleansiaTypography.bodyLarge)
                            .foregroundColor(CleansiaColors.primary)
                        Spacer()
                        Image(systemName: "chevron.right")
                            .foregroundColor(CleansiaColors.primary)
                    }
                    .padding(.top, Spacing.m)
                }
                .buttonStyle(.plain)
            }
        }
        .cardPadding()
    }

    private var hasAnyDate: Bool {
        EarningsFormat.shortDate(invoice.generatedAt) != nil
            || EarningsFormat.shortDate(invoice.approvedAt) != nil
            || EarningsFormat.shortDate(invoice.paidAt) != nil
    }

    private var dateRows: some View {
        VStack(spacing: Spacing.xs) {
            if let generated = EarningsFormat.shortDate(invoice.generatedAt) {
                DateRow(label: L10n.Invoices.periodGenerated, value: generated)
            }
            if let approved = EarningsFormat.shortDate(invoice.approvedAt) {
                DateRow(label: L10n.Invoices.periodApproved, value: approved)
            }
            if let paid = EarningsFormat.shortDate(invoice.paidAt) {
                DateRow(label: L10n.Invoices.periodPaid, value: paid)
            }
        }
    }
}

private struct ReferencesCard: View {
    let invoice: EmployeeInvoiceDetailDto
    let onCopy: (String) -> Void

    private var fields: [(label: String, value: String)] {
        var rows: [(String, String)] = []
        if let value = invoice.invoiceNumber?.nilIfEmpty {
            rows.append((L10n.Invoices.fieldInvoiceNumber, value))
        }
        if let value = invoice.variableSymbol?.nilIfEmpty {
            rows.append((L10n.Invoices.fieldVariableSymbol, value))
        }
        if let value = invoice.paymentReference?.nilIfEmpty {
            rows.append((L10n.Invoices.fieldPaymentReference, value))
        }
        return rows
    }

    var body: some View {
        if !fields.isEmpty {
            VStack(alignment: .leading, spacing: Spacing.m) {
                HStack(spacing: Spacing.m) {
                    IconHalo(systemImage: "key")
                    Text(L10n.Invoices.referencesSection)
                        .font(CleansiaTypography.labelMedium)
                        .foregroundColor(CleansiaColors.primary)
                    Spacer(minLength: 0)
                }
                ForEach(fields, id: \.label) { field in
                    CopyableField(label: field.label, value: field.value) { onCopy(field.value) }
                }
            }
            .cardPadding()
        }
    }
}

private struct NotesCard: View {
    let invoice: EmployeeInvoiceDetailDto

    var body: some View {
        let admin = invoice.adminNotes?.nilIfEmpty
        let bank = invoice.bankTransferNote?.nilIfEmpty
        if admin != nil || bank != nil {
            VStack(alignment: .leading, spacing: Spacing.m) {
                HStack(spacing: Spacing.m) {
                    IconHalo(systemImage: "note.text")
                    Text(L10n.Invoices.notes)
                        .font(CleansiaTypography.labelMedium)
                        .foregroundColor(CleansiaColors.primary)
                    Spacer(minLength: 0)
                }
                if let admin {
                    NoteBlock(label: L10n.Invoices.notesAdmin, text: admin)
                }
                if let bank {
                    NoteBlock(label: L10n.Invoices.notesBank, text: bank)
                }
            }
            .cardPadding()
        }
    }
}

private struct NoteBlock: View {
    let label: String
    let text: String

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.hair) {
            Text(label)
                .font(CleansiaTypography.labelSmall)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
            Text(text)
                .font(CleansiaTypography.bodyMedium)
                .foregroundColor(CleansiaColors.onSurface)
        }
        .frame(maxWidth: .infinity, alignment: .leading)
    }
}

private struct MoneyRow: View {
    let label: String
    let amount: Double
    let code: String?
    var bold = false

    var body: some View {
        HStack {
            Text(label)
                .font(bold ? CleansiaTypography.bodyLarge : CleansiaTypography.bodyMedium)
                .foregroundColor(bold ? CleansiaColors.onSurface : CleansiaColors.onSurfaceVariant)
            Spacer()
            Text(EarningsFormat.decimalMoney(amount, currencyCode: code))
                .font(bold ? CleansiaTypography.titleMedium : CleansiaTypography.bodyMedium)
                .foregroundColor(CleansiaColors.onSurface)
        }
        .padding(.vertical, Spacing.xxs)
    }
}

private struct DateRow: View {
    let label: String
    let value: String

    var body: some View {
        HStack {
            Text(label)
                .font(CleansiaTypography.bodyMedium)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
            Spacer()
            Text(value)
                .font(CleansiaTypography.bodyMedium)
                .foregroundColor(CleansiaColors.onSurface)
        }
    }
}

private struct CopyableField: View {
    let label: String
    let value: String
    let onCopy: () -> Void

    var body: some View {
        Button(action: onCopy) {
            HStack(alignment: .center, spacing: Spacing.s) {
                VStack(alignment: .leading, spacing: Spacing.hair) {
                    Text(label)
                        .font(CleansiaTypography.labelSmall)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                    Text(value)
                        .font(CleansiaTypography.bodyMedium)
                        .monospaced()
                        .fontWeight(.semibold)
                        .foregroundColor(CleansiaColors.onSurface)
                }
                Spacer()
                Image(systemName: "doc.on.doc")
                    .font(.system(size: 16))
                    .foregroundColor(CleansiaColors.primary)
                    .accessibilityLabel(L10n.Invoices.fieldCopy)
            }
        }
        .buttonStyle(.plain)
    }
}

private extension String {
    var nilIfEmpty: String? {
        let trimmed = trimmingCharacters(in: .whitespacesAndNewlines)
        return trimmed.isEmpty ? nil : trimmed
    }
}
