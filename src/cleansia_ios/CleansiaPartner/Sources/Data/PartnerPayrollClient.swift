import CleansiaCore
import CleansiaPartnerApi
import Foundation

protocol PartnerPayrollClient: AnyObject {
    /// The signed-in cleaner's own employeeId — the JWT-truth surrogate the VM
    /// passes to GetPeriodPays. The client offers ONLY the caller's own id; it
    /// never echoes a screen-supplied one.
    func currentEmployeeId() async -> ApiResult<String>

    func getPeriodPays(employeeId: String, payPeriodId: String) async -> ApiResult<PeriodPaySummaryDto>
    func getPagedInvoices(employeeId: String) async -> ApiResult<[EmployeeInvoiceDto]>
    func getInvoice(id: String) async -> ApiResult<EmployeeInvoiceDetailDto>
    func downloadInvoicePdf(id: String) async -> ApiResult<URL>
}

final class LivePartnerPayrollClient: PartnerPayrollClient {
    func currentEmployeeId() async -> ApiResult<String> {
        await apiResult(mapError: ApiError.fromGenerated) {
            let employee = try await PartnerEmployeeAPI.employeeGetCurrentEmployee()
            guard let id = employee.id, !id.isEmpty else {
                throw ApiError(code: "payroll.employee_id_missing")
            }
            return id
        }
    }

    func getPeriodPays(employeeId: String, payPeriodId: String) async -> ApiResult<PeriodPaySummaryDto> {
        await apiResult(mapError: ApiError.fromGenerated) {
            try await PartnerEmployeePayrollAPI.employeePayrollGetPeriodPays(
                employeeId: employeeId,
                payPeriodId: payPeriodId
            )
        }
    }

    func getPagedInvoices(employeeId: String) async -> ApiResult<[EmployeeInvoiceDto]> {
        await apiResult(mapError: ApiError.fromGenerated) {
            let paged = try await PartnerEmployeePayrollAPI.employeePayrollGetPagedInvoices(
                filterEmployeeId: employeeId,
                sort: [SortDefinition(field: "createdOn", direction: ._1)],
                offset: 0,
                limit: 50
            )
            return paged.data ?? []
        }
    }

    func getInvoice(id: String) async -> ApiResult<EmployeeInvoiceDetailDto> {
        await apiResult(mapError: ApiError.fromGenerated) {
            try await PartnerEmployeePayrollAPI.employeePayrollGetInvoiceById(invoiceId: id)
        }
    }

    func downloadInvoicePdf(id: String) async -> ApiResult<URL> {
        await apiResult(mapError: ApiError.fromGenerated) {
            try await PartnerEmployeePayrollAPI.employeePayrollDownloadInvoice(invoiceId: id)
        }
    }
}
