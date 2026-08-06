package cz.cleansia.partner.core.consent

import android.content.Context
import androidx.datastore.core.DataStore
import androidx.datastore.preferences.core.Preferences
import androidx.datastore.preferences.preferencesDataStore
import cz.cleansia.core.consent.SignupConsentClient
import cz.cleansia.core.consent.SignupConsentDataStore
import cz.cleansia.partner.api.client.GdprApi
import cz.cleansia.partner.core.network.AuthRetrofit
import dagger.Binds
import dagger.Module
import dagger.Provides
import dagger.hilt.InstallIn
import dagger.hilt.android.qualifiers.ApplicationContext
import dagger.hilt.components.SingletonComponent
import javax.inject.Singleton
import retrofit2.Retrofit

private val Context.signupConsentDataStore by preferencesDataStore(name = "signup_consent")

@Module
@InstallIn(SingletonComponent::class)
object ConsentModule {

    @Provides
    @Singleton
    fun provideGdprApi(@AuthRetrofit retrofit: Retrofit): GdprApi =
        retrofit.create(GdprApi::class.java)

    @Provides
    @Singleton
    @SignupConsentDataStore
    fun provideSignupConsentDataStore(
        @ApplicationContext context: Context,
    ): DataStore<Preferences> = context.signupConsentDataStore
}

@Module
@InstallIn(SingletonComponent::class)
abstract class ConsentBindingsModule {

    @Binds
    @Singleton
    abstract fun bindSignupConsentClient(impl: GdprConsentClient): SignupConsentClient
}
