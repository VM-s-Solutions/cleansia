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
