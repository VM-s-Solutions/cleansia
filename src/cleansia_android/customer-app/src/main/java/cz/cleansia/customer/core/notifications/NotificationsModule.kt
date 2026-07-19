package cz.cleansia.customer.core.notifications

import android.content.Context
import androidx.datastore.core.DataStore
import androidx.datastore.preferences.core.Preferences
import androidx.datastore.preferences.preferencesDataStore
import cz.cleansia.core.notifications.DeviceRegistrationClient
import cz.cleansia.core.notifications.PushTokenDataStore
import cz.cleansia.customer.api.client.DeviceApi as GenDeviceApi
import cz.cleansia.customer.api.client.NotificationPreferencesApi as GenNotificationPreferencesApi
import cz.cleansia.customer.core.auth.AuthRetrofit
import dagger.Binds
import dagger.Module
import dagger.Provides
import dagger.hilt.InstallIn
import dagger.hilt.android.qualifiers.ApplicationContext
import dagger.hilt.components.SingletonComponent
import javax.inject.Singleton
import retrofit2.Retrofit

private val Context.pushTokenDataStore by preferencesDataStore(name = "push_token_state")

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
    fun provideGenNotificationPreferencesApi(
        @AuthRetrofit retrofit: Retrofit,
    ): GenNotificationPreferencesApi =
        retrofit.create(GenNotificationPreferencesApi::class.java)

    @Provides
    @Singleton
    fun provideNotificationPreferencesApi(
        genNotificationPreferencesApi: GenNotificationPreferencesApi,
    ): NotificationPreferencesApi = NotificationPreferencesApi(genNotificationPreferencesApi)

    @Provides
    @Singleton
    fun provideNotificationFeedApi(@AuthRetrofit retrofit: Retrofit): NotificationFeedApi =
        retrofit.create(NotificationFeedApi::class.java)
}

@Module
@InstallIn(SingletonComponent::class)
abstract class NotificationsBindingsModule {

    @Binds
    @Singleton
    abstract fun bindDeviceRegistrationClient(impl: DeviceApiClient): DeviceRegistrationClient
}
