package cz.cleansia.customer.core.payments

import cz.cleansia.customer.api.client.PaymentApi as GenPaymentApi
import cz.cleansia.customer.core.auth.AuthRetrofit
import dagger.Module
import dagger.Provides
import dagger.hilt.InstallIn
import dagger.hilt.components.SingletonComponent
import javax.inject.Singleton
import retrofit2.Retrofit

@Module
@InstallIn(SingletonComponent::class)
object PaymentModule {
    @Provides
    @Singleton
    fun provideGenPaymentApi(@AuthRetrofit retrofit: Retrofit): GenPaymentApi =
        retrofit.create(GenPaymentApi::class.java)

    @Provides
    @Singleton
    fun providePaymentApi(genPaymentApi: GenPaymentApi): PaymentApi = PaymentApi(genPaymentApi)
}
