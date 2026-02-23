package cz.cleansia.partner.di

import cz.cleansia.partner.domain.repositories.AuthRepository
import cz.cleansia.partner.domain.repositories.AuthRepositoryImpl
import cz.cleansia.partner.domain.repositories.DashboardRepository
import cz.cleansia.partner.domain.repositories.DashboardRepositoryImpl
import cz.cleansia.partner.domain.repositories.InvoicesRepository
import cz.cleansia.partner.domain.repositories.InvoicesRepositoryImpl
import cz.cleansia.partner.domain.repositories.OrdersRepository
import cz.cleansia.partner.domain.repositories.OrdersRepositoryImpl
import cz.cleansia.partner.domain.repositories.ProfileRepository
import cz.cleansia.partner.domain.repositories.ProfileRepositoryImpl
import dagger.Binds
import dagger.Module
import dagger.hilt.InstallIn
import dagger.hilt.components.SingletonComponent
import javax.inject.Singleton

@Module
@InstallIn(SingletonComponent::class)
abstract class RepositoryModule {

    @Binds
    @Singleton
    abstract fun bindAuthRepository(impl: AuthRepositoryImpl): AuthRepository

    @Binds
    @Singleton
    abstract fun bindDashboardRepository(impl: DashboardRepositoryImpl): DashboardRepository

    @Binds
    @Singleton
    abstract fun bindOrdersRepository(impl: OrdersRepositoryImpl): OrdersRepository

    @Binds
    @Singleton
    abstract fun bindInvoicesRepository(impl: InvoicesRepositoryImpl): InvoicesRepository

    @Binds
    @Singleton
    abstract fun bindProfileRepository(impl: ProfileRepositoryImpl): ProfileRepository
}
