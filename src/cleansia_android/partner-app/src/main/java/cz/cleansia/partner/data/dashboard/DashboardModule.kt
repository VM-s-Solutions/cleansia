package cz.cleansia.partner.data.dashboard

import cz.cleansia.core.auth.SessionScopedCache
import dagger.Binds
import dagger.Module
import dagger.hilt.InstallIn
import dagger.hilt.components.SingletonComponent
import dagger.multibindings.IntoSet
import javax.inject.Singleton

@Module
@InstallIn(SingletonComponent::class)
abstract class DashboardModule {
    @Binds @Singleton
    abstract fun bindDashboardRepository(impl: DashboardRepositoryImpl): DashboardRepository

    @Binds @IntoSet
    abstract fun bindDashboardRepositoryAsSessionScopedCache(
        impl: DashboardRepositoryImpl,
    ): SessionScopedCache
}
