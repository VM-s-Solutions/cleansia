package cz.cleansia.partner.data.orders

import cz.cleansia.core.auth.SessionScopedCache
import dagger.Binds
import dagger.Module
import dagger.hilt.InstallIn
import dagger.hilt.components.SingletonComponent
import dagger.multibindings.IntoSet
import javax.inject.Singleton

@Module
@InstallIn(SingletonComponent::class)
abstract class OrdersModule {

    @Binds
    @Singleton
    abstract fun bindOrdersRepository(impl: OrdersRepositoryImpl): OrdersRepository

    @Binds
    @IntoSet
    abstract fun bindOrdersRepositoryAsSessionScopedCache(
        impl: OrdersRepositoryImpl,
    ): SessionScopedCache
}
