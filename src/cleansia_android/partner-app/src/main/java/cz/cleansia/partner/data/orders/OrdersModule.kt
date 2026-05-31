package cz.cleansia.partner.data.orders

import dagger.Binds
import dagger.Module
import dagger.hilt.InstallIn
import dagger.hilt.components.SingletonComponent
import javax.inject.Singleton

@Module
@InstallIn(SingletonComponent::class)
abstract class OrdersModule {

    @Binds
    @Singleton
    abstract fun bindOrdersRepository(impl: OrdersRepositoryImpl): OrdersRepository
}
