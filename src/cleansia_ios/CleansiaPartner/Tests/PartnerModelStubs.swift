import CleansiaPartnerApi
import Foundation
@testable import CleansiaPartner

/// Domain stubs for view-model tests. The DTO → domain refusals are driven directly in
/// `PartnerWireContractTests`; these keep the view-model tests about the view model.
extension DashboardStats {
    static func stub(
        todayEarnings: Double = 0,
        todayCompletedCount: Int = 0,
        weekEarnings: Double = 0,
        weekCompletedCount: Int = 0,
        lastMonthEarnings: Double = 0,
        lastMonthCompletedOrders: Int = 0,
        thisMonthCompletedOrders: Int = 0,
        currentPeriodEarnings: Double = 0,
        ratingCount: Int = 0,
        averageRating: Double? = nil,
        currentPayPeriodStart: Date? = nil,
        currentPayPeriodEnd: Date? = nil,
        nextPayoutDate: Date? = nil,
        latestInvoiceStatus: String? = nil,
        currencyCode: String? = nil
    ) -> DashboardStats {
        DashboardStats(
            todayEarnings: todayEarnings,
            todayCompletedCount: todayCompletedCount,
            weekEarnings: weekEarnings,
            weekCompletedCount: weekCompletedCount,
            lastMonthEarnings: lastMonthEarnings,
            lastMonthCompletedOrders: lastMonthCompletedOrders,
            thisMonthCompletedOrders: thisMonthCompletedOrders,
            currentPeriodEarnings: currentPeriodEarnings,
            ratingCount: ratingCount,
            averageRating: averageRating,
            currentPayPeriodStart: currentPayPeriodStart,
            currentPayPeriodEnd: currentPayPeriodEnd,
            nextPayoutDate: nextPayoutDate,
            latestInvoiceStatus: latestInvoiceStatus,
            currencyCode: currencyCode
        )
    }
}

extension PeriodPaySummary {
    static func stub(
        payPeriodLabel: String? = nil,
        totalOrders: Int = 0,
        totalBasePay: Double = 0,
        totalExtrasPay: Double = 0,
        totalExpensesPay: Double = 0,
        totalBonusPay: Double = 0,
        totalDeductionPay: Double = 0,
        grandTotal: Double = 0,
        orderPays: [OrderPayLine] = []
    ) -> PeriodPaySummary {
        PeriodPaySummary(
            payPeriodLabel: payPeriodLabel,
            totalOrders: totalOrders,
            totalBasePay: totalBasePay,
            totalExtrasPay: totalExtrasPay,
            totalExpensesPay: totalExpensesPay,
            totalBonusPay: totalBonusPay,
            totalDeductionPay: totalDeductionPay,
            grandTotal: grandTotal,
            orderPays: orderPays
        )
    }
}

extension Invoice {
    static func stub(
        id: String = "inv-1",
        invoiceNumber: String? = "INV-1",
        payPeriodLabel: String? = nil,
        totalOrders: Int = 0,
        totalAmount: Double = 0,
        currencyCode: String? = "CZK",
        status: EmployeeInvoiceStatus = ._1,
        generatedAt: Date? = nil,
        paidAt: Date? = nil
    ) -> Invoice {
        Invoice(
            id: id,
            invoiceNumber: invoiceNumber,
            payPeriodLabel: payPeriodLabel,
            totalOrders: totalOrders,
            totalAmount: totalAmount,
            currencyCode: currencyCode,
            status: status,
            generatedAt: generatedAt,
            paidAt: paidAt
        )
    }
}

extension InvoiceDetail {
    static func stub(
        id: String = "inv-1",
        totalAmount: Double = 0,
        pdfGenerationFailed: Bool = false
    ) -> InvoiceDetail {
        InvoiceDetail(
            id: id,
            invoiceNumber: "INV-1",
            payPeriodId: "pp-1",
            payPeriodLabel: nil,
            variableSymbol: nil,
            paymentReference: nil,
            totalOrders: 0,
            subTotal: 0,
            bonusAmount: 0,
            deductionAmount: 0,
            totalAmount: totalAmount,
            currencyCode: "CZK",
            status: ._1,
            pdfGenerationFailed: pdfGenerationFailed,
            generatedAt: nil,
            approvedAt: nil,
            paidAt: nil,
            adminNotes: nil,
            bankTransferNote: nil
        )
    }
}
