package cz.cleansia.partner.mock

import cz.cleansia.partner.core.network.ApiResult
import cz.cleansia.partner.domain.models.invoices.Invoice
import cz.cleansia.partner.domain.models.invoices.InvoiceDetail
import cz.cleansia.partner.domain.models.invoices.InvoiceFilter
import cz.cleansia.partner.domain.models.invoices.PagedInvoiceResponse
import cz.cleansia.partner.domain.repositories.InvoicesRepository
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.flowOf
import okhttp3.ResponseBody
import okhttp3.ResponseBody.Companion.toResponseBody

class MockInvoicesRepository : InvoicesRepository {

    override suspend fun getInvoices(
        page: Int,
        pageSize: Int,
        filter: InvoiceFilter?,
        sortBy: String?,
        sortDescending: Boolean?
    ): ApiResult<PagedInvoiceResponse> {
        delay(500)
        return ApiResult.Success(
            MockDataProvider.getPagedInvoices(page, pageSize, filter, sortBy, sortDescending)
        )
    }

    override suspend fun getInvoiceById(invoiceId: String): ApiResult<InvoiceDetail> {
        delay(300)
        return ApiResult.Success(MockDataProvider.getInvoiceDetail(invoiceId))
    }

    override suspend fun downloadInvoicePdf(invoiceId: String): ApiResult<ResponseBody> {
        delay(500)
        val mockPdfContent = "%PDF-1.4 mock invoice $invoiceId".toByteArray()
        return ApiResult.Success(mockPdfContent.toResponseBody(null))
    }

    override fun getCachedInvoices(): Flow<List<Invoice>> = flowOf(emptyList())

    override suspend fun getCachedInvoiceById(invoiceId: String): Invoice? = null

    override suspend fun clearCache() { /* no-op */ }
}
