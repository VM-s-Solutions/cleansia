package cz.cleansia.customer.core.recurring

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
    fun provideRecurringBookingApi(@AuthRetrofit retrofit: Retrofit): RecurringBookingApi =
        retrofit.create(RecurringBookingApi::class.java)
}
