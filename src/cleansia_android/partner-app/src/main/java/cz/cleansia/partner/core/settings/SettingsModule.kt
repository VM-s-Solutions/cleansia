package cz.cleansia.partner.core.settings

import android.content.Context
import androidx.datastore.core.DataStore
import androidx.datastore.preferences.core.Preferences
import androidx.datastore.preferences.preferencesDataStore
import dagger.Module
import dagger.Provides
import dagger.hilt.InstallIn
import dagger.hilt.android.qualifiers.ApplicationContext
import dagger.hilt.components.SingletonComponent
import javax.inject.Qualifier
import javax.inject.Singleton

private val Context.appSettingsDataStore by preferencesDataStore(name = "partner_app_settings")

@Qualifier
@Retention(AnnotationRetention.BINARY)
annotation class AppSettingsDataStore

@Module
@InstallIn(SingletonComponent::class)
object SettingsModule {

    @Provides
    @Singleton
    @AppSettingsDataStore
    fun provideAppSettingsDataStore(
        @ApplicationContext context: Context,
    ): DataStore<Preferences> = context.appSettingsDataStore

    @Provides
    @Singleton
    fun provideAppSettingsRepository(
        @AppSettingsDataStore dataStore: DataStore<Preferences>,
        @ApplicationContext context: Context,
    ): AppSettingsRepository = AppSettingsRepository(dataStore, context)
}
