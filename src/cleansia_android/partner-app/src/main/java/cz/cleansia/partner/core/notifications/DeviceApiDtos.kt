package cz.cleansia.partner.core.notifications

import kotlinx.serialization.Serializable

@Serializable
data class RegisterDeviceRequest(
    val deviceId: String,
    val deviceToken: String,
    val platform: String,
)

@Serializable
data class RegisterDeviceResponse(val deviceId: String)

@Serializable
data class UnregisterDeviceResponse(val success: Boolean)
