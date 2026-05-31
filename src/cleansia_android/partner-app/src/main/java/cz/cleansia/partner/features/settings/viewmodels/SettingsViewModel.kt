package cz.cleansia.partner.features.settings.viewmodels

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.partner.core.settings.AppSettings
import cz.cleansia.partner.core.settings.AppSettingsRepository
import cz.cleansia.partner.core.settings.LanguagePreference
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
) : ViewModel() {

    val settings: StateFlow<AppSettings> = appSettingsRepository.settings.stateIn(
        scope = viewModelScope,
        started = SharingStarted.WhileSubscribed(5_000),
        initialValue = AppSettings(),
    )

    private val _uiState = MutableStateFlow(SettingsUiState())
    val uiState: StateFlow<SettingsUiState> = _uiState

    fun setLanguage(language: LanguagePreference) {
        viewModelScope.launch { appSettingsRepository.setLanguage(language) }
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
