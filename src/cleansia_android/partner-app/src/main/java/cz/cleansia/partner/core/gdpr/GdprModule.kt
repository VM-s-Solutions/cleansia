package cz.cleansia.partner.core.gdpr

import cz.cleansia.partner.api.client.GdprApi
import cz.cleansia.partner.core.network.AuthRetrofit
import dagger.Module
import dagger.Provides
import dagger.hilt.InstallIn
import dagger.hilt.components.SingletonComponent
import retrofit2.Retrofit
import javax.inject.Singleton

@Module
@InstallIn(SingletonComponent::class)
object GdprModule {

    @Provides
    @Singleton
    fun provideGdprApi(@AuthRetrofit retrofit: Retrofit): GdprApi =
        retrofit.create(GdprApi::class.java)
}
