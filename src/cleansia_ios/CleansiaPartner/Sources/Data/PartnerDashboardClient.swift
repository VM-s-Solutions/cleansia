import CleansiaCore
import CleansiaPartnerApi
import Foundation

protocol PartnerDashboardClient {
    func getStats(employeeId: String?) async -> ApiResult<DashboardStats>
    func getUpcomingOrders(employeeId: String, limit: Int) async -> ApiResult<[OrderListItem]>
    func getAvailableJobsPreview(limit: Int) async -> ApiResult<AvailableJobsPreview>
    func getCurrentEmployee() async -> ApiResult<EmployeeItem>
}

struct LivePartnerDashboardClient: PartnerDashboardClient {
    func getStats(employeeId: String?) async -> ApiResult<DashboardStats> {
        await apiResult(mapError: ApiError.fromGenerated) {
            try await DashboardStats(PartnerDashboardAPI.dashboardGetStats(employeeId: employeeId))
        }
    }

    func getUpcomingOrders(employeeId: String, limit: Int) async -> ApiResult<[OrderListItem]> {
        await apiResult(mapError: ApiError.fromGenerated) {
            try await PartnerDashboardAPI.dashboardGetUpcomingOrders(
                filterIsActive: true,
                filterEmployeeId: employeeId,
                sort: [SortDefinition(field: "cleaningDateTime", direction: ._0)],
                offset: 0,
                limit: limit
            ).data ?? []
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
