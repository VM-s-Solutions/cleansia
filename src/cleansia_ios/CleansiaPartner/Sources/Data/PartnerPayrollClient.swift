import CleansiaCore
import CleansiaPartnerApi
import Foundation

protocol PartnerPayrollClient: AnyObject {
    /// The signed-in cleaner's own employeeId — the JWT-truth surrogate the VM
    /// passes to GetPeriodPays. The client offers ONLY the caller's own id; it
    /// never echoes a screen-supplied one.
    func currentEmployeeId() async -> ApiResult<String>

    func getPeriodPays(employeeId: String, payPeriodId: String) async -> ApiResult<PeriodPaySummary>
    func getPagedInvoices(employeeId: String) async -> ApiResult<[Invoice]>
    func getInvoice(id: String) async -> ApiResult<InvoiceDetail>
    func downloadInvoicePdf(id: String) async -> ApiResult<URL>
}

final class LivePartnerPayrollClient: PartnerPayrollClient {
    func currentEmployeeId() async -> ApiResult<String> {
        await apiResult(mapError: ApiError.fromGenerated) {
            try await PartnerEmployeeAPI.employeeGetCurrentEmployee().id.requireNonBlank("id")
        }
    }

    func getPeriodPays(employeeId: String, payPeriodId: String) async -> ApiResult<PeriodPaySummary> {
        await apiResult(mapError: ApiError.fromGenerated) {
            try await PeriodPaySummary(PartnerEmployeePayrollAPI.employeePayrollGetPeriodPays(
                employeeId: employeeId,
                payPeriodId: payPeriodId
            ))
        }
    }

    func getPagedInvoices(employeeId: String) async -> ApiResult<[Invoice]> {
        await apiResult(mapError: ApiError.fromGenerated) {
            let paged = try await PartnerEmployeePayrollAPI.employeePayrollGetPagedInvoices(
                filterEmployeeId: employeeId,
                sort: [SortDefinition(field: "createdOn", direction: ._1)],
                offset: 0,
                limit: 50
            )
            return try (paged.data ?? []).map(Invoice.init)
        }
    }

    func getInvoice(id: String) async -> ApiResult<InvoiceDetail> {
        await apiResult(mapError: ApiError.fromGenerated) {
            try await InvoiceDetail(PartnerEmployeePayrollAPI.employeePayrollGetInvoiceById(invoiceId: id))
        }
    }

    func downloadInvoicePdf(id: String) async -> ApiResult<URL> {
        await apiResult(mapError: ApiError.fromGenerated) {
            try await PartnerEmployeePayrollAPI.employeePayrollDownloadInvoice(invoiceId: id)
        }
    }
}
