package cz.cleansia.partner.di

import cz.cleansia.partner.api.client.AuthApi
import cz.cleansia.partner.api.client.CountryApi
import cz.cleansia.partner.api.client.DashboardApi
import cz.cleansia.partner.api.client.DeviceApi
import cz.cleansia.partner.api.client.EmployeeApi
import cz.cleansia.partner.api.client.EmployeePayrollApi
import cz.cleansia.partner.api.client.GdprApi
import cz.cleansia.partner.api.client.LanguageApi
import cz.cleansia.partner.api.client.OrderApi
import cz.cleansia.partner.config.AppConfig
import cz.cleansia.partner.config.Constants
import cz.cleansia.partner.core.network.ApiService
import cz.cleansia.partner.core.network.AuthInterceptor
import dagger.Module
import dagger.Provides
import dagger.hilt.InstallIn
import dagger.hilt.components.SingletonComponent
import javax.inject.Qualifier
import kotlinx.serialization.json.Json
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.OkHttpClient
import okhttp3.logging.HttpLoggingInterceptor
import retrofit2.Retrofit
import retrofit2.converter.kotlinx.serialization.asConverterFactory
import java.util.concurrent.TimeUnit
import javax.inject.Singleton

/**
 * Qualifier for the Retrofit instance the OpenAPI-generated client uses. The
 * generated `@POST("api/Auth/Login")` etc. paths already include the `api/`
 * prefix, so this Retrofit's base URL is the HOST root (no `/api`). The
 * legacy hand-written [ApiService] keeps using the un-qualified Retrofit
 * whose base URL still has `/api` (so its `@POST("Auth/Login")` paths
 * resolve correctly). Both share the same OkHttp client + interceptors.
 */
@Qualifier
@Retention(AnnotationRetention.BINARY)
annotation class GeneratedClientRetrofit

@Module
@InstallIn(SingletonComponent::class)
object NetworkModule {

    @Provides
    @Singleton
    fun provideJson(): Json = Json {
        ignoreUnknownKeys = true
        isLenient = true
        encodeDefaults = true
        prettyPrint = AppConfig.isDebug
        coerceInputValues = true
    }

    @Provides
    @Singleton
    fun provideLoggingInterceptor(): HttpLoggingInterceptor {
        return HttpLoggingInterceptor().apply {
            level = if (AppConfig.isDebug) {
                HttpLoggingInterceptor.Level.BODY
            } else {
                HttpLoggingInterceptor.Level.NONE
            }
        }
    }

    @Provides
    @Singleton
    fun provideOkHttpClient(
        authInterceptor: AuthInterceptor,
        loggingInterceptor: HttpLoggingInterceptor
    ): OkHttpClient {
        return OkHttpClient.Builder()
            .addInterceptor(authInterceptor)
            .addInterceptor(loggingInterceptor)
            .connectTimeout(Constants.Network.CONNECT_TIMEOUT_SECONDS, TimeUnit.SECONDS)
            .readTimeout(Constants.Network.READ_TIMEOUT_SECONDS, TimeUnit.SECONDS)
            .writeTimeout(Constants.Network.WRITE_TIMEOUT_SECONDS, TimeUnit.SECONDS)
            .build()
    }

    @Provides
    @Singleton
    fun provideRetrofit(
        okHttpClient: OkHttpClient,
        json: Json
    ): Retrofit {
        val contentType = "application/json".toMediaType()
        return Retrofit.Builder()
            .baseUrl(AppConfig.apiBaseUrl + "/")
            .client(okHttpClient)
            .addConverterFactory(json.asConverterFactory(contentType))
            .build()
    }

    @Provides
    @Singleton
    fun provideApiService(retrofit: Retrofit): ApiService {
        return retrofit.create(ApiService::class.java)
    }

    // ─── OpenAPI generated client wiring ─────────────────────────────
    // The generated `*Api` interfaces carry their `api/` path prefix
    // inline, so they need a Retrofit whose base URL is just the host
    // root. Migration target: as repos move over, the legacy Retrofit +
    // ApiService above will be deleted.

    @Provides
    @Singleton
    @GeneratedClientRetrofit
    fun provideGeneratedClientRetrofit(
        okHttpClient: OkHttpClient,
        json: Json,
    ): Retrofit {
        val contentType = "application/json".toMediaType()
        // Strip the trailing /api from apiBaseUrl — generated endpoints
        // include it in their path. Falls back to appending '/' if the
        // base URL ever stops being /api-suffixed (paranoid).
        val hostRoot = AppConfig.apiBaseUrl
            .removeSuffix("/")
            .removeSuffix("/api") + "/"
        return Retrofit.Builder()
            .baseUrl(hostRoot)
            .client(okHttpClient)
            .addConverterFactory(json.asConverterFactory(contentType))
            .build()
    }

    @Provides @Singleton fun provideAuthApi(@GeneratedClientRetrofit r: Retrofit): AuthApi = r.create(AuthApi::class.java)
    @Provides @Singleton fun provideCountryApi(@GeneratedClientRetrofit r: Retrofit): CountryApi = r.create(CountryApi::class.java)
    @Provides @Singleton fun provideDashboardApi(@GeneratedClientRetrofit r: Retrofit): DashboardApi = r.create(DashboardApi::class.java)
    @Provides @Singleton fun provideDeviceApi(@GeneratedClientRetrofit r: Retrofit): DeviceApi = r.create(DeviceApi::class.java)
    @Provides @Singleton fun provideEmployeeApi(@GeneratedClientRetrofit r: Retrofit): EmployeeApi = r.create(EmployeeApi::class.java)
    @Provides @Singleton fun provideEmployeePayrollApi(@GeneratedClientRetrofit r: Retrofit): EmployeePayrollApi = r.create(EmployeePayrollApi::class.java)
    @Provides @Singleton fun provideGdprApi(@GeneratedClientRetrofit r: Retrofit): GdprApi = r.create(GdprApi::class.java)
    @Provides @Singleton fun provideLanguageApi(@GeneratedClientRetrofit r: Retrofit): LanguageApi = r.create(LanguageApi::class.java)
    @Provides @Singleton fun provideOrderApi(@GeneratedClientRetrofit r: Retrofit): OrderApi = r.create(OrderApi::class.java)
}
