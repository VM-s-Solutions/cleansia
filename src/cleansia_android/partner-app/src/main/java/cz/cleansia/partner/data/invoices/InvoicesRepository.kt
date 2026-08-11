package cz.cleansia.partner.data.invoices

import cz.cleansia.core.auth.SessionScopedCache
import cz.cleansia.core.freshness.Staleness
import cz.cleansia.partner.api.client.EmployeePayrollApi
import cz.cleansia.partner.api.model.EmployeeInvoiceDetailDto
import cz.cleansia.partner.api.model.EmployeeInvoiceDto
import cz.cleansia.partner.api.model.EmployeeInvoiceStatus
import cz.cleansia.partner.api.model.SortDefinition
import cz.cleansia.partner.api.model.SortDirection
import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.network.safeApiCall
import cz.cleansia.core.network.mapWire
import cz.cleansia.partner.data.payroll.OrderPayLine
import cz.cleansia.partner.data.payroll.toDomainOrNull
import cz.cleansia.core.network.required
import kotlinx.serialization.json.Json
import okhttp3.ResponseBody
import javax.inject.Inject
import javax.inject.Singleton

interface InvoicesRepository {
    suspend fun getMyInvoices(employeeId: String): ApiResult<List<Invoice>>
    suspend fun getById(invoiceId: String): ApiResult<InvoiceDetail>
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

/**
 * The list and detail schemas are modelled separately rather than as a shared core plus extras:
 * `specificSymbol` and `orderPays` exist only on the detail, and one merged type would have to make
 * them nullable, leaving a reader unable to tell "this endpoint never sends it" from "this invoice
 * doesn't have one".
 */
data class Invoice(
    val id: String,
    val employeeId: String?,
    val employeeName: String?,
    val payPeriodId: String?,
    val payPeriodLabel: String?,
    val invoiceNumber: String?,
    val variableSymbol: String?,
    val paymentReference: String?,
    val totalOrders: Int,
    val subTotal: Double,
    val bonusAmount: Double,
    val deductionAmount: Double,
    val totalAmount: Double,
    val currencyCode: String?,
    val status: EmployeeInvoiceStatus,
    val pdfBlobName: String?,
    val pdfGenerationFailed: Boolean,
    val pdfGenerationError: String?,
    val generatedAt: String?,
    val approvedAt: String?,
    val approvedBy: String?,
    val paidAt: String?,
    val adminNotes: String?,
    val bankTransferNote: String?,
)

data class InvoiceDetail(
    val id: String,
    val employeeId: String?,
    val employeeName: String?,
    val payPeriodId: String?,
    val payPeriodLabel: String?,
    val invoiceNumber: String?,
    val variableSymbol: String?,
    val specificSymbol: String?,
    val paymentReference: String?,
    val totalOrders: Int,
    val subTotal: Double,
    val bonusAmount: Double,
    val deductionAmount: Double,
    val totalAmount: Double,
    val currencyCode: String?,
    val status: EmployeeInvoiceStatus,
    val pdfBlobName: String?,
    val pdfGenerationFailed: Boolean,
    val pdfGenerationError: String?,
    val generatedAt: String?,
    val approvedAt: String?,
    val approvedBy: String?,
    val paidAt: String?,
    val adminNotes: String?,
    val bankTransferNote: String?,
    val orderPays: List<OrderPayLine>,
)

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

    override suspend fun getMyInvoices(employeeId: String): ApiResult<List<Invoice>> =
        safeApiCall(json) {
            payrollApi.employeePayrollGetPagedInvoices(
                filterEmployeeId = employeeId,
                sort = listOf(SortDefinition(field = "createdOn", direction = SortDirection._1)),
                offset = 0,
                limit = 50,
            )
        }.mapWire { page -> page.data.orEmpty().map { it.toDomain() } }
            .also { result ->
                // Only stamp the watermark on success — a failed fetch
                // should NOT advance the stale gate, otherwise the next
                // entry would think we have fresh data when we just
                // logged a network error and have nothing new to show.
                if (result is ApiResult.Success) myInvoicesStaleness.markFresh()
            }

    override suspend fun getById(invoiceId: String): ApiResult<InvoiceDetail> =
        safeApiCall(json) { payrollApi.employeePayrollGetInvoiceById(invoiceId) }
            .mapWire { it.toDomain() }

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

/**
 * Strict where the period-pay line mapper drops. `id` is nullable by spec here too, but an invoice is
 * an addend of the list screen's hero rollup, so dropping an unmappable row would silently subtract
 * its total from the figure a cleaner reads as what they are owed — a smaller, plausible, unmarked
 * number. Refusing the page is the only outcome that cannot show a wrong total; the alternative,
 * marking the row, still leaves the sum undefined.
 *
 * `pdfGenerationFailed` is refused rather than defaulted because it is the only durable signal that
 * a render failed (ADR-0046 E5) — `false` reports a healthy document that may not exist. `status` is
 * refused on the same ground: it is the invoice's paid-or-not assertion, and a placeholder badge
 * across a whole list is silent degradation, not an honest gap.
 *
 * `generatedAt` is `nullable: false` yet stays nullable: the card already renders its absence, so
 * leaving it null fabricates nothing. The rule is that coercion must not invent a fact the cleaner
 * reads, not that every spec-required field must be refused.
 */
internal fun EmployeeInvoiceDto.toDomain() = Invoice(
    id = id.required("id"),
    employeeId = employeeId,
    employeeName = employeeName,
    payPeriodId = payPeriodId,
    payPeriodLabel = payPeriodLabel,
    invoiceNumber = invoiceNumber,
    variableSymbol = variableSymbol,
    paymentReference = paymentReference,
    totalOrders = totalOrders.required("totalOrders"),
    subTotal = subTotal.required("subTotal"),
    bonusAmount = bonusAmount.required("bonusAmount"),
    deductionAmount = deductionAmount.required("deductionAmount"),
    totalAmount = totalAmount.required("totalAmount"),
    currencyCode = currencyCode,
    status = status.required("status"),
    pdfBlobName = pdfBlobName,
    pdfGenerationFailed = pdfGenerationFailed.required("pdfGenerationFailed"),
    pdfGenerationError = pdfGenerationError,
    generatedAt = generatedAt,
    approvedAt = approvedAt,
    approvedBy = approvedBy,
    paidAt = paidAt,
    adminNotes = adminNotes,
    bankTransferNote = bankTransferNote,
)

/**
 * `orderPays` keeps the period-pay ruling — drop the unidentifiable line — because the detail screen
 * renders money from this invoice's own `subTotal`/`bonusAmount`/`deductionAmount`/`totalAmount` and
 * never from a sum over the lines, so a lost line cannot move a figure.
 */
internal fun EmployeeInvoiceDetailDto.toDomain() = InvoiceDetail(
    id = id.required("id"),
    employeeId = employeeId,
    employeeName = employeeName,
    payPeriodId = payPeriodId,
    payPeriodLabel = payPeriodLabel,
    invoiceNumber = invoiceNumber,
    variableSymbol = variableSymbol,
    specificSymbol = specificSymbol,
    paymentReference = paymentReference,
    totalOrders = totalOrders.required("totalOrders"),
    subTotal = subTotal.required("subTotal"),
    bonusAmount = bonusAmount.required("bonusAmount"),
    deductionAmount = deductionAmount.required("deductionAmount"),
    totalAmount = totalAmount.required("totalAmount"),
    currencyCode = currencyCode,
    status = status.required("status"),
    pdfBlobName = pdfBlobName,
    pdfGenerationFailed = pdfGenerationFailed.required("pdfGenerationFailed"),
    pdfGenerationError = pdfGenerationError,
    generatedAt = generatedAt,
    approvedAt = approvedAt,
    approvedBy = approvedBy,
    paidAt = paidAt,
    adminNotes = adminNotes,
    bankTransferNote = bankTransferNote,
    orderPays = orderPays.orEmpty().mapNotNull { it.toDomainOrNull() },
)
