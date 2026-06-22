package cz.cleansia.core.notifications

import kotlinx.serialization.Serializable

@Serializable
data class RegisterDeviceRequest(
    val deviceId: String,
    val deviceToken: String,
    val platform: String,
)
