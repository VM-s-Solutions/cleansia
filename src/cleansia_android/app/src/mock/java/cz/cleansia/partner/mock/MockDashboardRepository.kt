package cz.cleansia.partner.mock

import cz.cleansia.partner.core.network.ApiResult
import cz.cleansia.partner.domain.models.dashboard.CompletionTimeEfficiency
import cz.cleansia.partner.domain.models.dashboard.DashboardStats
import cz.cleansia.partner.domain.models.dashboard.EarningsAnalytics
import cz.cleansia.partner.domain.models.dashboard.EarningsSummary
import cz.cleansia.partner.domain.models.dashboard.MonthlyEarningsTrend
import cz.cleansia.partner.domain.models.dashboard.OrderStatusDistribution
import cz.cleansia.partner.domain.models.dashboard.PerformanceScore
import cz.cleansia.partner.domain.models.dashboard.ScheduleUtilization
import cz.cleansia.partner.domain.models.dashboard.ServiceRevenueBreakdown
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

    override suspend fun getOrderStatusDistribution(): ApiResult<OrderStatusDistribution> {
        delay(300)
        return ApiResult.Success(MockDataProvider.orderStatusDistribution())
    }

    override suspend fun getPerformanceScore(): ApiResult<PerformanceScore> {
        delay(300)
        return ApiResult.Success(MockDataProvider.performanceScore())
    }

    override suspend fun getMonthlyEarningsTrend(): ApiResult<MonthlyEarningsTrend> {
        delay(300)
        return ApiResult.Success(MockDataProvider.monthlyEarningsTrend())
    }

    override suspend fun getServiceRevenueBreakdown(): ApiResult<ServiceRevenueBreakdown> {
        delay(300)
        return ApiResult.Success(MockDataProvider.serviceRevenueBreakdown())
    }

    override suspend fun getScheduleUtilization(): ApiResult<ScheduleUtilization> {
        delay(300)
        return ApiResult.Success(MockDataProvider.scheduleUtilization())
    }

    override suspend fun getCompletionTimeEfficiency(): ApiResult<CompletionTimeEfficiency> {
        delay(300)
        return ApiResult.Success(MockDataProvider.completionTimeEfficiency())
    }
}
