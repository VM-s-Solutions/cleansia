package cz.cleansia.partner.data.payroll

import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.network.safeApiCall
import cz.cleansia.partner.api.client.EmployeePayrollApi
import cz.cleansia.partner.api.model.OrderEmployeePayDto
import cz.cleansia.partner.api.model.PeriodPaySummaryDto
import cz.cleansia.core.network.mapWire
import cz.cleansia.core.network.required
import kotlinx.serialization.json.Json
import javax.inject.Inject
import javax.inject.Singleton

/**
 * The cleaner's own pay for one period. The backend scopes the result to the caller's own
 * EmployeeId server-side; a foreign employeeId comes back as employee.not_found.
 */
interface PeriodPayRepository {
    suspend fun getPeriodPays(employeeId: String, payPeriodId: String): ApiResult<PeriodPaySummary>
}

data class PeriodPaySummary(
    val payPeriodId: String?,
    val payPeriodLabel: String?,
    val employeeId: String?,
    val employeeName: String?,
    val totalOrders: Int,
    val totalBasePay: Double,
    val totalExtrasPay: Double,
    val totalExpensesPay: Double,
    val totalBonusPay: Double,
    val totalDeductionPay: Double,
    val grandTotal: Double,
    val hasInvoice: Boolean,
    val invoiceId: String?,
    val orderPays: List<OrderPayLine>,
    /** The currency every amount above is in, from the server — the invoice's own on an invoiced
     *  period, so this screen and the payout document cannot disagree. Nullable on the wire. */
    val currencyCode: String?,
)

data class OrderPayLine(
    val id: String,
    val orderId: String?,
    val orderNumber: String?,
    val basePay: Double,
    val extrasPay: Double,
    val expensesPay: Double,
    val bonusPay: Double,
    val deductionPay: Double,
    val totalPay: Double,
    val isApproved: Boolean,
    val createdOn: String?,
)

// Stateless — nothing cached, so no SessionScopedCache
@Singleton
class PeriodPayRepositoryImpl @Inject constructor(
    private val payrollApi: EmployeePayrollApi,
    private val json: Json,
) : PeriodPayRepository {

    override suspend fun getPeriodPays(employeeId: String, payPeriodId: String): ApiResult<PeriodPaySummary> =
        safeApiCall(json) { payrollApi.employeePayrollGetPeriodPays(employeeId, payPeriodId) }
            .mapWire { it.toDomain() }
}

/**
 * Only [OrderEmployeePayDto.id] is nullable by spec, and an unidentifiable line is dropped rather
 * than given a synthesized id — the summary's own totals are what the screen renders money from, so
 * one lost row cannot falsify a figure, while failing the whole mapping would blank a period the
 * server answered correctly. Where a collection *is* the addends of a rendered total the ruling
 * inverts; see [cz.cleansia.partner.data.invoices.toDomain].
 */
internal fun PeriodPaySummaryDto.toDomain() = PeriodPaySummary(
    payPeriodId = payPeriodId,
    payPeriodLabel = payPeriodLabel,
    employeeId = employeeId,
    employeeName = employeeName,
    totalOrders = totalOrders.required("totalOrders"),
    totalBasePay = totalBasePay.required("totalBasePay"),
    totalExtrasPay = totalExtrasPay.required("totalExtrasPay"),
    totalExpensesPay = totalExpensesPay.required("totalExpensesPay"),
    totalBonusPay = totalBonusPay.required("totalBonusPay"),
    totalDeductionPay = totalDeductionPay.required("totalDeductionPay"),
    grandTotal = grandTotal.required("grandTotal"),
    hasInvoice = hasInvoice.required("hasInvoice"),
    invoiceId = invoiceId,
    orderPays = orderPays.orEmpty().mapNotNull { it.toDomainOrNull() },
    currencyCode = currencyCode,
)

internal fun OrderEmployeePayDto.toDomainOrNull(): OrderPayLine? {
    val lineId = id ?: return null
    return OrderPayLine(
        id = lineId,
        orderId = orderId,
        orderNumber = orderNumber,
        basePay = basePay.required("basePay"),
        extrasPay = extrasPay.required("extrasPay"),
        expensesPay = expensesPay.required("expensesPay"),
        bonusPay = bonusPay.required("bonusPay"),
        deductionPay = deductionPay.required("deductionPay"),
        totalPay = totalPay.required("totalPay"),
        isApproved = isApproved.required("isApproved"),
        createdOn = createdOn,
    )
}
