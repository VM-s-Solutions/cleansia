package cz.cleansia.partner.core.servicearea

import cz.cleansia.core.servicearea.ServiceAreaDataSource
import cz.cleansia.core.servicearea.ServiceAreaProvider
import dagger.Binds
import dagger.Module
import dagger.Provides
import dagger.hilt.InstallIn
import dagger.hilt.components.SingletonComponent
import javax.inject.Singleton

/**
 * Partner-app bridge for the `:core` service-area stack. Exposes the
 * shared [ServiceAreaProvider] as a Hilt singleton (consumed by the
 * Address section's service-area indicator) and binds the data-source
 * adapter that wraps partner-app's NSwag clients.
 *
 * The NSwag-generated `CountryApi` + `ServiceCityApi` instances
 * themselves are provided by [cz.cleansia.partner.core.network.NetworkModule].
 */
@Module
@InstallIn(SingletonComponent::class)
object ServiceAreaModule {

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
        impl: PartnerServiceAreaDataSource,
    ): ServiceAreaDataSource
}
