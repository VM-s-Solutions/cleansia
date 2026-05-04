package cz.cleansia.customer.core.disputes

import okhttp3.MultipartBody
import okhttp3.RequestBody
import retrofit2.Response
import retrofit2.http.Body
import retrofit2.http.GET
import retrofit2.http.Multipart
import retrofit2.http.POST
import retrofit2.http.Part
import retrofit2.http.Path
import retrofit2.http.Query

/**
 * Retrofit binding for the customer Dispute endpoints.
 *
 * Routes mirror [Cleansia.Web.Customer.Controllers.DisputeController]:
 *  - `GET  /api/Dispute/GetPaged`            → paged wrapper
 *  - `GET  /api/Dispute/GetById/{disputeId}` → details (PATH parameter!)
 *  - `POST /api/Dispute/Create`              → created dispute id (String)
 *  - `POST /api/Dispute/AddMessage`          → 200 OK, no body
 *  - `POST /api/Dispute/UploadEvidence`      → multipart, returns evidence DTO
 *
 * GetPaged takes `Offset` + `Limit` via `[FromQuery] DataRangeRequest` — same
 * param names as [cz.cleansia.customer.core.orders.OrderApi].
 *
 * Note that GetById uses a PATH segment (`{disputeId}`), NOT a query param —
 * this is different from how Order's GetById works.
 */
interface DisputeApi {
    @GET("api/Dispute/GetPaged")
    suspend fun getPaged(
        @Query("Offset") offset: Int = 0,
        @Query("Limit") limit: Int = 20,
    ): Response<DisputeListResponseDto>

    @GET("api/Dispute/GetById/{disputeId}")
    suspend fun getById(@Path("disputeId") id: String): Response<DisputeDetailsDto>

    @POST("api/Dispute/Create")
    suspend fun create(@Body body: CreateDisputeRequest): Response<String>

    @POST("api/Dispute/AddMessage")
    suspend fun addMessage(@Body body: AddDisputeMessageRequest): Response<Unit>

    /**
     * Upload a single evidence file for an existing dispute. Backend accepts
     * images (jpeg/png/webp) and PDF; max 10MB. Server validates ownership —
     * only the dispute's author may upload.
     *
     * The form field name MUST be `file` (lowercase) to match the controller's
     * `IFormFile file` parameter binding.
     */
    @Multipart
    @POST("api/Dispute/UploadEvidence")
    suspend fun uploadEvidence(
        @Part("disputeId") disputeId: RequestBody,
        @Part file: MultipartBody.Part,
    ): Response<UploadDisputeEvidenceResponse>
}
