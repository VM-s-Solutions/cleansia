import CleansiaCore
import CleansiaPartnerApi
import Foundation

protocol PartnerDashboardClient {
    func getStats(employeeId: String?) async -> ApiResult<DashboardStatsDto>
    func getAvailableJobsPreview(limit: Int) async -> ApiResult<AvailableJobsPreviewResponse>
    func getCurrentEmployeeProfile() async -> ApiResult<EmployeeProfile>
}

struct LivePartnerDashboardClient: PartnerDashboardClient {
    func getStats(employeeId: String?) async -> ApiResult<DashboardStatsDto> {
        await apiResult(mapError: ApiError.fromGenerated) {
            try await PartnerDashboardAPI.dashboardGetStats(employeeId: employeeId)
        }
    }

    func getAvailableJobsPreview(limit: Int) async -> ApiResult<AvailableJobsPreviewResponse> {
        await apiResult(mapError: ApiError.fromGenerated) {
            try await PartnerDashboardAPI.dashboardGetAvailableJobsPreview(limit: limit)
        }
    }

    func getCurrentEmployeeProfile() async -> ApiResult<EmployeeProfile> {
        await apiResult(mapError: ApiError.fromGenerated) {
            try await PartnerEmployeeEndpoints.getCurrentEmployeeProfile()
        }
    }
}
