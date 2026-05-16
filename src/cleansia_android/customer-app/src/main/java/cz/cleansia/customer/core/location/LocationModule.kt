package cz.cleansia.customer.core.location

import android.content.Context
import cz.cleansia.customer.BuildConfig
import dagger.Module
import dagger.Provides
import dagger.hilt.InstallIn
import dagger.hilt.android.qualifiers.ApplicationContext
import dagger.hilt.components.SingletonComponent
import okhttp3.OkHttpClient
import java.util.concurrent.TimeUnit
import javax.inject.Singleton

@Module
@InstallIn(SingletonComponent::class)
object LocationModule {
    @Provides
    @Singleton
    fun provideLocationService(
        @ApplicationContext context: Context,
    ): LocationService = LocationService(context)

    /**
     * OkHttp used for one-off geocoding lookups. 10s total budget — geocoding should
     * be fast, and a hung call stalls the address picker UX.
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

@javax.inject.Qualifier
@Retention(AnnotationRetention.BINARY)
annotation class GeocodingHttp
