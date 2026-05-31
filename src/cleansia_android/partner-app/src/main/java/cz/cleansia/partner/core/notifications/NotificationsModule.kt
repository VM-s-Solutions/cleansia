package cz.cleansia.partner.core.notifications

import android.content.Context
import androidx.room.Room
import cz.cleansia.core.auth.SessionScopedCache
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

@Module
@InstallIn(SingletonComponent::class)
object NotificationsModule {

    @Provides
    @Singleton
    fun provideGenDeviceApi(@AuthRetrofit retrofit: Retrofit): GenDeviceApi =
        retrofit.create(GenDeviceApi::class.java)

    @Provides
    @Singleton
    fun provideDeviceApi(genDeviceApi: GenDeviceApi): DeviceApi = DeviceApi(genDeviceApi)

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
 * Joins [PushTokenRepository] to the [SessionScopedCache] multibinding so its
 * `clear()` runs on every sign-out alongside the other per-user caches.
 * Separate abstract module because @Binds can't live in an `object` module.
 */
@Module
@InstallIn(SingletonComponent::class)
abstract class NotificationsBindingsModule {

    @Binds
    @IntoSet
    abstract fun bindPushTokenRepository(impl: PushTokenRepository): SessionScopedCache
}
