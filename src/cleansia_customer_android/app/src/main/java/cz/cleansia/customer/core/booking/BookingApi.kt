package cz.cleansia.customer.core.booking

import retrofit2.Response
import retrofit2.http.Body
import retrofit2.http.POST

interface BookingApi {
    @POST("api/Order/Quote")
    suspend fun quote(@Body command: QuoteOrderCommand): Response<QuoteOrderResponse>

    @POST("api/Order/CreateOrder")
    suspend fun create(@Body command: CreateOrderCommand): Response<CreateOrderResponse>
}
