package cz.cleansia.partner.data.profile

import cz.cleansia.core.auth.SessionScopedCache
import dagger.Binds
import dagger.Module
import dagger.hilt.InstallIn
import dagger.hilt.components.SingletonComponent
import dagger.multibindings.IntoSet
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
}
