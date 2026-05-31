package cz.cleansia.partner.data.invoices

import dagger.Binds
import dagger.Module
import dagger.hilt.InstallIn
import dagger.hilt.components.SingletonComponent
import javax.inject.Singleton

@Module
@InstallIn(SingletonComponent::class)
abstract class InvoicesModule {
    @Binds @Singleton
    abstract fun bindInvoicesRepository(impl: InvoicesRepositoryImpl): InvoicesRepository
}
