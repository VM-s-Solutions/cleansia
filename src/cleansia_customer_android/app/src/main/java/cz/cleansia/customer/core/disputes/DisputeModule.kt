package cz.cleansia.customer.core.disputes

import cz.cleansia.customer.core.auth.AuthRetrofit
import dagger.Module
import dagger.Provides
import dagger.hilt.InstallIn
import dagger.hilt.components.SingletonComponent
import javax.inject.Singleton
import retrofit2.Retrofit

@Module
@InstallIn(SingletonComponent::class)
object DisputeModule {
    @Provides
    @Singleton
    fun provideDisputeApi(@AuthRetrofit retrofit: Retrofit): DisputeApi =
        retrofit.create(DisputeApi::class.java)
}
