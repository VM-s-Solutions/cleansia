package cz.cleansia.customer.features.profile

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.customer.core.settings.AppSettings
import cz.cleansia.customer.core.settings.AppSettingsRepository
import cz.cleansia.customer.core.settings.LanguagePreference
import cz.cleansia.customer.core.settings.LanguagePreferenceSync
import dagger.hilt.android.lifecycle.HiltViewModel
import javax.inject.Inject
import kotlinx.coroutines.flow.SharingStarted
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.stateIn
import kotlinx.coroutines.launch

@HiltViewModel
class LanguageViewModel @Inject constructor(
    private val appSettingsRepository: AppSettingsRepository,
    private val languageSync: LanguagePreferenceSync,
) : ViewModel() {

    val settings: StateFlow<AppSettings> = appSettingsRepository.settings.stateIn(
        scope = viewModelScope,
        started = SharingStarted.WhileSubscribed(5_000),
        initialValue = AppSettings(),
    )

    /**
     * Persists the choice, then tells the server — `PreferredLanguageCode` is the only input to the
     * language every server-rendered mail is written in, and DataStore alone leaves it frozen at
     * whatever signup sent.
     *
     * What goes on the wire is the *resolved* tag: "System" persists as a null tag, the server cannot
     * see this device's locale, and the validator only accepts one of the five supported codes.
     */
    fun setLanguage(language: LanguagePreference) {
        viewModelScope.launch {
            appSettingsRepository.setLanguage(language)
            languageSync.send(appSettingsRepository.emailLanguageTag())
        }
    }
}
