package cz.cleansia.customer.core.devices

import kotlinx.serialization.Serializable
import retrofit2.Response
import retrofit2.http.DELETE
import retrofit2.http.GET
import retrofit2.http.Path
import retrofit2.http.Query

/**
 * Hand-written Retrofit interface for the device self-service endpoints
 * (list my devices + revoke one). Hand-written rather than generated because
 * the checked-in OpenAPI spec is refreshed by the owner from a running host
 * and doesn't carry these endpoints yet — same precedent as [cz.cleansia.customer.core.auth.AuthApi].
 */
interface DeviceManagementApi {

    @GET("api/Device/Mine")
    suspend fun mine(
        @Query("currentDeviceId") currentDeviceId: String?,
    ): Response<List<UserDeviceDto>>

    @DELETE("api/Device/{deviceRowId}")
    suspend fun revoke(
        @Path("deviceRowId") deviceRowId: String,
    ): Response<RevokeDeviceResponse>
}

@Serializable
data class UserDeviceDto(
    val id: String,
    val platform: String? = null,
    val deviceId: String? = null,
    /** ISO-8601 with offset, e.g. "2026-06-10T12:34:56+00:00". */
    val lastActiveAt: String? = null,
    /** True when [deviceId] matches the `currentDeviceId` the caller sent. */
    val isCurrent: Boolean = false,
)

@Serializable
data class RevokeDeviceResponse(
    val success: Boolean = false,
)
