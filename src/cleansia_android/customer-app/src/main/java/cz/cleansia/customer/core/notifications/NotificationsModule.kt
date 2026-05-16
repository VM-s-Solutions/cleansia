package cz.cleansia.customer.core.notifications

import cz.cleansia.customer.api.client.DeviceApi as GenDeviceApi
import cz.cleansia.customer.api.client.NotificationPreferencesApi as GenNotificationPreferencesApi
import cz.cleansia.customer.core.auth.AuthRetrofit
import dagger.Module
import dagger.Provides
import dagger.hilt.InstallIn
import dagger.hilt.components.SingletonComponent
import javax.inject.Singleton
import retrofit2.Retrofit

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
    fun provideGenNotificationPreferencesApi(
        @AuthRetrofit retrofit: Retrofit,
    ): GenNotificationPreferencesApi =
        retrofit.create(GenNotificationPreferencesApi::class.java)

    @Provides
    @Singleton
    fun provideNotificationPreferencesApi(
        genNotificationPreferencesApi: GenNotificationPreferencesApi,
    ): NotificationPreferencesApi = NotificationPreferencesApi(genNotificationPreferencesApi)
}
