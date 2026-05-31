package cz.cleansia.customer.core.servicearea

import cz.cleansia.core.servicearea.ServiceAreaDataSource
import cz.cleansia.core.servicearea.ServiceAreaProvider
import cz.cleansia.customer.api.client.CountryApi
import cz.cleansia.customer.api.client.ServiceCityApi
import cz.cleansia.customer.core.auth.NoAuthRetrofit
import dagger.Binds
import dagger.Module
import dagger.Provides
import dagger.hilt.InstallIn
import dagger.hilt.components.SingletonComponent
import javax.inject.Singleton
import retrofit2.Retrofit

/**
 * Customer-app bridge for the `:core` service-area stack. Provides the
 * NSwag-generated API clients, plumbs them through
 * [CustomerServiceAreaDataSource], and exposes the shared
 * [ServiceAreaProvider] as a Hilt singleton both the address picker
 * and the booking flow consume.
 */
@Module
@InstallIn(SingletonComponent::class)
object ServiceAreaModule {

    // Anonymous endpoints — anyone hitting the booking flow can fetch the
    // serviced country / city lists without auth. Use the NoAuth Retrofit.
    @Provides
    @Singleton
    fun provideGenCountryApi(@NoAuthRetrofit retrofit: Retrofit): CountryApi =
        retrofit.create(CountryApi::class.java)

    @Provides
    @Singleton
    fun provideGenServiceCityApi(@NoAuthRetrofit retrofit: Retrofit): ServiceCityApi =
        retrofit.create(ServiceCityApi::class.java)

    @Provides
    @Singleton
    fun provideServiceAreaProvider(
        dataSource: ServiceAreaDataSource,
    ): ServiceAreaProvider = ServiceAreaProvider(dataSource)
}

@Module
@InstallIn(SingletonComponent::class)
abstract class ServiceAreaDataSourceModule {
    @Binds
    @Singleton
    abstract fun bindDataSource(
        impl: CustomerServiceAreaDataSource,
    ): ServiceAreaDataSource
}
