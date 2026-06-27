import Foundation

enum EarningsRoute: Hashable {
    case invoices
    case invoiceDetail(id: String)
    case periodPay(payPeriodId: String, currencyCode: String?)
}
