package cz.cleansia.customer.core.orders

import okhttp3.ResponseBody
import retrofit2.Response
import retrofit2.http.Body
import retrofit2.http.GET
import retrofit2.http.POST
import retrofit2.http.Query
import retrofit2.http.Streaming

/**
 * Retrofit binding for the customer Order endpoints.
 *
 * Routes mirror [Cleansia.Web.Customer.Controllers.OrderController]:
 *  - `GET  /api/Order/GetMyOrders`     → paged wrapper `PagedData<OrderListItem>`
 *  - `GET  /api/Order/GetById?OrderId=…` → `OrderItem` (OrderDetailDto)
 *  - `POST /api/Order/Cancel`          → `CancelOrder.Response`
 *  - `POST /api/Order/SubmitReview`    → `OrderReviewDto`
 *  - `GET  /api/Order/DownloadReceipt?OrderId=…` → raw PDF bytes (streamed)
 *  - `GET  /api/Order/GetPhotos?OrderId=…` → `GetOrderPhotos.Response`
 *
 * Note that GetById / DownloadReceipt / GetPhotos take `OrderId` as a query
 * parameter, not a path segment — the backend uses `[FromQuery]` on query
 * record types.
 *
 * Cancel and SubmitReview servers-enrich the `UserId` field from the JWT
 * claims before the handler runs, so the request bodies do NOT include it.
 *
 * Query-parameter names are case-sensitive PascalCase (matches ASP.NET model
 * binding defaults): `Offset`, `Limit`, `OrderId`.
 */
interface OrderApi {
    @GET("api/Order/GetMyOrders")
    suspend fun getMyOrders(
        @Query("Offset") offset: Int = 0,
        @Query("Limit") limit: Int = 20,
    ): Response<OrderListResponseDto>

    @GET("api/Order/GetById")
    suspend fun getById(@Query("OrderId") id: String): Response<OrderDetailDto>

    @POST("api/Order/Cancel")
    suspend fun cancel(@Body body: CancelOrderRequest): Response<CancelOrderResponse>

    @POST("api/Order/SubmitReview")
    suspend fun submitReview(@Body body: SubmitReviewRequest): Response<OrderReviewDto>

    /**
     * Receipt PDF is returned via `File(…)` result on the backend — raw bytes
     * with `Content-Type: application/pdf`. `@Streaming` keeps OkHttp from
     * buffering the full body into memory; the repo copies the stream to disk.
     */
    @GET("api/Order/DownloadReceipt")
    @Streaming
    suspend fun downloadReceipt(@Query("OrderId") id: String): Response<ResponseBody>

    @GET("api/Order/GetPhotos")
    suspend fun getPhotos(@Query("OrderId") id: String): Response<OrderPhotosResponse>

    /**
     * Cleaners the calling user has had a Completed order with — feeds the
     * "request your favorite cleaner" picker on the booking flow. Sorted by
     * most-recent service date, capped at 20 server-side.
     */
    @GET("api/Order/MyServingCleaners")
    suspend fun getMyServingCleaners(): Response<List<ServingCleanerDto>>
}
