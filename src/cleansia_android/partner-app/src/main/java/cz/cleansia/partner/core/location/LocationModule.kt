package cz.cleansia.partner.core.location

import android.content.Context
import cz.cleansia.core.location.LocationService
import cz.cleansia.core.location.ReverseGeocodingService
import cz.cleansia.core.location.UserLocation
import cz.cleansia.partner.BuildConfig
import dagger.Module
import dagger.Provides
import dagger.hilt.InstallIn
import dagger.hilt.android.qualifiers.ApplicationContext
import dagger.hilt.components.SingletonComponent
import okhttp3.OkHttpClient
import java.util.concurrent.TimeUnit
import javax.inject.Qualifier
import javax.inject.Singleton

/**
 * Partner-app side of the location stack. The classes live in `:core`;
 * this module supplies the app-specific bindings — namely the Mapbox
 * token (sourced from partner-app's BuildConfig), a dedicated OkHttp
 * client for geocoding lookups, and the debug-only Prague-center
 * fallback location used to unblock the address picker on broken
 * emulator images.
 */
@Module
@InstallIn(SingletonComponent::class)
object LocationModule {
    @Provides
    @Singleton
    fun provideLocationService(
        @ApplicationContext context: Context,
    ): LocationService = LocationService(
        context = context,
        // Some emulator images never deliver a real fix even with mock
        // location configured; without a debug stub the Orders-distance
        // badge + the new address picker would be untestable on those
        // AVDs. BuildConfig.DEBUG ensures release APKs never see this.
        debugFallbackLocation = if (BuildConfig.DEBUG) UserLocation(50.0755, 14.4378) else null,
    )

    /**
     * OkHttp used for one-off geocoding lookups. 5s connect + 5s read —
     * geocoding should be fast, and a hung call stalls the address
     * picker UX.
     */
    @Provides
    @Singleton
    @GeocodingHttp
    fun provideGeocodingHttpClient(): OkHttpClient = OkHttpClient.Builder()
        .connectTimeout(5, TimeUnit.SECONDS)
        .readTimeout(5, TimeUnit.SECONDS)
        .build()

    @Provides
    @Singleton
    fun provideReverseGeocodingService(
        @GeocodingHttp httpClient: OkHttpClient,
    ): ReverseGeocodingService = ReverseGeocodingService(
        httpClient = httpClient,
        accessToken = BuildConfig.MAPBOX_ACCESS_TOKEN,
    )
}

@Qualifier
@Retention(AnnotationRetention.BINARY)
annotation class GeocodingHttp
