package cz.cleansia.partner.data.payroll

import dagger.Binds
import dagger.Module
import dagger.hilt.InstallIn
import dagger.hilt.components.SingletonComponent
import javax.inject.Singleton

@Module
@InstallIn(SingletonComponent::class)
abstract class PayrollModule {

    @Binds @Singleton
    abstract fun bindPeriodPayRepository(impl: PeriodPayRepositoryImpl): PeriodPayRepository
}
