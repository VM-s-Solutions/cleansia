package cz.cleansia.partner.data.profile

import cz.cleansia.core.auth.SessionScopedCache
import cz.cleansia.partner.core.network.AuthRetrofit
import dagger.Binds
import dagger.Module
import dagger.Provides
import dagger.hilt.InstallIn
import dagger.hilt.components.SingletonComponent
import dagger.multibindings.IntoSet
import retrofit2.Retrofit
import javax.inject.Singleton

@Module
@InstallIn(SingletonComponent::class)
abstract class ProfileModule {

    @Binds
    @Singleton
    abstract fun bindProfileRepository(impl: ProfileRepositoryImpl): ProfileRepository

    @Binds
    @IntoSet
    abstract fun bindProfileRepositoryAsSessionScopedCache(
        impl: ProfileRepositoryImpl,
    ): SessionScopedCache

    companion object {
        @Provides
        @Singleton
        fun provideJobRadiusApi(@AuthRetrofit retrofit: Retrofit): JobRadiusApi =
            retrofit.create(JobRadiusApi::class.java)
    }
}
