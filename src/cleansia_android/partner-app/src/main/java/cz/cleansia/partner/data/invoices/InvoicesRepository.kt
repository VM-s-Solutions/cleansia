package cz.cleansia.partner.data.invoices

import cz.cleansia.core.auth.SessionScopedCache
import cz.cleansia.core.freshness.Staleness
import cz.cleansia.partner.api.client.EmployeePayrollApi
import cz.cleansia.partner.api.model.EmployeeInvoiceDetailDto
import cz.cleansia.partner.api.model.EmployeeInvoiceDto
import cz.cleansia.partner.api.model.SortDefinition
import cz.cleansia.partner.api.model.SortDirection
import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.network.safeApiCall
import kotlinx.serialization.json.Json
import okhttp3.ResponseBody
import javax.inject.Inject
import javax.inject.Singleton

interface InvoicesRepository {
    suspend fun getMyInvoices(employeeId: String): ApiResult<List<EmployeeInvoiceDto>>
    suspend fun getById(invoiceId: String): ApiResult<EmployeeInvoiceDetailDto>
    suspend fun downloadPdf(invoiceId: String): ApiResult<ResponseBody>

    /**
     * Freshness watermark for [getMyInvoices]. ViewModels check
     * [Staleness.isStale] before triggering background refreshes
     * (silent-stale pattern) so screen-resume on a still-fresh cache
     * skips the network round-trip. User-initiated pulls ignore this
     * and always fetch — the user's intent is the source of truth,
     * not the cache age.
     *
     * Marked fresh ONLY on a successful fetch; errors leave the
     * watermark untouched so the next entry still retries. Watermark
     * survives for the lifetime of the singleton repo (no per-screen
     * reset).
     */
    fun getMyInvoicesStaleness(): Staleness

    /**
     * Forget the my-invoices watermark. Call after a mutation that
     * could change the list (e.g. an invoice-related action elsewhere)
     * so the next [getMyInvoices] call refetches even though the
     * staleness window hasn't elapsed.
     */
    fun invalidateMyInvoices()
}

@Singleton
class InvoicesRepositoryImpl @Inject constructor(
    private val payrollApi: EmployeePayrollApi,
    private val json: Json,
) : InvoicesRepository, SessionScopedCache {

    /**
     * Watermark for the my-invoices cache. Stamped on every successful
     * [getMyInvoices] fetch; errors intentionally leave it untouched
     * so a failed first attempt doesn't gate out the immediate retry.
     * Exposed via [getMyInvoicesStaleness] so the consumer ViewModel
     * can run its silent-stale check without us leaking mutable state.
     */
    private val myInvoicesStaleness = Staleness()

    override suspend fun getMyInvoices(employeeId: String): ApiResult<List<EmployeeInvoiceDto>> =
        safeApiCall(json) {
            payrollApi.employeePayrollGetPagedInvoices(
                filterEmployeeId = employeeId,
                sort = listOf(SortDefinition(field = "createdOn", direction = SortDirection._1)),
                offset = 0,
                limit = 50,
            )
        }.map { it.data.orEmpty() }
            .also { result ->
                // Only stamp the watermark on success — a failed fetch
                // should NOT advance the stale gate, otherwise the next
                // entry would think we have fresh data when we just
                // logged a network error and have nothing new to show.
                if (result is ApiResult.Success) myInvoicesStaleness.markFresh()
            }

    override suspend fun getById(invoiceId: String): ApiResult<EmployeeInvoiceDetailDto> =
        safeApiCall(json) { payrollApi.employeePayrollGetInvoiceById(invoiceId) }

    override suspend fun downloadPdf(invoiceId: String): ApiResult<ResponseBody> =
        safeApiCall(json) { payrollApi.employeePayrollDownloadInvoice(invoiceId) }

    override fun getMyInvoicesStaleness(): Staleness = myInvoicesStaleness

    override fun invalidateMyInvoices() {
        myInvoicesStaleness.reset()
    }

    override suspend fun clear() {
        myInvoicesStaleness.reset()
    }
}
