package cz.cleansia.customer.core.consent

import android.content.Context
import androidx.datastore.core.DataStore
import androidx.datastore.preferences.core.Preferences
import androidx.datastore.preferences.preferencesDataStore
import cz.cleansia.core.consent.SignupConsentClient
import cz.cleansia.core.consent.SignupConsentDataStore
import dagger.Binds
import dagger.Module
import dagger.Provides
import dagger.hilt.InstallIn
import dagger.hilt.android.qualifiers.ApplicationContext
import dagger.hilt.components.SingletonComponent
import javax.inject.Singleton

private val Context.signupConsentDataStore by preferencesDataStore(name = "signup_consent")

@Module
@InstallIn(SingletonComponent::class)
object ConsentModule {

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
