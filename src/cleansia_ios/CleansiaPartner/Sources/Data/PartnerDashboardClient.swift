import CleansiaCore
import CleansiaPartnerApi
import Foundation

protocol PartnerDashboardClient {
    func getStats(employeeId: String?) async -> ApiResult<DashboardStats>
    func getAvailableJobsPreview(limit: Int) async -> ApiResult<AvailableJobsPreview>
    func getCurrentEmployee() async -> ApiResult<EmployeeItem>
}

struct LivePartnerDashboardClient: PartnerDashboardClient {
    func getStats(employeeId: String?) async -> ApiResult<DashboardStats> {
        await apiResult(mapError: ApiError.fromGenerated) {
            try await DashboardStats(PartnerDashboardAPI.dashboardGetStats(employeeId: employeeId))
        }
    }

    func getAvailableJobsPreview(limit: Int) async -> ApiResult<AvailableJobsPreview> {
        await apiResult(mapError: ApiError.fromGenerated) {
            try await AvailableJobsPreview(PartnerDashboardAPI.dashboardGetAvailableJobsPreview(limit: limit))
        }
    }

    func getCurrentEmployee() async -> ApiResult<EmployeeItem> {
        await apiResult(mapError: ApiError.fromGenerated) {
            try await PartnerEmployeeAPI.employeeGetCurrentEmployee()
        }
    }
}
