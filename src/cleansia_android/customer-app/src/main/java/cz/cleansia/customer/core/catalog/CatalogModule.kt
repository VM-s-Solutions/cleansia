package cz.cleansia.customer.core.catalog

import cz.cleansia.customer.api.client.ExtraApi
import cz.cleansia.customer.api.client.PackageApi
import cz.cleansia.customer.api.client.ServiceApi
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

    // Generated clients — wire each to the NoAuth Retrofit since the catalog
    // endpoints are anonymous (rendered before sign-in for the booking wizard).
    @Provides
    @Singleton
    fun provideGenServiceApi(@NoAuthRetrofit retrofit: Retrofit): ServiceApi =
        retrofit.create(ServiceApi::class.java)

    @Provides
    @Singleton
    fun provideGenPackageApi(@NoAuthRetrofit retrofit: Retrofit): PackageApi =
        retrofit.create(PackageApi::class.java)

    @Provides
    @Singleton
    fun provideGenExtraApi(@NoAuthRetrofit retrofit: Retrofit): ExtraApi =
        retrofit.create(ExtraApi::class.java)

    // App-facing adapter — combines the three generated clients into one
    // surface that returns the hand-written DTOs the repo + UI already
    // consume. Keeps consumer call sites unchanged.
    @Provides
    @Singleton
    fun provideCatalogApi(
        serviceApi: ServiceApi,
        packageApi: PackageApi,
        extraApi: ExtraApi,
    ): CatalogApi = CatalogApi(serviceApi, packageApi, extraApi)
}
