package cz.cleansia.customer.core.auth

import android.content.Context
import cz.cleansia.customer.BuildConfig
import cz.cleansia.customer.core.data.AddressRepository
import cz.cleansia.customer.core.disputes.DisputeRepository
import cz.cleansia.customer.core.loyalty.LoyaltyRepository
import cz.cleansia.customer.core.orders.OrderRepository
import cz.cleansia.customer.core.referral.ReferralRepository
import cz.cleansia.customer.ui.snackbar.SnackbarController
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
    ): NetworkErrorInterceptor = NetworkErrorInterceptor(snackbarController)

    @Provides
    @Singleton
    @NoAuthOkHttp
    fun provideNoAuthOkHttpClient(
        logging: HttpLoggingInterceptor,
        networkErrorInterceptor: NetworkErrorInterceptor,
    ): OkHttpClient = OkHttpClient.Builder()
        .eventListener(SentryOkHttpEventListener())
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
        addressRepository: AddressRepository,
        orderRepository: OrderRepository,
        disputeRepository: DisputeRepository,
        loyaltyRepository: LoyaltyRepository,
        referralRepository: ReferralRepository,
        membershipRepository: cz.cleansia.customer.core.memberships.MembershipRepository,
        recurringBookingRepository: cz.cleansia.customer.core.recurring.RecurringBookingRepository,
        @ApplicationContext appContext: Context,
    ): AuthRepository = AuthRepository(
        api,
        tokenStore,
        sessionManager,
        addressRepository,
        orderRepository,
        disputeRepository,
        loyaltyRepository,
        referralRepository,
        membershipRepository,
        recurringBookingRepository,
        appContext,
    )

    @Provides
    @Singleton
    fun provideAuthAuthenticator(
        tokenStore: TokenStore,
        sessionManager: SessionManager,
        // Late-binding via Provider for AuthRepository and every session-scoped
        // cache repository to break the DI cycles:
        //   1. AuthRepository → AuthApi → NoAuth client … main client → Authenticator
        //   2. AddressRepository → SavedAddressApi → AuthRetrofit → AuthOkHttpClient → Authenticator
        //   3. OrderRepository → OrderApi → AuthRetrofit → AuthOkHttpClient → Authenticator
        //   4. DisputeRepository → DisputeApi → AuthRetrofit → AuthOkHttpClient → Authenticator
        // Hilt resolves each Provider lazily on .get() so the repos construct
        // after the OkHttpClient graph is fully built.
        repositoryProvider: javax.inject.Provider<AuthRepository>,
        addressRepositoryProvider: javax.inject.Provider<AddressRepository>,
        orderRepositoryProvider: javax.inject.Provider<OrderRepository>,
        disputeRepositoryProvider: javax.inject.Provider<DisputeRepository>,
        loyaltyRepositoryProvider: javax.inject.Provider<LoyaltyRepository>,
        referralRepositoryProvider: javax.inject.Provider<ReferralRepository>,
    ): AuthAuthenticator = AuthAuthenticator(
        tokenStore = tokenStore,
        sessionManager = sessionManager,
        addressRepositoryProvider = addressRepositoryProvider,
        orderRepositoryProvider = orderRepositoryProvider,
        disputeRepositoryProvider = disputeRepositoryProvider,
        loyaltyRepositoryProvider = loyaltyRepositoryProvider,
        referralRepositoryProvider = referralRepositoryProvider,
        refreshClient = { repositoryProvider.get() },
    )

    @Provides
    @Singleton
    fun provideAuthInterceptor(tokenStore: TokenStore): AuthInterceptor =
        AuthInterceptor(tokenStore)

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
    ): OkHttpClient = OkHttpClient.Builder()
        .eventListener(SentryOkHttpEventListener())
        .addInterceptor(authInterceptor)
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
