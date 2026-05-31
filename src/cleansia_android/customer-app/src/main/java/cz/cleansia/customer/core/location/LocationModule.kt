package cz.cleansia.customer.core.location

import android.content.Context
import cz.cleansia.core.location.LocationService
import cz.cleansia.core.location.ReverseGeocodingService
import cz.cleansia.customer.BuildConfig
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
 * Customer-app side of the location stack. The actual classes
 * (LocationService, ReverseGeocodingService) live in `:core`; this
 * module supplies their app-specific bindings — namely the Mapbox token
 * (sourced from customer-app's BuildConfig) and the dedicated OkHttp
 * client used for geocoding lookups.
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
        // Customer app doesn't need a debug fallback — its address
        // picker works around a null fix by asking the user to search
        // for an address by name. No need to lie about coordinates.
        debugFallbackLocation = null,
    )

    /**
     * OkHttp used for one-off geocoding lookups. 5s connect + 5s read —
     * geocoding should be fast, and a hung call stalls the address picker UX.
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
