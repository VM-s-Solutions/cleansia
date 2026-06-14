package cz.cleansia.partner.features.auth.viewmodels

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.core.ui.state.ActionState
import cz.cleansia.partner.core.network.ApiErrorTranslator
import cz.cleansia.partner.core.network.ApiResult
import cz.cleansia.partner.data.auth.AuthRepository
import cz.cleansia.partner.data.auth.LoginOutcome
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableSharedFlow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.SharedFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asSharedFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import javax.inject.Inject

data class LoginFormState(
    val email: String = "",
    val password: String = "",
    val rememberMe: Boolean = true,
    val emailError: String? = null,
    val passwordError: String? = null,
)

data class LoginSuccess(val requiresEmailConfirmation: Boolean)

@HiltViewModel
class LoginViewModel @Inject constructor(
    private val authRepository: AuthRepository,
    private val errorTranslator: ApiErrorTranslator,
    private val snackbar: SnackbarController,
) : ViewModel() {

    private val _uiState = MutableStateFlow(LoginFormState())
    val uiState: StateFlow<LoginFormState> = _uiState.asStateFlow()

    private val _loginState = MutableStateFlow<ActionState>(ActionState.Idle)
    val loginState: StateFlow<ActionState> = _loginState.asStateFlow()

    private val _loginSuccess = MutableSharedFlow<LoginSuccess>(extraBufferCapacity = 1)
    val loginSuccess: SharedFlow<LoginSuccess> = _loginSuccess.asSharedFlow()

    fun onEmailChange(email: String) {
        _uiState.update { it.copy(email = email, emailError = null) }
    }

    fun onPasswordChange(password: String) {
        _uiState.update { it.copy(password = password, passwordError = null) }
    }

    fun onRememberMeChange(rememberMe: Boolean) {
        _uiState.update { it.copy(rememberMe = rememberMe) }
    }

    fun login() {
        if (_loginState.value is ActionState.Submitting) return
        val state = _uiState.value

        var hasError = false
        if (state.email.isBlank()) {
            _uiState.update { it.copy(emailError = "Email is required") }
            hasError = true
        } else if (!android.util.Patterns.EMAIL_ADDRESS.matcher(state.email).matches()) {
            _uiState.update { it.copy(emailError = "Please enter a valid email") }
            hasError = true
        }
        if (state.password.isBlank()) {
            _uiState.update { it.copy(passwordError = "Password is required") }
            hasError = true
        }
        if (hasError) return

        viewModelScope.launch {
            _loginState.value = ActionState.Submitting
            when (val result = authRepository.login(state.email, state.password, state.rememberMe)) {
                is ApiResult.Success -> {
                    _loginState.value = ActionState.Idle
                    _loginSuccess.emit(
                        LoginSuccess(requiresEmailConfirmation = result.data is LoginOutcome.UnverifiedEmail),
                    )
                }
                is ApiResult.Error -> {
                    snackbar.showError(errorTranslator.translate(result.error))
                    _loginState.value = ActionState.Idle
                }
            }
        }
    }
}
