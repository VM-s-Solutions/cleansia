package cz.cleansia.partner.core.network

import cz.cleansia.partner.domain.models.dashboard.DashboardStats
import cz.cleansia.partner.domain.models.dashboard.EarningsAnalytics
import cz.cleansia.partner.domain.models.dashboard.EarningsSummary
import cz.cleansia.partner.domain.models.dashboard.OrderAnalyticsResponse
import cz.cleansia.partner.domain.models.dashboard.ProductivityMetricsResponse
import cz.cleansia.partner.domain.models.dashboard.TimeAnalyticsResponse
import cz.cleansia.partner.domain.models.dashboard.UpcomingOrder
import cz.cleansia.partner.domain.models.invoices.Invoice
import cz.cleansia.partner.domain.models.invoices.InvoiceDetail
import cz.cleansia.partner.domain.models.invoices.PagedInvoiceResponse
import cz.cleansia.partner.domain.models.orders.CompleteOrderRequest
import cz.cleansia.partner.domain.models.orders.NotifyOnTheWayRequest
import cz.cleansia.partner.domain.models.orders.NotifyOnTheWayResponse
import cz.cleansia.partner.domain.models.orders.Order
import cz.cleansia.partner.domain.models.orders.OrderDetail
import cz.cleansia.partner.domain.models.orders.PagedOrderResponse
import cz.cleansia.partner.domain.models.orders.StartOrderRequest
import cz.cleansia.partner.domain.models.orders.StartOrderResponse
import cz.cleansia.partner.domain.models.orders.TakeOrderRequest
import cz.cleansia.partner.domain.models.orders.TakeOrderResponse
import cz.cleansia.partner.domain.models.orders.CompleteOrderResponse
import cz.cleansia.partner.domain.models.profile.Country
import cz.cleansia.partner.domain.models.profile.EmployeeDocument
import cz.cleansia.partner.domain.models.profile.EmployeeProfile
import cz.cleansia.partner.domain.models.profile.GetMyDocumentsResponse
import cz.cleansia.partner.domain.models.profile.SaveMyDocumentsRequest
import cz.cleansia.partner.domain.models.profile.SaveMyDocumentsResponse
import cz.cleansia.partner.domain.models.profile.RegistrationCompletionStatus
import cz.cleansia.partner.domain.models.profile.UpdateAddressInfoRequest
import cz.cleansia.partner.domain.models.profile.UpdateAvailabilityRequest
import cz.cleansia.partner.domain.models.profile.UpdateBankDetailsRequest
import cz.cleansia.partner.domain.models.profile.UpdateEmergencyContactRequest
import cz.cleansia.partner.domain.models.profile.UpdateIdentificationInfoRequest
import cz.cleansia.partner.domain.models.profile.UpdatePersonalInfoRequest
import cz.cleansia.partner.domain.models.profile.UpdateSectionResponse
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

    // Auth endpoints removed — see cz.cleansia.partner.domain.repositories.AuthRepository
    // which now uses the OpenAPI-generated cz.cleansia.partner.api.client.AuthApi
    // directly. Migration of the remaining endpoints (Dashboard / Orders / Profile /
    // Invoices) tracked under phase 5 of arch-001's auth-migration follow-up.

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

    @GET("Dashboard/GetOrderAnalytics")
    suspend fun getOrderAnalytics(
        @Query("employeeId") employeeId: String? = null,
        @Query("startDate") startDate: String? = null,
        @Query("endDate") endDate: String? = null
    ): Response<OrderAnalyticsResponse>

    @GET("Dashboard/GetTimeAnalytics")
    suspend fun getTimeAnalytics(
        @Query("employeeId") employeeId: String? = null,
        @Query("startDate") startDate: String? = null,
        @Query("endDate") endDate: String? = null
    ): Response<TimeAnalyticsResponse>

    @GET("Dashboard/GetProductivityMetrics")
    suspend fun getProductivityMetrics(
        @Query("employeeId") employeeId: String? = null,
        @Query("startDate") startDate: String? = null,
        @Query("endDate") endDate: String? = null
    ): Response<ProductivityMetricsResponse>

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
    suspend fun takeOrder(@Body request: TakeOrderRequest): Response<TakeOrderResponse>

    @POST("Order/StartOrder")
    suspend fun startOrder(@Body request: StartOrderRequest): Response<StartOrderResponse>

    @POST("Order/NotifyOnTheWay")
    suspend fun notifyOnTheWay(@Body request: NotifyOnTheWayRequest): Response<NotifyOnTheWayResponse>

    @POST("Order/CompleteOrder")
    suspend fun completeOrder(@Body request: CompleteOrderRequest): Response<CompleteOrderResponse>

    @POST("Order/UploadPhoto")
    suspend fun uploadOrderPhoto(
        @Body request: cz.cleansia.partner.domain.models.orders.UploadOrderPhotoRequest
    ): Response<cz.cleansia.partner.domain.models.orders.UploadOrderPhotoResponse>

    @POST("Order/AddNote")
    suspend fun addOrderNote(
        @Body request: cz.cleansia.partner.domain.models.orders.AddOrderNoteRequest
    ): Response<cz.cleansia.partner.domain.models.orders.AddOrderNoteResponse>

    @POST("Order/ReportIssue")
    suspend fun reportOrderIssue(
        @Body request: cz.cleansia.partner.domain.models.orders.ReportOrderIssueRequest
    ): Response<cz.cleansia.partner.domain.models.orders.ReportOrderIssueResponse>

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

    // ===== Countries =====

    @GET("Country/GetOverview")
    suspend fun getCountries(): Response<List<Country>>

    // ===== Profile =====

    @GET("Employee/CheckCurrentEmployee")
    suspend fun checkCurrentEmployee(): Response<RegistrationCompletionStatus>

    @GET("Employee/GetCurrentEmployee")
    suspend fun getCurrentEmployee(): Response<EmployeeProfile>

    @PUT("Employee/UpdateEmployee")
    suspend fun updateEmployee(@Body profile: EmployeeProfile): Response<EmployeeProfile>

    // Per-section profile updates

    @PUT("Employee/UpdatePersonalInfo")
    suspend fun updatePersonalInfo(@Body request: UpdatePersonalInfoRequest): Response<UpdateSectionResponse>

    @PUT("Employee/UpdateIdentificationInfo")
    suspend fun updateIdentificationInfo(@Body request: UpdateIdentificationInfoRequest): Response<UpdateSectionResponse>

    @PUT("Employee/UpdateAddressInfo")
    suspend fun updateAddressInfo(@Body request: UpdateAddressInfoRequest): Response<UpdateSectionResponse>

    @PUT("Employee/UpdateBankDetails")
    suspend fun updateBankDetails(@Body request: UpdateBankDetailsRequest): Response<UpdateSectionResponse>

    @PUT("Employee/UpdateEmergencyContact")
    suspend fun updateEmergencyContact(@Body request: UpdateEmergencyContactRequest): Response<UpdateSectionResponse>

    @PUT("Employee/UpdateAvailability")
    suspend fun updateAvailability(@Body request: UpdateAvailabilityRequest): Response<UpdateSectionResponse>

    @GET("Employee/GetMyDocuments")
    suspend fun getMyDocuments(): Response<GetMyDocumentsResponse>

    @POST("Employee/SaveMyDocuments")
    suspend fun saveDocuments(
        @Body request: SaveMyDocumentsRequest
    ): Response<SaveMyDocumentsResponse>

    @DELETE("Employee/DeleteMyDocument")
    suspend fun deleteDocument(@Query("documentId") documentId: String): Response<Unit>

    @GET("Employee/DownloadMyDocument")
    suspend fun downloadDocument(@Query("documentId") documentId: String): Response<ResponseBody>

    @Multipart
    @POST("Employee/UploadProfilePhoto")
    suspend fun uploadProfilePhoto(
        @Part photo: MultipartBody.Part
    ): Response<EmployeeProfile>
}
