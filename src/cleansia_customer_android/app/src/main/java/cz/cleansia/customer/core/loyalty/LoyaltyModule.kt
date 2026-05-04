package cz.cleansia.customer.core.loyalty

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
    fun provideLoyaltyApi(@AuthRetrofit retrofit: Retrofit): LoyaltyApi =
        retrofit.create(LoyaltyApi::class.java)
}
