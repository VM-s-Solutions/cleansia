package cz.cleansia.customer.core.promo

import cz.cleansia.customer.core.auth.AuthRetrofit
import dagger.Module
import dagger.Provides
import dagger.hilt.InstallIn
import dagger.hilt.components.SingletonComponent
import javax.inject.Singleton
import retrofit2.Retrofit

/**
 * Hilt provider for [PromoCodeApi]. Mirrors [cz.cleansia.customer.core.loyalty.LoyaltyModule] —
 * authenticated Retrofit client because the validate endpoint is gated by
 * `Permission(CanRedeemPromoCode)`.
 *
 * No repository binding — promo validation is request/response, not cached
 * state. [BookingViewModel] injects the API directly.
 */
@Module
@InstallIn(SingletonComponent::class)
object PromoCodeModule {
    @Provides
    @Singleton
    fun providePromoCodeApi(@AuthRetrofit retrofit: Retrofit): PromoCodeApi =
        retrofit.create(PromoCodeApi::class.java)
}
