package cz.cleansia.partner.features.auth.viewmodels

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.partner.core.network.ApiErrorTranslator
import cz.cleansia.partner.core.network.ApiResult
import cz.cleansia.partner.core.storage.PreferencesManager
import cz.cleansia.partner.domain.repositories.AuthRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import javax.inject.Inject

data class LoginUiState(
    val email: String = "",
    val password: String = "",
    val rememberMe: Boolean = false,
    val isLoading: Boolean = false,
    val error: String? = null,
    val emailError: String? = null,
    val passwordError: String? = null,
    val isLoginSuccessful: Boolean = false,
    val requiresEmailConfirmation: Boolean = false,
    val biometricEnabled: Boolean = false,
    val showBiometricPrompt: Boolean = false
)

@HiltViewModel
class LoginViewModel @Inject constructor(
    private val authRepository: AuthRepository,
    private val preferencesManager: PreferencesManager,
    private val errorTranslator: ApiErrorTranslator
) : ViewModel() {

    private val _uiState = MutableStateFlow(LoginUiState())
    val uiState: StateFlow<LoginUiState> = _uiState.asStateFlow()

    init {
        loadSavedEmail()
        loadBiometricPreference()
    }

    private fun loadBiometricPreference() {
        viewModelScope.launch {
            val biometricEnabled = preferencesManager.biometricEnabled.first()
            _uiState.update { it.copy(biometricEnabled = biometricEnabled) }
        }
    }

    fun onBiometricLoginRequested() {
        _uiState.update { it.copy(showBiometricPrompt = true) }
    }

    fun onBiometricPromptDismissed() {
        _uiState.update { it.copy(showBiometricPrompt = false) }
    }

    fun onBiometricSuccess() {
        _uiState.update { it.copy(showBiometricPrompt = false) }
        // Attempt auto-login with saved credentials
        loginWithBiometric()
    }

    private fun loginWithBiometric() {
        viewModelScope.launch {
            val savedEmail = preferencesManager.savedEmail.first()
            if (savedEmail.isNullOrBlank()) {
                _uiState.update { it.copy(error = "No saved credentials for biometric login") }
                return@launch
            }

            _uiState.update { it.copy(isLoading = true, error = null) }

            // For biometric login, we use a special endpoint or token refresh
            // In this implementation, we just mark as successful if biometric passes
            // and the user has saved credentials (remember me was enabled)
            when (val result = authRepository.refreshToken()) {
                is ApiResult.Success -> {
                    _uiState.update {
                        it.copy(
                            isLoading = false,
                            isLoginSuccessful = true,
                            requiresEmailConfirmation = !result.data.isEmailConfirmed
                        )
                    }
                }
                is ApiResult.Error -> {
                    _uiState.update {
                        it.copy(
                            isLoading = false,
                            error = "Biometric login failed. Please login with password."
                        )
                    }
                }
            }
        }
    }

    private fun loadSavedEmail() {
        viewModelScope.launch {
            val rememberMe = preferencesManager.rememberMe.first()
            if (rememberMe) {
                val savedEmail = preferencesManager.savedEmail.first()
                _uiState.update {
                    it.copy(
                        email = savedEmail ?: "",
                        rememberMe = true
                    )
                }
            }
        }
    }

    fun onEmailChange(email: String) {
        _uiState.update {
            it.copy(
                email = email,
                emailError = null,
                error = null
            )
        }
    }

    fun onPasswordChange(password: String) {
        _uiState.update {
            it.copy(
                password = password,
                passwordError = null,
                error = null
            )
        }
    }

    fun onRememberMeChange(rememberMe: Boolean) {
        _uiState.update { it.copy(rememberMe = rememberMe) }
    }

    fun login() {
        val state = _uiState.value

        // Validate inputs
        var hasError = false

        if (state.email.isBlank()) {
            _uiState.update { it.copy(emailError = "Email is required") }
            hasError = true
        } else if (!isValidEmail(state.email)) {
            _uiState.update { it.copy(emailError = "Please enter a valid email") }
            hasError = true
        }

        if (state.password.isBlank()) {
            _uiState.update { it.copy(passwordError = "Password is required") }
            hasError = true
        }

        if (hasError) return

        viewModelScope.launch {
            _uiState.update { it.copy(isLoading = true, error = null) }

            when (val result = authRepository.login(state.email, state.password)) {
                is ApiResult.Success -> {
                    // Save remember me preference
                    if (state.rememberMe) {
                        preferencesManager.setRememberMe(true, state.email)
                    } else {
                        preferencesManager.setRememberMe(false)
                    }

                    _uiState.update {
                        it.copy(
                            isLoading = false,
                            isLoginSuccessful = true,
                            requiresEmailConfirmation = !result.data.isEmailConfirmed
                        )
                    }
                }

                is ApiResult.Error -> {
                    _uiState.update {
                        it.copy(
                            isLoading = false,
                            error = errorTranslator.translateError(result.error)
                        )
                    }
                }
            }
        }
    }

    fun clearError() {
        _uiState.update { it.copy(error = null) }
    }

    private fun isValidEmail(email: String): Boolean {
        return android.util.Patterns.EMAIL_ADDRESS.matcher(email).matches()
    }
}
