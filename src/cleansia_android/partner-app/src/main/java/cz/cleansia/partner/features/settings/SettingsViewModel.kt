package cz.cleansia.partner.features.settings

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.partner.core.settings.AppSettings
import cz.cleansia.partner.core.settings.AppSettingsRepository
import cz.cleansia.partner.core.settings.LanguagePreference
import cz.cleansia.partner.core.settings.LanguagePreferenceSync
import cz.cleansia.partner.core.settings.ThemePreference
import cz.cleansia.partner.data.auth.AuthRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.SharingStarted
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.stateIn
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import javax.inject.Inject

data class SettingsUiState(
    val isSignedOut: Boolean = false,
)

@HiltViewModel
class SettingsViewModel @Inject constructor(
    private val appSettingsRepository: AppSettingsRepository,
    private val authRepository: AuthRepository,
    private val languageSync: LanguagePreferenceSync,
) : ViewModel() {

    val settings: StateFlow<AppSettings> = appSettingsRepository.settings.stateIn(
        scope = viewModelScope,
        started = SharingStarted.WhileSubscribed(5_000),
        initialValue = AppSettings(),
    )

    private val _uiState = MutableStateFlow(SettingsUiState())
    val uiState: StateFlow<SettingsUiState> = _uiState

    /**
     * Persists the choice, then tells the server. `PreferredLanguageCode` is what the period-closed
     * email and the payout invoice PDF are rendered in, and DataStore alone leaves it frozen at
     * whatever signup sent.
     *
     * What goes on the wire is the *resolved* tag: "System" persists as a null tag, the server cannot
     * see this handset's locale, and the validator only accepts one of the five supported codes.
     */
    fun setLanguage(language: LanguagePreference) {
        viewModelScope.launch {
            appSettingsRepository.setLanguage(language)
            languageSync.send(appSettingsRepository.emailLanguageTag())
        }
    }

    fun setTheme(theme: ThemePreference) {
        viewModelScope.launch { appSettingsRepository.setTheme(theme) }
    }

    fun signOut() {
        viewModelScope.launch {
            authRepository.logout()
            _uiState.update { it.copy(isSignedOut = true) }
        }
    }
}
