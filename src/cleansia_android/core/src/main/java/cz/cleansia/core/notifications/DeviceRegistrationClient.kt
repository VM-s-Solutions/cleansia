package cz.cleansia.core.notifications

/**
 * Per-app binding seam for the push-notification device endpoints. Each app
 * implements this over its own OpenAPI-generated `DeviceApi` client; the
 * shared [PushTokenRepository] depends only on this interface so the lifecycle
 * logic lives once in `:core`.
 *
 * Implementations return `true` only when the backend accepted the call so the
 * repository caches the last-registered token exclusively on success.
 */
interface DeviceRegistrationClient {

    suspend fun register(request: RegisterDeviceRequest): Boolean

    suspend fun unregister(deviceId: String): Boolean
}
