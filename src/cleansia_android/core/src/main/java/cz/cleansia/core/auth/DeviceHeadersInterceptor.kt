package cz.cleansia.core.auth

import android.os.Build
import okhttp3.Interceptor
import okhttp3.Response

/**
 * Stamps the two device-identity headers onto EVERY outgoing request, on BOTH
 * OkHttp clients (authenticated and anonymous).
 *
 * - `X-Device-Id`    — the stable per-install id from [DeviceIdProvider], the
 *   SAME id the app registers for push. The backend writes it onto the
 *   refresh-token record at issue and rotation time.
 * - `X-Device-Label` — best-effort human name for the audit list
 *   ("signed in from Pixel 9 Pro").
 *
 * Why this is its own interceptor and not part of [AuthInterceptor]: tokens are
 * *issued* by Login / Register / GoogleAuth / RefreshToken, which all go out on
 * the anonymous client — the one client [AuthInterceptor] is deliberately NOT
 * installed on. While these headers lived there, `RefreshToken.DeviceId` was
 * written null on every token an Android phone ever received, and since
 * `RefreshTokenService.RevokeByDeviceAsync` matches on that column, "Your
 * devices → revoke" could never kill an Android session. Nothing backfills the
 * column either, so the id has to be present on the very first request.
 *
 * This interceptor never touches `Authorization` — that is exactly why it is
 * safe to install on the anonymous client. The Bearer stays in [AuthInterceptor]
 * with its `ANON_ENDPOINTS` guard, on the authenticated client only, because
 * `/api/Auth/Logout` and `/api/Auth/ChangePassword` require a token.
 */
class DeviceHeadersInterceptor(
    private val deviceIdProvider: DeviceIdProvider,
) : Interceptor {

    override fun intercept(chain: Interceptor.Chain): Response {
        val request = chain.request().newBuilder()
            .header(DEVICE_LABEL_HEADER, deviceLabel())
            .header(DEVICE_ID_HEADER, headerSafeDeviceId())
            .build()

        return chain.proceed(request)
    }

    private fun deviceLabel(): String {
        // e.g. "Pixel 9 Pro - Android 15". ASCII-only because HTTP header values
        // reject non-ASCII (OkHttp throws on middle-dot, em-dash, etc).
        // Kept under 120 chars to match the server-side column.
        val device = "${Build.MANUFACTURER} ${Build.MODEL}".trim()
        return "$device - Android ${Build.VERSION.RELEASE}".take(120)
    }

    private fun headerSafeDeviceId(): String =
        // ANDROID_ID is hex so the ASCII filter is a no-op on the normal path;
        // it only guards the rare MANUFACTURER-MODEL fallback (OkHttp throws on
        // non-ASCII header values). 64 chars matches the server-side column.
        deviceIdProvider.deviceId.filter { it.code in 32..126 }.take(64)

    private companion object {
        const val DEVICE_LABEL_HEADER = "X-Device-Label"
        const val DEVICE_ID_HEADER = "X-Device-Id"
    }
}
