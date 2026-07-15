package cz.cleansia.partner.core.network

import android.content.Context
import cz.cleansia.core.auth.AuthAuthenticator
import cz.cleansia.core.auth.AuthInterceptor
import cz.cleansia.core.auth.DeviceIdProvider
import cz.cleansia.core.auth.JwtDecoder
import cz.cleansia.core.auth.RefreshClient
import cz.cleansia.core.auth.RefreshResult
import cz.cleansia.core.auth.SessionManager
import cz.cleansia.core.auth.SessionScopedCache
import cz.cleansia.core.auth.TokenStore
import cz.cleansia.core.network.RetryAfterInterceptor
import cz.cleansia.partner.BuildConfig
import cz.cleansia.partner.api.client.AuthApi
import cz.cleansia.partner.api.client.CountryApi
import cz.cleansia.partner.api.client.DashboardApi
import cz.cleansia.partner.api.client.EmployeeApi
import cz.cleansia.partner.api.client.EmployeePayrollApi
import cz.cleansia.partner.api.client.OrderApi
import cz.cleansia.partner.api.model.RefreshTokenCommand
import dagger.Module
import dagger.Provides
import dagger.hilt.InstallIn
import dagger.hilt.android.qualifiers.ApplicationContext
import dagger.hilt.components.SingletonComponent
import dagger.multibindings.Multibinds
import kotlinx.serialization.json.Json
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.OkHttpClient
import okhttp3.logging.HttpLoggingInterceptor
import retrofit2.Retrofit
import retrofit2.converter.kotlinx.serialization.asConverterFactory
import java.time.Instant
import java.util.concurrent.TimeUnit
import javax.inject.Provider
import javax.inject.Qualifier
import javax.inject.Singleton

/**
 * Network DI graph. Two OkHttp clients on purpose:
 *  - [AuthOkHttp]   — main client; carries [AuthInterceptor] + [AuthAuthenticator].
 *                     Every business call goes here.
 *  - [NoAuthOkHttp] — refresh endpoint only. If the Authenticator was on the
 *                     same client that hits RefreshToken, a 401 on refresh
 *                     would loop. Keeping it separate makes the boundary
 *                     explicit even though the interceptor already skips
 *                     anonymous paths.
 *
 * Reuses the canonical pieces from `:core`:
 *  - [TokenStore]              — EncryptedSharedPreferences-backed token persistence
 *  - [AuthInterceptor]         — attaches Bearer, skips anon paths, adds X-Device-Label
 *  - [AuthAuthenticator]       — single-flight 401-refresh-retry
 *  - [SessionManager]          — emits ForcedSignOut events the UI listens to
 *  - [SessionScopedCache]      — multibinding marker; any repo with per-user state
 *                                that needs flushing on sign-out joins this set
 */
@Module
@InstallIn(SingletonComponent::class)
abstract class NetworkBindingsModule {

    /**
     * Empty default for the [SessionScopedCache] multibinding. Caches join
     * the set via `@Binds @IntoSet` in their feature modules. Hilt requires
     * a declared set even if no contributors exist yet.
     */
    @Multibinds
    abstract fun sessionScopedCaches(): Set<SessionScopedCache>
}

@Module
@InstallIn(SingletonComponent::class)
object NetworkModule {

    @Provides
    @Singleton
    fun provideJson(): Json = Json {
        ignoreUnknownKeys = true
        isLenient = true
        explicitNulls = false
    }

    @Provides
    @Singleton
    fun provideTokenStore(@ApplicationContext context: Context): TokenStore = TokenStore(context)

    @Provides
    @Singleton
    fun provideSessionManager(): SessionManager = SessionManager()

    @Provides
    @Singleton
    fun provideDeviceIdProvider(@ApplicationContext context: Context): DeviceIdProvider =
        DeviceIdProvider(context)

    @Provides
    @Singleton
    fun provideAuthInterceptor(
        tokenStore: TokenStore,
        deviceIdProvider: DeviceIdProvider,
    ): AuthInterceptor = AuthInterceptor(tokenStore, deviceIdProvider)

    @Provides
    @Singleton
    fun provideAuthAuthenticator(
        tokenStore: TokenStore,
        sessionManager: SessionManager,
        sessionScopedCaches: Provider<Set<@JvmSuppressWildcards SessionScopedCache>>,
        refreshClient: Provider<RefreshClient>,
    ): AuthAuthenticator = AuthAuthenticator(
        tokenStore = tokenStore,
        sessionManager = sessionManager,
        sessionScopedCachesProvider = sessionScopedCaches,
        refreshClient = { refreshClient.get() },
    )

    @Provides
    @Singleton
    fun provideLoggingInterceptor(): HttpLoggingInterceptor = HttpLoggingInterceptor().apply {
        level = if (BuildConfig.DEBUG) HttpLoggingInterceptor.Level.HEADERS
        else HttpLoggingInterceptor.Level.NONE
        redactHeader("Authorization")
    }

    /**
     * Attaches the device's IANA timezone id to every request as
     * `X-Time-Zone` (e.g. "Europe/Prague"). Server-side handlers that
     * do day / week / month math (dashboard counts, revenue reports)
     * use this to compute boundaries in the user's wall clock instead
     * of in UTC — without it, a cleaner who finishes a job at 00:30
     * local sees it under "yesterday's" stats. Read once per request
     * via TimeZone.getDefault() so a system-zone change is picked up
     * on the next call without restarting the app.
     */
    @Provides
    @Singleton
    @TimeZoneInterceptorQ
    fun provideTimeZoneHeaderInterceptor(): okhttp3.Interceptor = okhttp3.Interceptor { chain ->
        val request = chain.request().newBuilder()
            .header("X-Time-Zone", java.util.TimeZone.getDefault().id)
            .build()
        chain.proceed(request)
    }

    @Provides
    @Singleton
    @NoAuthOkHttp
    fun provideNoAuthOkHttp(
        logging: HttpLoggingInterceptor,
        @TimeZoneInterceptorQ timeZoneInterceptor: okhttp3.Interceptor,
    ): OkHttpClient =
        OkHttpClient.Builder()
            .addInterceptor(timeZoneInterceptor)
            .addInterceptor(logging)
            .connectTimeout(15, TimeUnit.SECONDS)
            .readTimeout(30, TimeUnit.SECONDS)
            .build()

    @Provides
    @Singleton
    @NoAuthRetrofit
    fun provideNoAuthRetrofit(
        @NoAuthOkHttp client: OkHttpClient,
        json: Json,
    ): Retrofit = Retrofit.Builder()
        .baseUrl(BuildConfig.API_BASE_URL.ensureTrailingSlash())
        .client(client)
        .addConverterFactory(json.asConverterFactory("application/json".toMediaType()))
        .build()

    @Provides
    @Singleton
    fun provideAuthApi(@NoAuthRetrofit retrofit: Retrofit): AuthApi =
        retrofit.create(AuthApi::class.java)

    @Provides
    @Singleton
    fun provideRefreshClient(authApi: AuthApi): RefreshClient = object : RefreshClient {
        override suspend fun refresh(refreshToken: String): RefreshResult {
            // The partner-mobile-api enriches `RequiredAudience = "Mobile"` server-side
            // (see Cleansia.Web.Mobile.Partner/Controllers/AuthController.cs:106), so we
            // only need to send the refresh token itself. `RequiredProfile = null`
            // accepts any profile — the user's profile is whatever the original token
            // was issued for, the server checks it against the refresh-token record.
            val response = runCatching {
                authApi.authRefreshToken(RefreshTokenCommand(token = refreshToken))
            }.getOrNull() ?: return RefreshResult.Unavailable

            if (!response.isSuccessful) {
                val errorBody = runCatching { response.errorBody()?.string() }.getOrNull()
                return RefreshResult.classifyHttpFailure(response.code(), errorBody)
            }

            val body = response.body() ?: return RefreshResult.Unavailable
            val access = body.token
            if (access.isNullOrBlank()) return RefreshResult.Unavailable

            val accessExp = JwtDecoder.extractExpiryMillis(access)
                ?: (System.currentTimeMillis() + 15 * 60 * 1000L)
            val refresh = body.refreshToken ?: return RefreshResult.Unavailable
            val refreshExp = body.refreshTokenExpiresAt
                ?.let { runCatching { Instant.parse(it).toEpochMilli() }.getOrNull() }
                ?: (System.currentTimeMillis() + 24 * 60 * 60 * 1000L)

            return RefreshResult.Success(
                TokenStore.Tokens(
                    accessToken = access,
                    accessTokenExpiresAt = accessExp,
                    refreshToken = refresh,
                    refreshTokenExpiresAt = refreshExp,
                ),
            )
        }
    }

    @Provides
    @Singleton
    fun provideRetryAfterInterceptor(): RetryAfterInterceptor = RetryAfterInterceptor()

    @Provides
    @Singleton
    @AuthOkHttp
    fun provideAuthOkHttp(
        logging: HttpLoggingInterceptor,
        authInterceptor: AuthInterceptor,
        authenticator: AuthAuthenticator,
        retryAfterInterceptor: RetryAfterInterceptor,
        @TimeZoneInterceptorQ timeZoneInterceptor: okhttp3.Interceptor,
    ): OkHttpClient = OkHttpClient.Builder()
        // Outermost on purpose — the 429 back-off retry re-enters auth/timezone/
        // logging so the retried request carries a fresh token. NoAuth (refresh)
        // client stays without it.
        .addInterceptor(retryAfterInterceptor)
        .addInterceptor(authInterceptor)
        .addInterceptor(timeZoneInterceptor)
        .addInterceptor(logging)
        .authenticator(authenticator)
        .connectTimeout(15, TimeUnit.SECONDS)
        .readTimeout(30, TimeUnit.SECONDS)
        .build()

    @Provides
    @Singleton
    @AuthRetrofit
    fun provideAuthRetrofit(
        @AuthOkHttp client: OkHttpClient,
        json: Json,
    ): Retrofit = Retrofit.Builder()
        .baseUrl(BuildConfig.API_BASE_URL.ensureTrailingSlash())
        .client(client)
        .addConverterFactory(json.asConverterFactory("application/json".toMediaType()))
        .build()

    @Provides @Singleton fun provideEmployeeApi(@AuthRetrofit r: Retrofit): EmployeeApi = r.create(EmployeeApi::class.java)
    @Provides @Singleton fun provideOrderApi(@AuthRetrofit r: Retrofit): OrderApi = r.create(OrderApi::class.java)
    @Provides @Singleton fun provideDashboardApi(@AuthRetrofit r: Retrofit): DashboardApi = r.create(DashboardApi::class.java)
    @Provides @Singleton fun provideEmployeePayrollApi(@AuthRetrofit r: Retrofit): EmployeePayrollApi = r.create(EmployeePayrollApi::class.java)
    @Provides @Singleton fun provideCountryApi(@AuthRetrofit r: Retrofit): CountryApi = r.create(CountryApi::class.java)
    @Provides @Singleton fun provideServiceCityApi(@AuthRetrofit r: Retrofit): cz.cleansia.partner.api.client.ServiceCityApi =
        r.create(cz.cleansia.partner.api.client.ServiceCityApi::class.java)

    private fun String.ensureTrailingSlash(): String = if (endsWith("/")) this else "$this/"
}

@Qualifier @Retention(AnnotationRetention.BINARY) annotation class AuthOkHttp
@Qualifier @Retention(AnnotationRetention.BINARY) annotation class NoAuthOkHttp
@Qualifier @Retention(AnnotationRetention.BINARY) annotation class AuthRetrofit
@Qualifier @Retention(AnnotationRetention.BINARY) annotation class NoAuthRetrofit
@Qualifier @Retention(AnnotationRetention.BINARY) annotation class TimeZoneInterceptorQ
