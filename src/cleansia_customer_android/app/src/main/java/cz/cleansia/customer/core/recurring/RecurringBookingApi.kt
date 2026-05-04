package cz.cleansia.customer.core.recurring

import retrofit2.Response
import retrofit2.http.Body
import retrofit2.http.GET
import retrofit2.http.POST

/**
 * Retrofit binding for the customer RecurringBooking endpoints. UserId is
 * enriched server-side from the JWT NameIdentifier claim — never sent.
 */
interface RecurringBookingApi {
    @GET("api/RecurringBooking/GetMine")
    suspend fun getMine(): Response<List<RecurringBookingTemplateDto>>

    @POST("api/RecurringBooking/Create")
    suspend fun create(@Body body: CreateRecurringBookingRequest): Response<RecurringBookingTemplateDto>

    @POST("api/RecurringBooking/Update")
    suspend fun update(@Body body: UpdateRecurringBookingRequest): Response<RecurringBookingTemplateDto>

    @POST("api/RecurringBooking/SetActive")
    suspend fun setActive(@Body body: SetRecurringBookingActiveRequest): Response<Unit>

    @POST("api/RecurringBooking/Delete")
    suspend fun delete(@Body body: DeleteRecurringBookingRequest): Response<Unit>
}
