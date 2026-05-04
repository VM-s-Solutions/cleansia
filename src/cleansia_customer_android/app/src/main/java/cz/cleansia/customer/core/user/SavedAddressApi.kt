package cz.cleansia.customer.core.user

import retrofit2.Response
import retrofit2.http.Body
import retrofit2.http.DELETE
import retrofit2.http.GET
import retrofit2.http.POST
import retrofit2.http.PUT
import retrofit2.http.Path

interface SavedAddressApi {
    @GET("api/SavedAddress/GetMine")
    suspend fun getMine(): Response<List<SavedAddressDto>>

    @POST("api/SavedAddress/Add")
    suspend fun add(@Body command: AddSavedAddressCommand): Response<SavedAddressDto>

    @PUT("api/SavedAddress/Update")
    suspend fun update(@Body command: UpdateSavedAddressCommand): Response<SavedAddressDto>

    @POST("api/SavedAddress/SetDefault")
    suspend fun setDefault(@Body command: SetDefaultSavedAddressCommand): Response<Unit>

    @DELETE("api/SavedAddress/Delete/{id}")
    suspend fun delete(@Path("id") id: String): Response<Unit>
}
