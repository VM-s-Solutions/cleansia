package cz.cleansia.partner

import android.app.Application
import android.os.Build
import coil3.ImageLoader
import coil3.PlatformContext
import coil3.SingletonImageLoader
import coil3.gif.AnimatedImageDecoder
import coil3.gif.GifDecoder
import coil3.map.Mapper
import coil3.request.Options
import coil3.request.crossfade
import com.mapbox.common.MapboxOptions
import cz.cleansia.core.sentry.SentryUserTracker
import cz.cleansia.partner.core.auth.TokenStoreEntryPoint
import cz.cleansia.partner.core.notifications.NotificationChannels
import dagger.hilt.android.EntryPointAccessors
import dagger.hilt.android.HiltAndroidApp
import io.sentry.android.core.SentryAndroid

/**
 * Partner application entry point. Hosts:
 *  - Hilt root.
 *  - Mapbox access-token install (must happen before any MapboxMap composes).
 *  - Coil image-loader configuration (animated WebP support for the
 *    InProgress mascot in [OrderTrackerHero]).
 *  - Sentry, when a DSN is configured.
 */
@HiltAndroidApp
class CleansiaPartnerApp : Application(), SingletonImageLoader.Factory {
    override fun onCreate() {
        super.onCreate()
        MapboxOptions.accessToken = BuildConfig.MAPBOX_ACCESS_TOKEN
        // Register FCM channels on every cold start (idempotent) so the cleaner
        // can mute categories at the OS level even before the first push lands.
        NotificationChannels.registerAll(this)

        // Guarded on the DSN: a blank one means Sentry stays dormant rather than initialising into
        // nothing, which is what lets a clone without SENTRY_DSN run normally.
        if (BuildConfig.SENTRY_DSN.isNotBlank()) {
            initSentry()
            val tokenStore = EntryPointAccessors
                .fromApplication(this, TokenStoreEntryPoint::class.java)
                .tokenStore()
            SentryUserTracker.start(tokenStore)
        }
    }

    /**
     * Verbatim the customer app's configuration, deliberately: two crash pipelines with different
     * sampling or PII settings would make the two apps' dashboards incomparable, and the reason each
     * value was chosen is the same on both.
     */
    private fun initSentry() {
        SentryAndroid.init(this) { options ->
            options.dsn = BuildConfig.SENTRY_DSN
            options.environment = if (BuildConfig.DEBUG) "debug" else "release"
            options.release = "${BuildConfig.APPLICATION_ID}@${BuildConfig.VERSION_NAME}"
            // Full sampling for errors — we want every crash. Performance traces sampled at 20% to
            // keep quota bounded while still surfacing slow flows.
            options.tracesSampleRate = 0.2
            // Don't send PII automatically — SentryUserTracker sets user context explicitly once
            // there is a session (id only, no email or IP).
            options.isSendDefaultPii = false
            options.isDebug = BuildConfig.DEBUG
        }
    }

    override fun newImageLoader(context: PlatformContext): ImageLoader =
        ImageLoader.Builder(context)
            .components {
                // Animated WebP / GIF / HEIF. Platform's AnimatedImageDecoder
                // is available on API 28+; we fall back to GifDecoder
                // (pure-Kotlin) on older devices so animation still plays.
                if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.P) {
                    add(AnimatedImageDecoder.Factory())
                } else {
                    add(GifDecoder.Factory())
                }
                // Dev-only host rewrite for Azurite-issued blob URLs.
                // Active only in debug builds, no-op in release.
                if (BuildConfig.DEBUG) {
                    add(AzuriteEmulatorHostMapper())
                }
            }
            .crossfade(true)
            .build()
}

/**
 * Rewrites image URLs that point at Azurite's default loopback host
 * (`127.0.0.1:10000`, `localhost:10000`) to the Android-emulator
 * loopback (`10.0.2.2:10000`).
 *
 * Why this exists: backend (running on the host machine) hands out
 * SAS URLs whose host is the host machine's loopback. On an Android
 * emulator `127.0.0.1` resolves to the emulator itself, not the host
 * — so blob fetches hit nothing and fail with ECONNREFUSED. The
 * emulator's host-loopback alias is `10.0.2.2`.
 *
 * Debug-only via [BuildConfig.DEBUG] — production never sees Azurite
 * URLs (real Azure SAS URLs use `*.blob.core.windows.net`).
 */
private class AzuriteEmulatorHostMapper : Mapper<String, String> {
    override fun map(data: String, options: Options): String? {
        // Cheap pre-check before paying for URL parsing
        if (!data.contains("127.0.0.1") && !data.contains("localhost")) return null
        return runCatching {
            val original = java.net.URI(data)
            val host = original.host ?: return null
            if (host != "127.0.0.1" && host != "localhost") return null
            // Preserve scheme, port, path, query — only the host changes.
            java.net.URI(
                original.scheme,
                original.userInfo,
                "10.0.2.2",
                original.port,
                original.path,
                original.query,
                original.fragment,
            ).toString()
        }.getOrNull()
    }
}
