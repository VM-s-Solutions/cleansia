package cz.cleansia.core.auth

import android.content.Context
import android.os.Build
import android.provider.Settings

/**
 * THE single source of the stable per-install device id. Two consumers must
 * agree on the exact same string:
 *
 *  1. Push registration — the id sent as `deviceId` in /api/Device/Register,
 *     stored server-side on the Device row.
 *  2. [DeviceHeadersInterceptor]'s `X-Device-Id` header — stamped onto refresh
 *     tokens at issue/rotation time so revoking a device also kills its session.
 *     That interceptor is installed on BOTH OkHttp clients precisely because
 *     issue time means Login/Register/RefreshToken, which are anonymous.
 *
 * The revoke match is `RefreshToken.DeviceId == Device.DeviceId`, so a second
 * id source would silently break the session kill — never resolve the id
 * anywhere else.
 *
 * ANDROID_ID can change on factory reset and is scoped to the signing key,
 * Android user and device on Android 8+. The two apps can share it when signed
 * with the same key; S8 also pins shared-handset retirement to the non-empty
 * push token. -> /architecture/security-rules
 */
class DeviceIdProvider(context: Context) {

    private val appContext = context.applicationContext

    val deviceId: String by lazy {
        val androidId = Settings.Secure.getString(appContext.contentResolver, Settings.Secure.ANDROID_ID)
        if (!androidId.isNullOrEmpty()) androidId else "${Build.MANUFACTURER}-${Build.MODEL}"
    }
}
