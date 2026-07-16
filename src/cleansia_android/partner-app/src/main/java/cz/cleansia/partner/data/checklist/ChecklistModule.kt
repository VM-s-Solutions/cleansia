package cz.cleansia.partner.data.checklist

import cz.cleansia.core.auth.SessionScopedCache
import dagger.Binds
import dagger.Module
import dagger.hilt.InstallIn
import dagger.hilt.components.SingletonComponent
import dagger.multibindings.IntoSet

@Module
@InstallIn(SingletonComponent::class)
abstract class ChecklistModule {

    @Binds
    @IntoSet
    abstract fun bindOrderChecklistRepository(
        impl: OrderChecklistRepository,
    ): SessionScopedCache
}
