package cz.cleansia.partner.data.payroll

import cz.cleansia.partner.core.network.AuthRetrofit
import dagger.Binds
import dagger.Module
import dagger.Provides
import dagger.hilt.InstallIn
import dagger.hilt.components.SingletonComponent
import retrofit2.Retrofit
import javax.inject.Singleton

@Module
@InstallIn(SingletonComponent::class)
abstract class PayrollModule {

    @Binds @Singleton
    abstract fun bindPeriodPayRepository(impl: PeriodPayRepositoryImpl): PeriodPayRepository

    companion object {
        @Provides
        @Singleton
        fun providePeriodPayApi(@AuthRetrofit retrofit: Retrofit): PeriodPayApi =
            retrofit.create(PeriodPayApi::class.java)
    }
}
