package cz.cleansia.partner.data.payroll

import cz.cleansia.partner.core.network.ApiResult
import cz.cleansia.partner.core.network.safeApiCall
import kotlinx.serialization.json.Json
import javax.inject.Inject
import javax.inject.Singleton

interface PeriodPayRepository {
    suspend fun getPeriodPays(employeeId: String, payPeriodId: String): ApiResult<PeriodPaySummary>
}

@Singleton
class PeriodPayRepositoryImpl @Inject constructor(
    private val api: PeriodPayApi,
    private val json: Json,
) : PeriodPayRepository {

    override suspend fun getPeriodPays(employeeId: String, payPeriodId: String): ApiResult<PeriodPaySummary> =
        safeApiCall(json) { api.getPeriodPays(employeeId, payPeriodId) }
}
