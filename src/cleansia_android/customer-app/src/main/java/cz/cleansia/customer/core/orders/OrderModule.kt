package cz.cleansia.customer.core.orders

import cz.cleansia.customer.api.client.OrderApi as GenOrderApi
import cz.cleansia.customer.core.auth.AuthRetrofit
import dagger.Module
import dagger.Provides
import dagger.hilt.InstallIn
import dagger.hilt.components.SingletonComponent
import javax.inject.Singleton
import retrofit2.Retrofit

@Module
@InstallIn(SingletonComponent::class)
object OrderModule {
    // Generated OrderApi — single source for both this module's OrderApi
    // adapter and the BookingApi adapter (which re-projects Quote / CreateOrder
    // off the same controller).
    @Provides
    @Singleton
    fun provideGenOrderApi(@AuthRetrofit retrofit: Retrofit): GenOrderApi =
        retrofit.create(GenOrderApi::class.java)

    @Provides
    @Singleton
    fun provideOrderApi(orderApi: GenOrderApi): OrderApi = OrderApi(orderApi)
}
