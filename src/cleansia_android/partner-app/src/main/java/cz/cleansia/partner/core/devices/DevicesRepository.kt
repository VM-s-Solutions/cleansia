package cz.cleansia.partner.core.devices

import cz.cleansia.core.auth.DeviceIdProvider
import cz.cleansia.partner.core.network.ApiResult
import cz.cleansia.partner.core.network.safeApiCall
import kotlinx.serialization.json.Json
import javax.inject.Inject
import javax.inject.Singleton

interface DevicesRepository {
    /**
     * The stable per-install id of THIS handset — the same id push
     * registration uses. Sent as `currentDeviceId` so the backend can flag
     * the row the caller is on; the screen hides revoke for that row.
     */
    val currentDeviceId: String

    suspend fun getMyDevices(): ApiResult<List<UserDeviceDto>>

    /** Revoke by the server-side row id — kills push + that device's session. */
    suspend fun revoke(deviceRowId: String): ApiResult<RevokeDeviceResponse>
}

@Singleton
class DevicesRepositoryImpl @Inject constructor(
    private val api: DeviceManagementApi,
    private val json: Json,
    private val deviceIdProvider: DeviceIdProvider,
) : DevicesRepository {

    override val currentDeviceId: String get() = deviceIdProvider.deviceId

    override suspend fun getMyDevices(): ApiResult<List<UserDeviceDto>> =
        safeApiCall(json) { api.mine(deviceIdProvider.deviceId) }

    override suspend fun revoke(deviceRowId: String): ApiResult<RevokeDeviceResponse> =
        safeApiCall(json) { api.revoke(deviceRowId) }
}
