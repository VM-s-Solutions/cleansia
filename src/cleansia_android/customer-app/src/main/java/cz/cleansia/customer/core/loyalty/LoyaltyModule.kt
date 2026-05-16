package cz.cleansia.customer.core.loyalty

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

    @Provides
    @Singleton
    fun provideLoyaltyApi(genLoyaltyApi: GenLoyaltyApi): LoyaltyApi = LoyaltyApi(genLoyaltyApi)
}
