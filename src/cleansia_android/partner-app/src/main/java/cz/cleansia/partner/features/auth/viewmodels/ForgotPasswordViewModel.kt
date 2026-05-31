package cz.cleansia.partner.features.auth.viewmodels

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.partner.core.network.ApiErrorTranslator
import cz.cleansia.partner.core.network.ApiResult
import cz.cleansia.partner.core.settings.AppSettingsRepository
import cz.cleansia.partner.data.auth.AuthRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import javax.inject.Inject

data class ForgotPasswordUiState(
    val email: String = "",
    val isLoading: Boolean = false,
    val error: String? = null,
    val emailError: String? = null,
    val isRequestSuccessful: Boolean = false,
)

@HiltViewModel
class ForgotPasswordViewModel @Inject constructor(
    private val authRepository: AuthRepository,
    private val errorTranslator: ApiErrorTranslator,
    private val appSettingsRepository: AppSettingsRepository,
    private val snackbar: SnackbarController,
) : ViewModel() {

    private val _uiState = MutableStateFlow(ForgotPasswordUiState())
    val uiState: StateFlow<ForgotPasswordUiState> = _uiState.asStateFlow()

    fun onEmailChange(email: String) {
        _uiState.update { it.copy(email = email, emailError = null, error = null) }
    }

    fun requestPasswordReset() {
        val state = _uiState.value
        if (state.email.isBlank()) {
            _uiState.update { it.copy(emailError = "Email is required") }
            return
        }
        if (!android.util.Patterns.EMAIL_ADDRESS.matcher(state.email).matches()) {
            _uiState.update { it.copy(emailError = "Please enter a valid email") }
            return
        }

        viewModelScope.launch {
            _uiState.update { it.copy(isLoading = true, error = null) }
            val language = appSettingsRepository.settings.first().language.tag ?: "en"
            when (val result = authRepository.forgotPassword(state.email, language)) {
                is ApiResult.Success -> {
                    _uiState.update { it.copy(isLoading = false, isRequestSuccessful = true) }
                }
                is ApiResult.Error -> {
                    snackbar.showError(errorTranslator.translate(result.error))
                    _uiState.update { it.copy(isLoading = false) }
                }
            }
        }
    }

    fun clearError() {
        _uiState.update { it.copy(error = null) }
    }
}
