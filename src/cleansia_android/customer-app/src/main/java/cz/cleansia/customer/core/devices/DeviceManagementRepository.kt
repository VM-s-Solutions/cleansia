package cz.cleansia.customer.core.devices

import cz.cleansia.core.auth.DeviceIdProvider
import cz.cleansia.core.network.ApiError
import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.network.safeApiCall
import kotlinx.serialization.json.Json
import javax.inject.Inject
import javax.inject.Singleton

/**
 * Device self-service over GET /api/Device/Mine + DELETE /api/Device/{rowId}.
 * Sends THIS install's stable device id as `currentDeviceId` so the backend
 * can flag the row the user is holding (the screen badges it "This device"
 * and turns its revoke into an instant sign-out).
 * Stateless - nothing cached, so no [cz.cleansia.core.auth.SessionScopedCache].
 */
@Singleton
class DeviceManagementRepository @Inject constructor(
    private val api: DeviceManagementApi,
    private val deviceIdProvider: DeviceIdProvider,
    private val json: Json,
) {

    suspend fun getMyDevices(): ApiResult<List<UserDeviceDto>> =
        when (val result = safeApiCall(json) { api.mine(deviceIdProvider.deviceId) }) {
            is ApiResult.Success ->
                ApiResult.Success((result.data as? List<UserDeviceDto>).orEmpty())
            is ApiResult.Error -> result
        }

    /**
     * Revoke by the server-side row id - kills push + that device's session.
     * A `200 { success = false }` is a soft failure the backend reports without
     * an error body; it carries no surfaceable message (legacy showed none), so
     * it maps to the silent [ApiError.Network] channel the ViewModel no-ops.
     */
    suspend fun revoke(deviceRowId: String): ApiResult<Unit> =
        when (val result = safeApiCall(json) { api.revoke(deviceRowId) }) {
            is ApiResult.Success ->
                if (result.data is RevokeDeviceResponse && !result.data.success) ApiResult.Error(ApiError.Network(""))
                else ApiResult.Success(Unit)
            is ApiResult.Error -> result
        }
}
