package cz.cleansia.partner.core.devices

import cz.cleansia.core.devices.DeviceManagementApi
import cz.cleansia.partner.core.network.AuthRetrofit
import dagger.Module
import dagger.Provides
import dagger.hilt.InstallIn
import dagger.hilt.components.SingletonComponent
import retrofit2.Retrofit
import javax.inject.Singleton

@Module
@InstallIn(SingletonComponent::class)
object DevicesModule {

    @Provides
    @Singleton
    fun provideDeviceManagementApi(@AuthRetrofit retrofit: Retrofit): DeviceManagementApi =
        retrofit.create(DeviceManagementApi::class.java)
}
