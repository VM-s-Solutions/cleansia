package cz.cleansia.partner.core.notifications

import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.network.safeApiCall
import cz.cleansia.core.notifications.DeviceRegistrationClient
import cz.cleansia.core.notifications.RegisterDeviceRequest
import cz.cleansia.partner.api.client.DeviceApi as GenDeviceApi
import cz.cleansia.partner.api.model.RegisterDeviceCommand as GenRegisterDeviceCommand
import kotlinx.serialization.json.Json
import javax.inject.Inject

/**
 * Partner-app binding of the shared [DeviceRegistrationClient] over the
 * OpenAPI-generated [GenDeviceApi]. Push-notification device registration —
 * the FCM token + platform tuple goes into the backend so it can target this
 * device on push fanout.
 */
class DeviceApiClient @Inject constructor(
    private val deviceApi: GenDeviceApi,
    private val json: Json,
) : DeviceRegistrationClient {

    override suspend fun register(request: RegisterDeviceRequest): Boolean {
        val result = safeApiCall(json) {
            deviceApi.deviceRegister(
                registerDeviceCommand = GenRegisterDeviceCommand(
                    deviceId = request.deviceId,
                    deviceToken = request.deviceToken,
                    platform = request.platform,
                ),
            )
        }
        return result is ApiResult.Success
    }

    override suspend fun unregister(deviceId: String): Boolean {
        val result = safeApiCall(json) { deviceApi.deviceUnregister(deviceId = deviceId) }
        return result is ApiResult.Success
    }
}
