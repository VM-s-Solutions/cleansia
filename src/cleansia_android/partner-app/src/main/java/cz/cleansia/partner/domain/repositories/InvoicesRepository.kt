package cz.cleansia.partner.domain.repositories

import cz.cleansia.partner.config.Constants
import cz.cleansia.partner.core.database.dao.InvoiceDao
import cz.cleansia.partner.core.database.entities.CachedInvoice
import cz.cleansia.partner.core.network.ApiResult
import cz.cleansia.partner.core.network.ApiService
import cz.cleansia.partner.core.network.NetworkMonitor
import cz.cleansia.partner.core.network.safeApiCall
import cz.cleansia.partner.domain.models.invoices.Invoice
import cz.cleansia.partner.domain.models.invoices.InvoiceDetail
import cz.cleansia.partner.domain.models.invoices.InvoiceFilter
import cz.cleansia.partner.domain.models.invoices.PagedInvoiceResponse
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.map
import kotlinx.serialization.json.Json
import okhttp3.ResponseBody
import javax.inject.Inject
import javax.inject.Singleton

interface InvoicesRepository {
    suspend fun getInvoices(
        page: Int = 1,
        pageSize: Int = Constants.Pagination.DEFAULT_PAGE_SIZE,
        filter: InvoiceFilter? = null,
        sortBy: String? = null,
        sortDescending: Boolean? = null
    ): ApiResult<PagedInvoiceResponse>

    suspend fun getInvoiceById(invoiceId: String): ApiResult<InvoiceDetail>
    suspend fun downloadInvoicePdf(invoiceId: String): ApiResult<ResponseBody>

    // Offline support methods
    fun getCachedInvoices(): Flow<List<Invoice>>
    suspend fun getCachedInvoiceById(invoiceId: String): Invoice?
    suspend fun clearCache()
}

@Singleton
class InvoicesRepositoryImpl @Inject constructor(
    private val apiService: ApiService,
    private val json: Json,
    private val invoiceDao: InvoiceDao,
    private val networkMonitor: NetworkMonitor
) : InvoicesRepository {

    override suspend fun getInvoices(
        page: Int,
        pageSize: Int,
        filter: InvoiceFilter?,
        sortBy: String?,
        sortDescending: Boolean?
    ): ApiResult<PagedInvoiceResponse> {
        val offset = (page - 1) * pageSize
        val result = safeApiCall(json) {
            apiService.getInvoices(
                offset = offset,
                limit = pageSize,
                statuses = filter?.statuses?.map { it.apiValue }?.ifEmpty { null },
                invoiceNumber = filter?.searchTerm,
                dateFrom = filter?.startDate,
                dateTo = filter?.endDate,
                sortField = sortBy,
                sortDirection = sortDescending?.let { if (it) 1 else 0 }
            )
        }

        // Cache successful results (only first page without filters)
        if (result is ApiResult.Success && page == 1 && filter == null) {
            cacheInvoices(result.data.items)
        }

        return result
    }

    /**
     * Cache invoices to local database
     */
    private suspend fun cacheInvoices(invoices: List<Invoice>) {
        try {
            val cachedInvoices = invoices.map { CachedInvoice.fromDomainModel(it) }
            invoiceDao.insertInvoices(cachedInvoices)
        } catch (e: Exception) {
            // Ignore cache errors
        }
    }

    override suspend fun getInvoiceById(invoiceId: String): ApiResult<InvoiceDetail> {
        return safeApiCall(json) {
            apiService.getInvoiceById(invoiceId)
        }
    }

    override suspend fun downloadInvoicePdf(invoiceId: String): ApiResult<ResponseBody> {
        return safeApiCall(json) {
            apiService.downloadInvoicePdf(invoiceId)
        }
    }

    // Offline support methods

    override fun getCachedInvoices(): Flow<List<Invoice>> {
        return invoiceDao.getAllInvoices().map { cachedInvoices ->
            cachedInvoices.map { it.toDomainModel() }
        }
    }

    override suspend fun getCachedInvoiceById(invoiceId: String): Invoice? {
        return invoiceDao.getInvoiceById(invoiceId)?.toDomainModel()
    }

    override suspend fun clearCache() {
        invoiceDao.deleteAllInvoices()
    }
}
