package cz.cleansia.customer.core.notifications

import cz.cleansia.customer.api.client.DeviceApi as GenDeviceApi
import cz.cleansia.customer.api.model.RegisterDeviceCommand as GenRegisterDeviceCommand
import cz.cleansia.customer.api.model.RegisterDeviceResponse as GenRegisterDeviceResponse
import cz.cleansia.customer.api.model.UnregisterDeviceResponse as GenUnregisterDeviceResponse
import retrofit2.Response

/**
 * Adapter over the OpenAPI-generated [GenDeviceApi]. Push-notification device
 * registration — the FCM token + platform tuple goes into the backend so it
 * can target this device on push fanout.
 */
class DeviceApi(
    private val deviceApi: GenDeviceApi,
) {
    suspend fun register(body: RegisterDeviceRequest): Response<RegisterDeviceResponse> {
        val raw = deviceApi.deviceRegister(
            registerDeviceCommand = GenRegisterDeviceCommand(
                deviceId = body.deviceId,
                deviceToken = body.deviceToken,
                platform = body.platform,
            ),
        )
        return raw.mapBody { it.toAppDto() }
    }

    suspend fun unregister(deviceId: String): Response<UnregisterDeviceResponse> {
        val raw = deviceApi.deviceUnregister(deviceId = deviceId)
        return raw.mapBody { it.toAppDto() }
    }
}

private inline fun <T, R : Any> Response<T>.mapBody(transform: (T?) -> R?): Response<R> =
    if (isSuccessful) Response.success(transform(body()), raw())
    else @Suppress("UNCHECKED_CAST") (this as Response<R>)

private fun GenRegisterDeviceResponse?.toAppDto(): RegisterDeviceResponse =
    RegisterDeviceResponse(deviceId = this?.deviceId.orEmpty())

private fun GenUnregisterDeviceResponse?.toAppDto(): UnregisterDeviceResponse =
    UnregisterDeviceResponse(success = this?.success ?: false)
