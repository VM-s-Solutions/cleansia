package cz.cleansia.partner.core.network

import cz.cleansia.partner.domain.models.auth.ConfirmEmailRequest
import cz.cleansia.partner.domain.models.auth.ForgotPasswordRequest
import cz.cleansia.partner.domain.models.auth.LoginRequest
import cz.cleansia.partner.domain.models.auth.LoginResponse
import cz.cleansia.partner.domain.models.auth.RegisterRequest
import cz.cleansia.partner.domain.models.auth.RegisterResponse
import cz.cleansia.partner.domain.models.auth.ResendConfirmationRequest
import cz.cleansia.partner.domain.models.dashboard.DashboardStats
import cz.cleansia.partner.domain.models.dashboard.EarningsAnalytics
import cz.cleansia.partner.domain.models.dashboard.EarningsSummary
import cz.cleansia.partner.domain.models.dashboard.UpcomingOrder
import cz.cleansia.partner.domain.models.invoices.Invoice
import cz.cleansia.partner.domain.models.invoices.InvoiceDetail
import cz.cleansia.partner.domain.models.invoices.PagedInvoiceResponse
import cz.cleansia.partner.domain.models.orders.CompleteOrderRequest
import cz.cleansia.partner.domain.models.orders.Order
import cz.cleansia.partner.domain.models.orders.OrderDetail
import cz.cleansia.partner.domain.models.orders.PagedOrderResponse
import cz.cleansia.partner.domain.models.orders.StartOrderRequest
import cz.cleansia.partner.domain.models.orders.TakeOrderRequest
import cz.cleansia.partner.domain.models.profile.EmployeeDocument
import cz.cleansia.partner.domain.models.profile.EmployeeProfile
import okhttp3.MultipartBody
import okhttp3.ResponseBody
import retrofit2.Response
import retrofit2.http.Body
import retrofit2.http.DELETE
import retrofit2.http.GET
import retrofit2.http.Multipart
import retrofit2.http.POST
import retrofit2.http.PUT
import retrofit2.http.Part
import retrofit2.http.Path
import retrofit2.http.Query

/**
 * Retrofit API service interface defining all API endpoints
 */
interface ApiService {

    // ===== Auth =====

    @POST("Auth/Login")
    suspend fun login(@Body request: LoginRequest): Response<LoginResponse>

    @POST("Auth/RegisterEmployee")
    suspend fun register(@Body request: RegisterRequest): Response<RegisterResponse>

    @POST("Auth/ConfirmUserEmail")
    suspend fun confirmEmail(@Body request: ConfirmEmailRequest): Response<LoginResponse>

    @POST("Auth/ResendConfirmationEmail")
    suspend fun resendConfirmation(@Body request: ResendConfirmationRequest): Response<Unit>

    @POST("Auth/ForgotPassword")
    suspend fun forgotPassword(@Body request: ForgotPasswordRequest): Response<Unit>

    // ===== Dashboard =====

    @GET("Dashboard/GetStats")
    suspend fun getDashboardStats(
        @Query("employeeId") employeeId: String? = null
    ): Response<DashboardStats>

    @GET("Dashboard/GetEarningsAnalytics")
    suspend fun getEarningsAnalytics(
        @Query("employeeId") employeeId: String? = null,
        @Query("startDate") startDate: String? = null,
        @Query("endDate") endDate: String? = null
    ): Response<EarningsAnalytics>

    @GET("Dashboard/GetUpcomingOrders")
    suspend fun getUpcomingOrders(
        @Query("employeeId") employeeId: String? = null,
        @Query("limit") limit: Int = 5
    ): Response<List<UpcomingOrder>>

    @GET("Dashboard/GetEarningsSummary")
    suspend fun getEarningsSummary(
        @Query("employeeId") employeeId: String? = null
    ): Response<EarningsSummary>

    // ===== Orders =====

    @GET("Order/GetPaged")
    suspend fun getOrders(
        @Query("Offset") offset: Int = 0,
        @Query("Limit") limit: Int = 20,
        @Query("Filter.OrderStatuses") orderStatuses: List<Int>? = null,
        @Query("Filter.PaymentStatuses") paymentStatuses: List<Int>? = null,
        @Query("Filter.EmployeeId") employeeId: String? = null,
        @Query("Filter.HasAvailableSpots") hasAvailableSpots: Boolean? = null,
        @Query("Filter.ExcludeEmployeeId") excludeEmployeeId: String? = null,
        @Query("Filter.CustomerName") customerName: String? = null,
        @Query("Filter.CustomerEmail") customerEmail: String? = null,
        @Query("Filter.DisplayOrderNumber") displayOrderNumber: String? = null,
        @Query("Filter.CleaningDateFrom") cleaningDateFrom: String? = null,
        @Query("Filter.CleaningDateTo") cleaningDateTo: String? = null,
        @Query("Sort[0].field") sortField: String? = null,
        @Query("Sort[0].direction") sortDirection: Int? = null
    ): Response<PagedOrderResponse>

    @GET("Order/GetById")
    suspend fun getOrderById(@Query("OrderId") orderId: String): Response<OrderDetail>

    @POST("Order/TakeOrder")
    suspend fun takeOrder(@Body request: TakeOrderRequest): Response<OrderDetail>

    @POST("Order/StartOrder")
    suspend fun startOrder(@Body request: StartOrderRequest): Response<OrderDetail>

    @POST("Order/CompleteOrder")
    suspend fun completeOrder(@Body request: CompleteOrderRequest): Response<OrderDetail>

    @Multipart
    @POST("Order/UploadPhoto")
    suspend fun uploadOrderPhoto(
        @Query("orderId") orderId: String,
        @Part photo: MultipartBody.Part,
        @Part type: MultipartBody.Part
    ): Response<Unit>

    // ===== Invoices =====

    @GET("EmployeePayroll/GetPagedInvoices")
    suspend fun getInvoices(
        @Query("Offset") offset: Int = 0,
        @Query("Limit") limit: Int = 20,
        @Query("Filter.Statuses") statuses: List<Int>? = null,
        @Query("Filter.InvoiceNumber") invoiceNumber: String? = null,
        @Query("Filter.DateFrom") dateFrom: String? = null,
        @Query("Filter.DateTo") dateTo: String? = null,
        @Query("Sort[0].field") sortField: String? = null,
        @Query("Sort[0].direction") sortDirection: Int? = null
    ): Response<PagedInvoiceResponse>

    @GET("EmployeePayroll/GetInvoiceById/{id}")
    suspend fun getInvoiceById(@Path("id") invoiceId: String): Response<InvoiceDetail>

    @GET("EmployeePayroll/DownloadInvoice/{id}")
    suspend fun downloadInvoicePdf(@Path("id") invoiceId: String): Response<ResponseBody>

    // ===== Profile =====

    @GET("Employee/GetCurrentEmployee")
    suspend fun getCurrentEmployee(): Response<EmployeeProfile>

    @PUT("Employee/UpdateEmployee")
    suspend fun updateEmployee(@Body profile: EmployeeProfile): Response<EmployeeProfile>

    @GET("Employee/GetMyDocuments")
    suspend fun getMyDocuments(): Response<List<EmployeeDocument>>

    @Multipart
    @POST("Employee/SaveMyDocuments")
    suspend fun saveDocuments(
        @Part documents: List<MultipartBody.Part>
    ): Response<List<EmployeeDocument>>

    @DELETE("Employee/DeleteMyDocument")
    suspend fun deleteDocument(@Query("documentId") documentId: String): Response<Unit>

    @GET("Employee/DownloadMyDocument")
    suspend fun downloadDocument(@Query("documentId") documentId: String): Response<ResponseBody>
}
