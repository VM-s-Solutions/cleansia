package cz.cleansia.partner.mock

import cz.cleansia.partner.core.network.ApiResult
import cz.cleansia.partner.domain.models.dashboard.DashboardStats
import cz.cleansia.partner.domain.models.dashboard.EarningsAnalytics
import cz.cleansia.partner.domain.models.dashboard.EarningsSummary
import cz.cleansia.partner.domain.models.dashboard.UpcomingOrder
import cz.cleansia.partner.domain.repositories.DashboardRepository
import kotlinx.coroutines.delay

class MockDashboardRepository : DashboardRepository {

    override suspend fun getDashboardStats(): ApiResult<DashboardStats> {
        delay(400)
        return ApiResult.Success(MockDataProvider.dashboardStats())
    }

    override suspend fun getEarningsAnalytics(
        startDate: String?,
        endDate: String?
    ): ApiResult<EarningsAnalytics> {
        delay(500)
        return ApiResult.Success(MockDataProvider.earningsAnalytics(startDate, endDate))
    }

    override suspend fun getEarnings(): ApiResult<EarningsSummary> {
        delay(300)
        return ApiResult.Success(MockDataProvider.earningsSummary())
    }

    override suspend fun getUpcomingOrders(limit: Int): ApiResult<List<UpcomingOrder>> {
        delay(400)
        return ApiResult.Success(MockDataProvider.upcomingOrders(limit))
    }
}
