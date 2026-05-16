package cz.cleansia.customer.core.booking

import cz.cleansia.customer.api.client.OrderApi as GenOrderApi
import dagger.Module
import dagger.Provides
import dagger.hilt.InstallIn
import dagger.hilt.components.SingletonComponent
import javax.inject.Singleton

@Module
@InstallIn(SingletonComponent::class)
object BookingModule {
    // BookingApi is now an adapter that re-projects the generated OrderApi's
    // Quote + CreateOrder endpoints. The generated GenOrderApi singleton is
    // provided by OrderModule (this lives in the same Hilt component, so the
    // single provider is shared across both adapters).
    @Provides
    @Singleton
    fun provideBookingApi(orderApi: GenOrderApi): BookingApi = BookingApi(orderApi)
}
