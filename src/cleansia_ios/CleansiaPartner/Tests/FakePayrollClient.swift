import CleansiaCore
import CleansiaPartnerApi
import Foundation
@testable import CleansiaPartner

final class FakePayrollClient: PartnerPayrollClient {
    var employeeIdResult: ApiResult<String> = .success("emp-1")
    var periodPaysResult: ApiResult<PeriodPaySummaryDto> = .success(PeriodPaySummaryDto())
    var invoicesResult: ApiResult<[EmployeeInvoiceDto]> = .success([])
    var invoiceResult: ApiResult<EmployeeInvoiceDetailDto> = .success(EmployeeInvoiceDetailDto())
    var downloadResult: ApiResult<URL> = .success(URL(fileURLWithPath: "/tmp/invoice.pdf"))

    private(set) var employeeIdCallCount = 0
    private(set) var periodPaysCallCount = 0
    private(set) var invoicesCallCount = 0
    private(set) var invoiceCallCount = 0
    private(set) var downloadCallCount = 0

    private(set) var periodPaysEmployeeId: String?
    private(set) var periodPaysPayPeriodId: String?
    private(set) var invoicesEmployeeId: String?
    private(set) var lastInvoiceId: String?
    private(set) var lastDownloadId: String?

    func currentEmployeeId() async -> ApiResult<String> {
        employeeIdCallCount += 1
        return employeeIdResult
    }

    func getPeriodPays(employeeId: String, payPeriodId: String) async -> ApiResult<PeriodPaySummaryDto> {
        periodPaysCallCount += 1
        periodPaysEmployeeId = employeeId
        periodPaysPayPeriodId = payPeriodId
        return periodPaysResult
    }

    func getPagedInvoices(employeeId: String) async -> ApiResult<[EmployeeInvoiceDto]> {
        invoicesCallCount += 1
        invoicesEmployeeId = employeeId
        return invoicesResult
    }

    func getInvoice(id: String) async -> ApiResult<EmployeeInvoiceDetailDto> {
        invoiceCallCount += 1
        lastInvoiceId = id
        return invoiceResult
    }

    func downloadInvoicePdf(id: String) async -> ApiResult<URL> {
        downloadCallCount += 1
        lastDownloadId = id
        return downloadResult
    }
}
