package cz.cleansia.partner.data.invoices

import cz.cleansia.core.auth.SessionScopedCache
import dagger.Binds
import dagger.Module
import dagger.hilt.InstallIn
import dagger.hilt.components.SingletonComponent
import dagger.multibindings.IntoSet
import javax.inject.Singleton

@Module
@InstallIn(SingletonComponent::class)
abstract class InvoicesModule {
    @Binds @Singleton
    abstract fun bindInvoicesRepository(impl: InvoicesRepositoryImpl): InvoicesRepository

    @Binds @IntoSet
    abstract fun bindInvoicesRepositoryAsSessionScopedCache(
        impl: InvoicesRepositoryImpl,
    ): SessionScopedCache
}
