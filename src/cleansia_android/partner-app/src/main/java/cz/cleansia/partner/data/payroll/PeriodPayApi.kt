package cz.cleansia.partner.data.payroll

import kotlinx.serialization.Serializable
import retrofit2.Response
import retrofit2.http.GET
import retrofit2.http.Query

/**
 * Hand-written Retrofit interface for the read-only "my period pay" endpoint
 * (the cleaner-facing surface left on the mobile-partner host after the
 * settlement writes moved admin-side). Hand-written rather than generated
 * because the checked-in OpenAPI spec is refreshed by the owner from a
 * running host and doesn't carry this endpoint yet — same precedent as the
 * customer app's hand-written AuthApi.
 *
 * The backend scopes the result to the caller's own EmployeeId server-side;
 * a foreign employeeId comes back as employee.not_found.
 */
interface PeriodPayApi {

    @GET("api/EmployeePayroll/GetPeriodPays")
    suspend fun getPeriodPays(
        @Query("EmployeeId") employeeId: String,
        @Query("PayPeriodId") payPeriodId: String,
    ): Response<PeriodPaySummary>
}

@Serializable
data class PeriodPaySummary(
    val payPeriodId: String? = null,
    val payPeriodLabel: String? = null,
    val employeeId: String? = null,
    val employeeName: String? = null,
    val totalOrders: Int = 0,
    val totalBasePay: Double = 0.0,
    val totalExtrasPay: Double = 0.0,
    val totalExpensesPay: Double = 0.0,
    val totalBonusPay: Double = 0.0,
    val totalDeductionPay: Double = 0.0,
    val grandTotal: Double = 0.0,
    val hasInvoice: Boolean = false,
    val invoiceId: String? = null,
    val orderPays: List<OrderPayLine> = emptyList(),
)

@Serializable
data class OrderPayLine(
    val id: String,
    val orderId: String? = null,
    val orderNumber: String? = null,
    val basePay: Double = 0.0,
    val extrasPay: Double = 0.0,
    val expensesPay: Double = 0.0,
    val bonusPay: Double = 0.0,
    val deductionPay: Double = 0.0,
    val totalPay: Double = 0.0,
    val isApproved: Boolean = false,
    val createdOn: String? = null,
)
