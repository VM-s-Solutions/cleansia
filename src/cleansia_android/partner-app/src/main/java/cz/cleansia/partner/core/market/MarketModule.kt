package cz.cleansia.partner.core.market

import cz.cleansia.partner.api.client.MarketApi
import cz.cleansia.partner.core.network.NoAuthRetrofit
import dagger.Module
import dagger.Provides
import dagger.hilt.InstallIn
import dagger.hilt.components.SingletonComponent
import retrofit2.Retrofit
import javax.inject.Singleton

@Module
@InstallIn(SingletonComponent::class)
object MarketModule {

    @Provides
    @Singleton
    fun provideMarketApi(@NoAuthRetrofit retrofit: Retrofit): MarketApi =
        retrofit.create(MarketApi::class.java)
}
