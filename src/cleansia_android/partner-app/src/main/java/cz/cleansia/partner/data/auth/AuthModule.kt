package cz.cleansia.partner.data.auth

import cz.cleansia.core.auth.SessionScopedCache
import cz.cleansia.partner.core.auth.UserProfileStore
import dagger.Binds
import dagger.Module
import dagger.hilt.InstallIn
import dagger.hilt.components.SingletonComponent
import dagger.multibindings.IntoSet
import javax.inject.Singleton

@Module
@InstallIn(SingletonComponent::class)
abstract class AuthModule {

    @Binds
    @Singleton
    abstract fun bindAuthRepository(impl: AuthRepositoryImpl): AuthRepository

    @Binds
    @IntoSet
    abstract fun bindUserProfileStore(impl: UserProfileStore): SessionScopedCache
}
