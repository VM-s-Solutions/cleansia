package cz.cleansia.customer.core.catalog

import cz.cleansia.customer.core.auth.NoAuthRetrofit
import dagger.Module
import dagger.Provides
import dagger.hilt.InstallIn
import dagger.hilt.components.SingletonComponent
import javax.inject.Singleton
import retrofit2.Retrofit

@Module
@InstallIn(SingletonComponent::class)
object CatalogModule {

    @Provides
    @Singleton
    fun provideCatalogApi(@NoAuthRetrofit retrofit: Retrofit): CatalogApi =
        retrofit.create(CatalogApi::class.java)
}
