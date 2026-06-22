package cz.cleansia.partner.core.notifications

import android.content.Context
import androidx.datastore.core.DataStore
import androidx.datastore.preferences.core.Preferences
import androidx.datastore.preferences.preferencesDataStore
import androidx.room.Room
import cz.cleansia.core.auth.SessionScopedCache
import cz.cleansia.core.notifications.DeviceRegistrationClient
import cz.cleansia.core.notifications.PushTokenDataStore
import cz.cleansia.core.notifications.PushTokenRepository
import cz.cleansia.partner.api.client.DeviceApi as GenDeviceApi
import cz.cleansia.partner.core.network.AuthRetrofit
import cz.cleansia.partner.core.notifications.db.NotificationDao
import cz.cleansia.partner.core.notifications.db.NotificationDatabase
import dagger.Binds
import dagger.Module
import dagger.Provides
import dagger.hilt.InstallIn
import dagger.hilt.android.qualifiers.ApplicationContext
import dagger.hilt.components.SingletonComponent
import dagger.multibindings.IntoSet
import retrofit2.Retrofit
import javax.inject.Singleton

private val Context.pushTokenDataStore by preferencesDataStore(name = "partner_push_token_state")

@Module
@InstallIn(SingletonComponent::class)
object NotificationsModule {

    @Provides
    @Singleton
    fun provideGenDeviceApi(@AuthRetrofit retrofit: Retrofit): GenDeviceApi =
        retrofit.create(GenDeviceApi::class.java)

    @Provides
    @Singleton
    @PushTokenDataStore
    fun providePushTokenDataStore(
        @ApplicationContext context: Context,
    ): DataStore<Preferences> = context.pushTokenDataStore

    @Provides
    @Singleton
    fun provideNotificationDatabase(
        @ApplicationContext context: Context,
    ): NotificationDatabase = Room.databaseBuilder(
        context,
        NotificationDatabase::class.java,
        "partner_notifications.db",
    ).build()

    @Provides
    @Singleton
    fun provideNotificationDao(db: NotificationDatabase): NotificationDao = db.notificationDao()
}

/**
 * Binds the partner [DeviceRegistrationClient] and joins [PushTokenRepository]
 * to the [SessionScopedCache] multibinding so its `clear()` runs on every
 * sign-out alongside the other per-user caches. Separate abstract module
 * because @Binds can't live in an `object` module.
 */
@Module
@InstallIn(SingletonComponent::class)
abstract class NotificationsBindingsModule {

    @Binds
    @Singleton
    abstract fun bindDeviceRegistrationClient(impl: DeviceApiClient): DeviceRegistrationClient

    @Binds
    @IntoSet
    abstract fun bindPushTokenRepository(impl: PushTokenRepository): SessionScopedCache
}
