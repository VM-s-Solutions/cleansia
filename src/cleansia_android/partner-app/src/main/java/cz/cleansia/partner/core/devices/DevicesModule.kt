package cz.cleansia.partner.core.devices

import cz.cleansia.partner.core.network.AuthRetrofit
import dagger.Binds
import dagger.Module
import dagger.Provides
import dagger.hilt.InstallIn
import dagger.hilt.components.SingletonComponent
import retrofit2.Retrofit
import javax.inject.Singleton

@Module
@InstallIn(SingletonComponent::class)
abstract class DevicesModule {

    @Binds @Singleton
    abstract fun bindDevicesRepository(impl: DevicesRepositoryImpl): DevicesRepository

    companion object {
        @Provides
        @Singleton
        fun provideDeviceManagementApi(@AuthRetrofit retrofit: Retrofit): DeviceManagementApi =
            retrofit.create(DeviceManagementApi::class.java)
    }
}
