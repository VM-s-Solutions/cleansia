package cz.cleansia.customer.core.recurring

import cz.cleansia.customer.api.client.RecurringBookingApi as GenRecurringBookingApi
import cz.cleansia.customer.core.auth.AuthRetrofit
import dagger.Module
import dagger.Provides
import dagger.hilt.InstallIn
import dagger.hilt.components.SingletonComponent
import javax.inject.Singleton
import retrofit2.Retrofit

@Module
@InstallIn(SingletonComponent::class)
object RecurringBookingModule {
    @Provides
    @Singleton
    fun provideGenRecurringBookingApi(@AuthRetrofit retrofit: Retrofit): GenRecurringBookingApi =
        retrofit.create(GenRecurringBookingApi::class.java)

    @Provides
    @Singleton
    fun provideRecurringBookingApi(
        genRecurringBookingApi: GenRecurringBookingApi,
    ): RecurringBookingApi = RecurringBookingApi(genRecurringBookingApi)
}
