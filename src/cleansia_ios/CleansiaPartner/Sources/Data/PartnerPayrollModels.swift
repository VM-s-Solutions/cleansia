import CleansiaCore
import CleansiaPartnerApi
import Foundation

/// **Refuse the page.** Every row is an addend of the list's own rollup — `InvoicesListContent`
/// sums `totalAmount` across the rows and renders it as what the cleaner is owed — so dropping an
/// unmappable row would silently subtract its amount from that figure. A smaller, plausible,
/// unmarked number is the failure with no symptom; refusing is the only outcome that cannot show a
/// wrong total.
///
/// `generatedAt` is `nullable: false` on the wire and stays optional here anyway: the card already
/// renders its absence (`dateLine` returns nil and the footer drops), so leaving it nil fabricates
/// nothing. The rule is that a coercion must not invent a fact the cleaner reads, not that every
/// non-nullable field must be refused. `status` and the money are refused because a placeholder
/// badge or a `0` reads as a fact.
struct Invoice: Equatable, Identifiable {
    let id: String
    let invoiceNumber: String?
    let payPeriodLabel: String?
    let totalOrders: Int
    let totalAmount: Double
    let currencyCode: String?
    let status: EmployeeInvoiceStatus
    let generatedAt: Date?
    let paidAt: Date?
}

extension Invoice {
    init(_ dto: EmployeeInvoiceDto) throws {
        id = try dto.id.requireNonBlank("id")
        invoiceNumber = dto.invoiceNumber
        payPeriodLabel = dto.payPeriodLabel
        totalOrders = try dto.totalOrders.require("totalOrders")
        totalAmount = try dto.totalAmount.require("totalAmount")
        currencyCode = dto.currencyCode
        status = try dto.status.require("status")
        generatedAt = dto.generatedAt
        paidAt = dto.paidAt
    }
}

/// One invoice, so there is no page to refuse and no row to drop: the §D2 question does not arise.
/// Every money field is refused on rule 1 alone — the screen renders each as this invoice's own
/// figure, and the detail carries no line collection on iOS (`InvoiceDetailContent` never reads
/// `orderPays`), so nothing here is a sum over anything.
///
/// `pdfGenerationFailed` is refused rather than defaulted: it is the only durable signal that a
/// render failed, and `false` reports a healthy document that may not exist — the "Open invoice PDF"
/// button would then be offered against nothing.
struct InvoiceDetail: Equatable, Identifiable {
    let id: String
    let invoiceNumber: String?
    let payPeriodId: String?
    let payPeriodLabel: String?
    let variableSymbol: String?
    let paymentReference: String?
    let totalOrders: Int
    let subTotal: Double
    let bonusAmount: Double
    let deductionAmount: Double
    let totalAmount: Double
    let currencyCode: String?
    let status: EmployeeInvoiceStatus
    let pdfGenerationFailed: Bool
    let generatedAt: Date?
    let approvedAt: Date?
    let paidAt: Date?
    let adminNotes: String?
    let bankTransferNote: String?
}

extension InvoiceDetail {
    init(_ dto: EmployeeInvoiceDetailDto) throws {
        id = try dto.id.requireNonBlank("id")
        invoiceNumber = dto.invoiceNumber
        payPeriodId = dto.payPeriodId
        payPeriodLabel = dto.payPeriodLabel
        variableSymbol = dto.variableSymbol
        paymentReference = dto.paymentReference
        totalOrders = try dto.totalOrders.require("totalOrders")
        subTotal = try dto.subTotal.require("subTotal")
        bonusAmount = try dto.bonusAmount.require("bonusAmount")
        deductionAmount = try dto.deductionAmount.require("deductionAmount")
        totalAmount = try dto.totalAmount.require("totalAmount")
        currencyCode = dto.currencyCode
        status = try dto.status.require("status")
        pdfGenerationFailed = try dto.pdfGenerationFailed.require("pdfGenerationFailed")
        generatedAt = dto.generatedAt
        approvedAt = dto.approvedAt
        paidAt = dto.paidAt
        adminNotes = dto.adminNotes
        bankTransferNote = dto.bankTransferNote
    }
}

/// **Drop the line, refuse the summary.** The two rulings compose in one mapper. Every figure the
/// screen renders money from is the summary's own — `grandTotal`, `totalBasePay` and the rest — and
/// never a sum over `orderPays`, so one lost line cannot falsify a figure while refusing the whole
/// period would blank a payslip the server answered correctly. The summary's own totals get the
/// opposite treatment for the same reason: they *are* what the cleaner reads.
struct PeriodPaySummary: Equatable {
    let payPeriodLabel: String?
    let totalOrders: Int
    let totalBasePay: Double
    let totalExtrasPay: Double
    let totalExpensesPay: Double
    let totalBonusPay: Double
    let totalDeductionPay: Double
    let grandTotal: Double
    let orderPays: [OrderPayLine]
}

extension PeriodPaySummary {
    init(_ dto: PeriodPaySummaryDto) throws {
        payPeriodLabel = dto.payPeriodLabel
        totalOrders = try dto.totalOrders.require("totalOrders")
        totalBasePay = try dto.totalBasePay.require("totalBasePay")
        totalExtrasPay = try dto.totalExtrasPay.require("totalExtrasPay")
        totalExpensesPay = try dto.totalExpensesPay.require("totalExpensesPay")
        totalBonusPay = try dto.totalBonusPay.require("totalBonusPay")
        totalDeductionPay = try dto.totalDeductionPay.require("totalDeductionPay")
        grandTotal = try dto.grandTotal.require("grandTotal")
        orderPays = try (dto.orderPays ?? []).compactMap(OrderPayLine.init)
    }
}

/// An unidentifiable line is dropped; a line whose own money is broken refuses, and because the line
/// is an element of the summary that refusal refuses the summary.
struct OrderPayLine: Equatable, Identifiable {
    let id: String
    let orderNumber: String?
    let totalPay: Double
    let createdOn: Date?
}

extension OrderPayLine {
    init?(_ dto: OrderEmployeePayDto) throws {
        guard let lineId = dto.id, !lineId.isBlank else { return nil }
        id = lineId
        orderNumber = dto.orderNumber
        totalPay = try dto.totalPay.require("totalPay")
        createdOn = dto.createdOn
    }
}
