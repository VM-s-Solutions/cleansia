package cz.cleansia.customer.core.user

import cz.cleansia.customer.api.client.GdprApi
import cz.cleansia.customer.api.client.SavedAddressApi as GenSavedAddressApi
import cz.cleansia.customer.api.client.UserApi
import cz.cleansia.customer.core.auth.AuthRetrofit
import dagger.Module
import dagger.Provides
import dagger.hilt.InstallIn
import dagger.hilt.components.SingletonComponent
import javax.inject.Singleton
import retrofit2.Retrofit

@Module
@InstallIn(SingletonComponent::class)
object UserModule {

    // Profile read/write (generated from OpenAPI spec — see app/build.gradle.kts
    // openApiGenerate block). Replaces the hand-written `UserApi` whose DTO
    // required-fields mismatched the backend response and silently nulled the
    // profile screen.
    @Provides
    @Singleton
    fun provideUserApi(@AuthRetrofit retrofit: Retrofit): UserApi =
        retrofit.create(UserApi::class.java)

    // Delete-account lives on GdprApi (route `/api/v1/Gdpr/delete-account`).
    // Was previously co-located on the hand-written UserApi; the generated
    // split matches the controller layout.
    @Provides
    @Singleton
    fun provideGdprApi(@AuthRetrofit retrofit: Retrofit): GdprApi =
        retrofit.create(GdprApi::class.java)

    // SavedAddress — generated client + hand-written adapter. Adapter narrows
    // the all-nullable wire DTO back to the strict hand-written SavedAddressDto
    // the address-list UI reads (id/label/street/city/zipCode/countryId all
    // load-bearing).
    @Provides
    @Singleton
    fun provideGenSavedAddressApi(@AuthRetrofit retrofit: Retrofit): GenSavedAddressApi =
        retrofit.create(GenSavedAddressApi::class.java)

    @Provides
    @Singleton
    fun provideSavedAddressApi(genSavedAddressApi: GenSavedAddressApi): SavedAddressApi =
        SavedAddressApi(genSavedAddressApi)
}
