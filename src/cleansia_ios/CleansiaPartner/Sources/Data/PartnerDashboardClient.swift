import CleansiaCore
import CleansiaPartnerApi
import Foundation

protocol PartnerDashboardClient {
    func getStats(employeeId: String?) async -> ApiResult<DashboardStatsDto>
    func getCurrentEmployee() async -> ApiResult<EmployeeItem>
}

struct LivePartnerDashboardClient: PartnerDashboardClient {
    func getStats(employeeId: String?) async -> ApiResult<DashboardStatsDto> {
        await apiResult(mapError: ApiError.fromGenerated) {
            try await PartnerDashboardAPI.dashboardGetStats(employeeId: employeeId)
        }
    }

    func getCurrentEmployee() async -> ApiResult<EmployeeItem> {
        await apiResult(mapError: ApiError.fromGenerated) {
            try await PartnerEmployeeAPI.employeeGetCurrentEmployee()
        }
    }
}

extension ApiError {
    static func fromGenerated(_ error: Error) -> ApiError {
        guard case let .error(status, data, _, underlying) = error as? ErrorResponse else {
            return ApiError.from(error)
        }
        let detail = data.flatMap { String(data: $0, encoding: .utf8) }
        return ApiError(code: nil, message: detail ?? underlying.localizedDescription, httpStatus: status)
    }
}
