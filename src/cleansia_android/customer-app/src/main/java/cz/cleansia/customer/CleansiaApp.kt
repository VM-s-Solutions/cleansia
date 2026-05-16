package cz.cleansia.customer

import android.app.Application
import android.os.Build
import coil3.ImageLoader
import coil3.PlatformContext
import coil3.SingletonImageLoader
import coil3.gif.AnimatedImageDecoder
import coil3.gif.GifDecoder
import com.mapbox.common.MapboxOptions
import com.stripe.android.PaymentConfiguration
import cz.cleansia.core.sentry.SentryUserTracker
import cz.cleansia.customer.core.auth.TokenStoreEntryPoint
import cz.cleansia.customer.core.notifications.NotificationChannels
import dagger.hilt.android.EntryPointAccessors
import dagger.hilt.android.HiltAndroidApp
import io.sentry.android.core.SentryAndroid

@HiltAndroidApp
class CleansiaApp : Application(), SingletonImageLoader.Factory {

    /**
     * Custom Coil [ImageLoader] that registers the animated-image decoder so
     * looping mascot WebPs (and any future animated WebP / GIF assets) actually
     * animate. Without this, Coil decodes only the first frame.
     *
     * Uses [AnimatedImageDecoder] on API 28+ (faster, hardware-accelerated via
     * the platform `ImageDecoder`) and falls back to the libwebp-backed
     * [GifDecoder] on older devices.
     */
    override fun newImageLoader(context: PlatformContext): ImageLoader =
        ImageLoader.Builder(context)
            .components {
                if (Build.VERSION.SDK_INT >= 28) {
                    add(AnimatedImageDecoder.Factory())
                } else {
                    add(GifDecoder.Factory())
                }
            }
            .build()

    override fun onCreate() {
        super.onCreate()
        // Register the per-category Android notification channels up-front
        // so users can pre-mute any future-phase categories from system
        // settings even before we ship the events that drive them.
        // Idempotent — Android dedupes by channelId.
        NotificationChannels.registerAll(this)

        // Mapbox requires the access token set once before any map is instantiated.
        // Token lives in ~/.gradle/gradle.properties → BuildConfig; see build.gradle.kts.
        MapboxOptions.accessToken = BuildConfig.MAPBOX_ACCESS_TOKEN

        // Stripe PaymentSheet requires the publishable key set once at app start.
        // Empty string fallback means "no card payments configured" — PaymentSheet
        // will throw a clear error at first use rather than crashing on launch.
        if (BuildConfig.STRIPE_PUBLISHABLE_KEY.isNotBlank()) {
            PaymentConfiguration.init(this, BuildConfig.STRIPE_PUBLISHABLE_KEY)
        }

        if (BuildConfig.SENTRY_DSN.isNotBlank()) {
            initSentry()
            val tokenStore = EntryPointAccessors
                .fromApplication(this, TokenStoreEntryPoint::class.java)
                .tokenStore()
            SentryUserTracker.start(tokenStore)
        }
    }

    private fun initSentry() {
        SentryAndroid.init(this) { options ->
            options.dsn = BuildConfig.SENTRY_DSN
            options.environment = if (BuildConfig.DEBUG) "debug" else "release"
            options.release = "${BuildConfig.APPLICATION_ID}@${BuildConfig.VERSION_NAME}"
            // Full sampling for errors — we want every crash. Performance traces
            // sampled at 20% to keep quota bounded while still surfacing slow flows.
            options.tracesSampleRate = 0.2
            // Don't send PII automatically — we set user context explicitly via
            // SentryUserTracker once we have a session (ID only, no email/IP).
            options.isSendDefaultPii = false
            // Debug builds log Sentry internals to logcat so we can confirm events
            // are being sent without waiting for the dashboard.
            options.isDebug = BuildConfig.DEBUG
        }
    }
}
