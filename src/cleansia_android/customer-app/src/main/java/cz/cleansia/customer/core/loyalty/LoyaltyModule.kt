package cz.cleansia.customer.core.loyalty

import cz.cleansia.customer.api.client.CreditApi as GenCreditApi
import cz.cleansia.customer.api.client.LoyaltyApi as GenLoyaltyApi
import cz.cleansia.customer.core.auth.AuthRetrofit
import dagger.Module
import dagger.Provides
import dagger.hilt.InstallIn
import dagger.hilt.components.SingletonComponent
import javax.inject.Singleton
import retrofit2.Retrofit

@Module
@InstallIn(SingletonComponent::class)
object LoyaltyModule {
    @Provides
    @Singleton
    fun provideGenLoyaltyApi(@AuthRetrofit retrofit: Retrofit): GenLoyaltyApi =
        retrofit.create(GenLoyaltyApi::class.java)

    /** Credit is read with the loyalty state — one session cache serves Rewards, Profile and Home. */
    @Provides
    @Singleton
    fun provideGenCreditApi(@AuthRetrofit retrofit: Retrofit): GenCreditApi =
        retrofit.create(GenCreditApi::class.java)

    @Provides
    @Singleton
    fun provideLoyaltyApi(genLoyaltyApi: GenLoyaltyApi, genCreditApi: GenCreditApi): LoyaltyApi =
        LoyaltyApi(genLoyaltyApi, genCreditApi)
}
