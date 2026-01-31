package cz.cleansia.partner.domain.repositories

import cz.cleansia.partner.core.network.ApiResult
import cz.cleansia.partner.core.network.ApiService
import cz.cleansia.partner.core.network.safeApiCall
import cz.cleansia.partner.core.storage.TokenManager
import cz.cleansia.partner.domain.models.dashboard.DashboardStats
import cz.cleansia.partner.domain.models.dashboard.EarningsAnalytics
import cz.cleansia.partner.domain.models.dashboard.EarningsSummary
import cz.cleansia.partner.domain.models.dashboard.UpcomingOrder
import kotlinx.serialization.json.Json
import javax.inject.Inject
import javax.inject.Singleton

interface DashboardRepository {
    suspend fun getDashboardStats(): ApiResult<DashboardStats>
    suspend fun getEarningsAnalytics(startDate: String? = null, endDate: String? = null): ApiResult<EarningsAnalytics>
    suspend fun getEarnings(): ApiResult<EarningsSummary>
    suspend fun getUpcomingOrders(limit: Int = 5): ApiResult<List<UpcomingOrder>>
}

@Singleton
class DashboardRepositoryImpl @Inject constructor(
    private val apiService: ApiService,
    private val tokenManager: TokenManager,
    private val json: Json
) : DashboardRepository {

    override suspend fun getDashboardStats(): ApiResult<DashboardStats> {
        return safeApiCall(json) {
            apiService.getDashboardStats(employeeId = tokenManager.getUserId())
        }
    }

    override suspend fun getEarningsAnalytics(
        startDate: String?,
        endDate: String?
    ): ApiResult<EarningsAnalytics> {
        return safeApiCall(json) {
            apiService.getEarningsAnalytics(
                employeeId = tokenManager.getUserId(),
                startDate = startDate,
                endDate = endDate
            )
        }
    }

    override suspend fun getUpcomingOrders(limit: Int): ApiResult<List<UpcomingOrder>> {
        return safeApiCall(json) {
            apiService.getUpcomingOrders(
                employeeId = tokenManager.getUserId(),
                limit = limit
            )
        }
    }

    override suspend fun getEarnings(): ApiResult<EarningsSummary> {
        return safeApiCall(json) {
            apiService.getEarningsSummary(employeeId = tokenManager.getUserId())
        }
    }
}
