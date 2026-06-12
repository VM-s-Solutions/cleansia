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
 *  2. [AuthInterceptor]'s `X-Device-Id` header — stamped onto refresh tokens
 *     at issue/rotation time so revoking a device also kills its session.
 *
 * The revoke match is `RefreshToken.DeviceId == Device.DeviceId`, so a second
 * id source would silently break the session kill — never resolve the id
 * anywhere else.
 *
 * ANDROID_ID resets on factory reset and is per-app-signing-key on Android 8+,
 * which is the right granularity: a factory reset SHOULD invalidate the
 * registration, and the per-app scoping keeps the partner and customer apps'
 * device rows from colliding on the same handset.
 */
class DeviceIdProvider(context: Context) {

    private val appContext = context.applicationContext

    val deviceId: String by lazy {
        val androidId = Settings.Secure.getString(appContext.contentResolver, Settings.Secure.ANDROID_ID)
        if (!androidId.isNullOrEmpty()) androidId else "${Build.MANUFACTURER}-${Build.MODEL}"
    }
}
