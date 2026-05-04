package cz.cleansia.customer.core.orders

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
    @Provides
    @Singleton
    fun provideOrderApi(@AuthRetrofit retrofit: Retrofit): OrderApi =
        retrofit.create(OrderApi::class.java)
}
