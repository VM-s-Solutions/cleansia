package cz.cleansia.customer.core.settings

import android.content.Context
import dagger.Binds
import dagger.Module
import dagger.Provides
import dagger.hilt.InstallIn
import dagger.hilt.android.qualifiers.ApplicationContext
import dagger.hilt.components.SingletonComponent
import javax.inject.Singleton

@Module
@InstallIn(SingletonComponent::class)
object SettingsModule {
    @Provides
    @Singleton
    fun provideAppSettingsRepository(
        @ApplicationContext context: Context,
    ): AppSettingsRepository = AppSettingsRepository(context)
}

@Module
@InstallIn(SingletonComponent::class)
abstract class LanguagePreferenceSyncModule {
    @Binds
    @Singleton
    abstract fun bindLanguagePreferenceSync(impl: LiveLanguagePreferenceSync): LanguagePreferenceSync
}
