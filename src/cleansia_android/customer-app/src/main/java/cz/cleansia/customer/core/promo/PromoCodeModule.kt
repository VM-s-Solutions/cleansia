package cz.cleansia.customer.core.promo

import cz.cleansia.customer.api.client.PromoCodeApi as GenPromoCodeApi
import cz.cleansia.customer.core.auth.AuthRetrofit
import dagger.Module
import dagger.Provides
import dagger.hilt.InstallIn
import dagger.hilt.components.SingletonComponent
import javax.inject.Singleton
import retrofit2.Retrofit

/**
 * Hilt provider for [PromoCodeApi]. Authenticated Retrofit client because the
 * validate endpoint is gated by `Permission(CanRedeemPromoCode)`.
 *
 * No repository binding — promo validation is request/response, not cached
 * state. The booking view-model injects the adapter directly.
 */
@Module
@InstallIn(SingletonComponent::class)
object PromoCodeModule {
    @Provides
    @Singleton
    fun provideGenPromoCodeApi(@AuthRetrofit retrofit: Retrofit): GenPromoCodeApi =
        retrofit.create(GenPromoCodeApi::class.java)

    @Provides
    @Singleton
    fun providePromoCodeApi(genPromoCodeApi: GenPromoCodeApi): PromoCodeApi =
        PromoCodeApi(genPromoCodeApi)
}
