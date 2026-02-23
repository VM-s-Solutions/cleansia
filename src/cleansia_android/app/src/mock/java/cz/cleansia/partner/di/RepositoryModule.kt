package cz.cleansia.partner.di

import cz.cleansia.partner.core.storage.TokenManager
import cz.cleansia.partner.domain.repositories.AuthRepository
import cz.cleansia.partner.domain.repositories.DashboardRepository
import cz.cleansia.partner.domain.repositories.InvoicesRepository
import cz.cleansia.partner.domain.repositories.OrdersRepository
import cz.cleansia.partner.domain.repositories.ProfileRepository
import cz.cleansia.partner.mock.MockAuthRepository
import cz.cleansia.partner.mock.MockDashboardRepository
import cz.cleansia.partner.mock.MockInvoicesRepository
import cz.cleansia.partner.mock.MockOrdersRepository
import cz.cleansia.partner.mock.MockProfileRepository
import dagger.Module
import dagger.Provides
import dagger.hilt.InstallIn
import dagger.hilt.components.SingletonComponent
import javax.inject.Singleton

@Module
@InstallIn(SingletonComponent::class)
object RepositoryModule {

    @Provides
    @Singleton
    fun provideAuthRepository(tokenManager: TokenManager): AuthRepository =
        MockAuthRepository(tokenManager)

    @Provides
    @Singleton
    fun provideDashboardRepository(): DashboardRepository =
        MockDashboardRepository()

    @Provides
    @Singleton
    fun provideOrdersRepository(): OrdersRepository =
        MockOrdersRepository()

    @Provides
    @Singleton
    fun provideInvoicesRepository(): InvoicesRepository =
        MockInvoicesRepository()

    @Provides
    @Singleton
    fun provideProfileRepository(): ProfileRepository =
        MockProfileRepository()
}
