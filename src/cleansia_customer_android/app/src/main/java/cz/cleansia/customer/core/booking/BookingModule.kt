package cz.cleansia.customer.core.booking

import cz.cleansia.customer.core.auth.AuthRetrofit
import dagger.Module
import dagger.Provides
import dagger.hilt.InstallIn
import dagger.hilt.components.SingletonComponent
import javax.inject.Singleton
import retrofit2.Retrofit

@Module
@InstallIn(SingletonComponent::class)
object BookingModule {
    // AuthRetrofit even though the backend endpoints are anonymous — when the
    // user is signed in the Bearer token lets the backend associate the order
    // with the account. For guests the auth interceptor is a no-op.
    @Provides
    @Singleton
    fun provideBookingApi(@AuthRetrofit retrofit: Retrofit): BookingApi =
        retrofit.create(BookingApi::class.java)
}
