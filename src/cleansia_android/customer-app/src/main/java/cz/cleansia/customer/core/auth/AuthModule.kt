package cz.cleansia.customer.core.auth

import android.content.Context
import cz.cleansia.core.auth.AuthAuthenticator
import cz.cleansia.core.auth.AuthInterceptor
import cz.cleansia.core.auth.NetworkErrorInterceptor
import cz.cleansia.core.auth.SessionManager
import cz.cleansia.core.auth.SessionScopedCache
import cz.cleansia.core.auth.TokenStore
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.customer.BuildConfig
import cz.cleansia.customer.R
import dagger.Module
import dagger.Provides
import dagger.hilt.InstallIn
import dagger.hilt.android.qualifiers.ApplicationContext
import dagger.hilt.components.SingletonComponent
import io.sentry.android.okhttp.SentryOkHttpEventListener
import kotlinx.serialization.json.Json
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.OkHttpClient
import okhttp3.logging.HttpLoggingInterceptor
import retrofit2.Retrofit
import retrofit2.converter.kotlinx.serialization.asConverterFactory
import java.util.concurrent.TimeUnit
import javax.inject.Qualifier
import javax.inject.Singleton

@Module
@InstallIn(SingletonComponent::class)
object AuthModule {

    /** Shared JSON config — lenient about unknown fields so backend additions don't break clients. */
    @Provides
    @Singleton
    fun provideJson(): Json = Json {
        ignoreUnknownKeys = true
        isLenient = true
        explicitNulls = false
        // Override the OpenAPI generator's string-serialized int enums.
        // See cz.cleansia.customer.core.network.IntEnumSerializers for the
        // full rationale + the list of affected enums (PaymentType,
        // OrderStatus, LoyaltyTier, etc.).
        serializersModule = cz.cleansia.customer.core.network.IntEnumSerializersModule
    }

    @Provides
    @Singleton
    fun provideTokenStore(@ApplicationContext context: Context): TokenStore = TokenStore(context)

    @Provides
    @Singleton
    fun provideSessionManager(): SessionManager = SessionManager()

    // ─── OkHttp clients ───
    // Two clients on purpose:
    //  1. [AuthOkHttp]     — main client, has AuthInterceptor + AuthAuthenticator.
    //     Every app API call goes through here. 401s trigger refresh automatically.
    //  2. [NoAuthOkHttp]   — bare client used by the refresh endpoint only.
    //     If we put the Authenticator on the client that hits RefreshToken itself,
    //     a 401 on refresh would recursively trigger another refresh → stack overflow.
    //     The AuthInterceptor already skips the Authorization header on anonymous
    //     endpoints, but using a separate client makes the boundary explicit.

    @Provides
    @Singleton
    fun provideLoggingInterceptor(): HttpLoggingInterceptor =
        HttpLoggingInterceptor().apply {
            level = if (BuildConfig.DEBUG) HttpLoggingInterceptor.Level.HEADERS
            else HttpLoggingInterceptor.Level.NONE
        }

    @Provides
    @Singleton
    fun provideNetworkErrorInterceptor(
        snackbarController: SnackbarController,
    ): NetworkErrorInterceptor = NetworkErrorInterceptor(
        snackbarController = snackbarController,
        networkErrorStringRes = R.string.error_generic_network,
        serverErrorStringRes = R.string.error_generic_server,
    )

    /**
     * Attaches the device's IANA timezone id to every request as
     * `X-Time-Zone` (e.g. "Europe/Prague"). Server-side handlers that
     * do day / week / month math (dashboard counts, revenue reports)
     * use this to compute boundaries in the user's wall clock instead
     * of in UTC. Mirrors the partner app's interceptor.
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
    fun provideNoAuthOkHttpClient(
        logging: HttpLoggingInterceptor,
        networkErrorInterceptor: NetworkErrorInterceptor,
        @TimeZoneInterceptorQ timeZoneInterceptor: okhttp3.Interceptor,
    ): OkHttpClient = OkHttpClient.Builder()
        .eventListener(SentryOkHttpEventListener())
        .addInterceptor(timeZoneInterceptor)
        .addInterceptor(networkErrorInterceptor)
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
        .baseUrl(BuildConfig.API_BASE_URL)
        .client(client)
        .addConverterFactory(json.asConverterFactory("application/json".toMediaType()))
        .build()

    /**
     * Dedicated AuthApi instance used ONLY by the Authenticator's refresh flow.
     * The main AuthRepository uses this same instance too — simpler than two
     * AuthApi bindings, and the refresh interceptor guard in [AuthInterceptor]
     * prevents auth tokens leaking into the refresh request.
     */
    @Provides
    @Singleton
    fun provideAuthApi(@NoAuthRetrofit retrofit: Retrofit): AuthApi =
        retrofit.create(AuthApi::class.java)

    @Provides
    @Singleton
    fun provideAuthRepository(
        api: AuthApi,
        tokenStore: TokenStore,
        sessionManager: SessionManager,
        sessionScopedCaches: Set<@JvmSuppressWildcards SessionScopedCache>,
        pushTokenRepository: cz.cleansia.customer.core.notifications.PushTokenRepository,
        @ApplicationContext appContext: Context,
    ): AuthRepository = AuthRepository(
        api,
        tokenStore,
        sessionManager,
        sessionScopedCaches,
        pushTokenRepository,
        appContext,
    )

    @Provides
    @Singleton
    fun provideAuthAuthenticator(
        tokenStore: TokenStore,
        sessionManager: SessionManager,
        // Late-binding via Provider to break the DI cycles:
        //   1. AuthRepository → AuthApi → NoAuth client … main client → Authenticator
        //   2. Each cache (Address/Order/Dispute/...) → AuthRetrofit → AuthOkHttpClient → Authenticator
        // Hilt resolves the Set lazily on .get() so the repos construct after
        // the OkHttpClient graph is fully built. The `Set<SessionScopedCache>`
        // multibinding is populated by [SessionScopedModule] — adding a new
        // cache there flows through to both this clear-path and
        // [AuthRepository.logout] without further edits.
        repositoryProvider: javax.inject.Provider<AuthRepository>,
        sessionScopedCachesProvider: javax.inject.Provider<Set<@JvmSuppressWildcards SessionScopedCache>>,
    ): AuthAuthenticator = AuthAuthenticator(
        tokenStore = tokenStore,
        sessionManager = sessionManager,
        sessionScopedCachesProvider = sessionScopedCachesProvider,
        refreshClient = { repositoryProvider.get() },
    )

    @Provides
    @Singleton
    fun provideAuthInterceptor(tokenStore: TokenStore): AuthInterceptor =
        AuthInterceptor(tokenStore)

    @Provides
    @Singleton
    fun provideGoogleSignInController(@ApplicationContext appContext: Context): GoogleSignInController =
        GoogleSignInController(appContext)

    /**
     * Main OkHttp client used by every non-auth endpoint (orders, services, etc.
     * wired in Phase 6). Carries the Authorization header via [AuthInterceptor]
     * and handles 401 refresh via [AuthAuthenticator].
     */
    @Provides
    @Singleton
    @AuthOkHttp
    fun provideAuthOkHttpClient(
        logging: HttpLoggingInterceptor,
        authInterceptor: AuthInterceptor,
        networkErrorInterceptor: NetworkErrorInterceptor,
        authenticator: AuthAuthenticator,
        @TimeZoneInterceptorQ timeZoneInterceptor: okhttp3.Interceptor,
    ): OkHttpClient = OkHttpClient.Builder()
        .eventListener(SentryOkHttpEventListener())
        .addInterceptor(authInterceptor)
        .addInterceptor(timeZoneInterceptor)
        .addInterceptor(networkErrorInterceptor)
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
        .baseUrl(BuildConfig.API_BASE_URL)
        .client(client)
        .addConverterFactory(json.asConverterFactory("application/json".toMediaType()))
        .build()
}

// ─── Qualifiers ───
// Two Retrofit + OkHttp pairs live in the graph; qualifiers disambiguate.

@Qualifier @Retention(AnnotationRetention.BINARY) annotation class AuthOkHttp
@Qualifier @Retention(AnnotationRetention.BINARY) annotation class NoAuthOkHttp
@Qualifier @Retention(AnnotationRetention.BINARY) annotation class AuthRetrofit
@Qualifier @Retention(AnnotationRetention.BINARY) annotation class NoAuthRetrofit
@Qualifier @Retention(AnnotationRetention.BINARY) annotation class TimeZoneInterceptorQ
