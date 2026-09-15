package cz.cleansia.customer.core.market

import cz.cleansia.customer.api.client.MarketApi as GenMarketApi
import cz.cleansia.customer.core.auth.NoAuthRetrofit
import dagger.Module
import dagger.Provides
import dagger.hilt.InstallIn
import dagger.hilt.components.SingletonComponent
import javax.inject.Singleton
import retrofit2.Retrofit

@Module
@InstallIn(SingletonComponent::class)
object MarketModule {
    @Provides
    @Singleton
    fun provideGenMarketApi(@NoAuthRetrofit retrofit: Retrofit): GenMarketApi =
        retrofit.create(GenMarketApi::class.java)

    @Provides
    @Singleton
    fun provideMarketApi(genMarketApi: GenMarketApi): MarketApi = MarketApi(genMarketApi)
}
