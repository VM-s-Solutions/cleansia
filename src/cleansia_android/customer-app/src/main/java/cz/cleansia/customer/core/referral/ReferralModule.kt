package cz.cleansia.customer.core.referral
import cz.cleansia.core.auth.AuthInterceptor
import cz.cleansia.core.auth.TokenStore

import cz.cleansia.customer.api.client.ReferralApi as GenReferralApi
import cz.cleansia.customer.core.auth.AuthRetrofit
import dagger.Module
import dagger.Provides
import dagger.hilt.InstallIn
import dagger.hilt.components.SingletonComponent
import javax.inject.Singleton
import retrofit2.Retrofit

/**
 * Hilt provider for [ReferralApi]. Uses `@AuthRetrofit` for all three methods.
 * The two GET endpoints require a Bearer token (gated by
 * `Permission(CanViewMyReferral)` on the backend); the POST Validate endpoint
 * is `[AllowAnonymous]` and is callable from the signup form before a token
 * exists. The shared [AuthInterceptor] simply skips the Authorization header
 * when [TokenStore.current] is null, so the unauthenticated call goes through
 * fine on the same client.
 */
@Module
@InstallIn(SingletonComponent::class)
object ReferralModule {
    @Provides
    @Singleton
    fun provideGenReferralApi(@AuthRetrofit retrofit: Retrofit): GenReferralApi =
        retrofit.create(GenReferralApi::class.java)

    @Provides
    @Singleton
    fun provideReferralApi(genReferralApi: GenReferralApi): ReferralApi =
        ReferralApi(genReferralApi)
}
