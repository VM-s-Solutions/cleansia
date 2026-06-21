package cz.cleansia.partner.features.auth.viewmodels

import cz.cleansia.core.validation.EmailValidator
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.partner.core.network.ApiErrorTranslator
import cz.cleansia.core.network.ApiResult
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

data class RegisterUiState(
    val firstName: String = "",
    val lastName: String = "",
    val email: String = "",
    val password: String = "",
    val confirmPassword: String = "",
    val acceptTerms: Boolean = false,
    val isLoading: Boolean = false,
    val error: String? = null,
    val firstNameError: String? = null,
    val lastNameError: String? = null,
    val emailError: String? = null,
    val passwordError: String? = null,
    val confirmPasswordError: String? = null,
    val termsError: String? = null,
    val isRegistrationSuccessful: Boolean = false,
) {
    val passwordHasMinLength get() = password.length >= 8
    val passwordHasLetter get() = password.any { it.isLetter() }
    val passwordHasNumber get() = password.any { it.isDigit() }
    val passwordsMatch get() = password.isNotEmpty() && password == confirmPassword
}

@HiltViewModel
class RegisterViewModel @Inject constructor(
    private val authRepository: AuthRepository,
    private val errorTranslator: ApiErrorTranslator,
    private val appSettingsRepository: AppSettingsRepository,
    private val snackbar: SnackbarController,
) : ViewModel() {

    private val _uiState = MutableStateFlow(RegisterUiState())
    val uiState: StateFlow<RegisterUiState> = _uiState.asStateFlow()

    fun onFirstNameChange(v: String) = _uiState.update { it.copy(firstName = v, firstNameError = null, error = null) }
    fun onLastNameChange(v: String) = _uiState.update { it.copy(lastName = v, lastNameError = null, error = null) }
    fun onEmailChange(v: String) = _uiState.update { it.copy(email = v, emailError = null, error = null) }
    fun onPasswordChange(v: String) = _uiState.update { it.copy(password = v, passwordError = null, error = null) }
    fun onConfirmPasswordChange(v: String) = _uiState.update { it.copy(confirmPassword = v, confirmPasswordError = null, error = null) }
    fun onAcceptTermsChange(v: Boolean) = _uiState.update { it.copy(acceptTerms = v, termsError = null, error = null) }

    fun register() {
        val state = _uiState.value
        var hasError = false
        if (state.firstName.isBlank()) { _uiState.update { it.copy(firstNameError = "First name is required") }; hasError = true }
        if (state.lastName.isBlank()) { _uiState.update { it.copy(lastNameError = "Last name is required") }; hasError = true }
        if (state.email.isBlank()) {
            _uiState.update { it.copy(emailError = "Email is required") }
            hasError = true
        } else if (!EmailValidator.isValid(state.email)) {
            _uiState.update { it.copy(emailError = "Please enter a valid email") }
            hasError = true
        }
        if (!state.passwordHasMinLength || !state.passwordHasLetter || !state.passwordHasNumber) {
            _uiState.update { it.copy(passwordError = "Password must be at least 8 characters with a letter and a number") }
            hasError = true
        }
        if (!state.passwordsMatch) {
            _uiState.update { it.copy(confirmPasswordError = "Passwords do not match") }
            hasError = true
        }
        if (!state.acceptTerms) {
            _uiState.update { it.copy(termsError = "You must accept the terms") }
            hasError = true
        }
        if (hasError) return

        viewModelScope.launch {
            _uiState.update { it.copy(isLoading = true, error = null) }
            val language = appSettingsRepository.settings.first().language.tag ?: "en"
            when (val result = authRepository.register(
                email = state.email,
                password = state.password,
                firstName = state.firstName,
                lastName = state.lastName,
                language = language,
            )) {
                is ApiResult.Success -> {
                    _uiState.update { it.copy(isLoading = false, isRegistrationSuccessful = true) }
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
