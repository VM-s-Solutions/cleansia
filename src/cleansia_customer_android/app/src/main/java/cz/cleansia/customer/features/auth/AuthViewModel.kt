package cz.cleansia.customer.features.auth

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.customer.R
import cz.cleansia.customer.core.auth.AuthRepository
import cz.cleansia.customer.core.auth.AuthResult
import cz.cleansia.customer.core.settings.AppSettingsRepository
import cz.cleansia.customer.ui.snackbar.SnackbarController
import dagger.hilt.android.lifecycle.HiltViewModel
import javax.inject.Inject
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.launch

/**
 * Single ViewModel serving all four auth screens (SignIn, SignUp, EmailVerify,
 * ForgotPassword). Each screen observes [uiState] for the loading flag + the
 * last outcome, and calls the method it needs.
 *
 * Keeping it in one VM means the screens don't fight over shared side effects
 * (e.g. post-register → auto-fill email on verify screen), and there's one
 * place to reason about the auth FSM.
 */
@HiltViewModel
class AuthViewModel @Inject constructor(
    private val authRepository: AuthRepository,
    private val settings: AppSettingsRepository,
    private val snackbar: SnackbarController,
) : ViewModel() {

    private val _uiState = MutableStateFlow(AuthUiState())
    val uiState: StateFlow<AuthUiState> = _uiState.asStateFlow()

    fun signIn(email: String, password: String, rememberMe: Boolean) {
        _uiState.value = AuthUiState(loading = true)
        viewModelScope.launch {
            _uiState.value = when (val result = authRepository.login(email, password, rememberMe)) {
                is AuthResult.Success -> AuthUiState(outcome = AuthOutcome.SignedIn)
                is AuthResult.EmailUnconfirmed -> AuthUiState(outcome = AuthOutcome.NeedsEmailConfirm(result.email ?: email))
                is AuthResult.Error -> {
                    snackbar.showError(result.message)
                    AuthUiState()
                }
            }
        }
    }

    fun register(
        email: String,
        password: String,
        firstName: String,
        lastName: String,
        /**
         * Loyalty Phase C — optional referral code from the signup form.
         * Trimmed/blank-coalesced to null so we don't ship empty strings on
         * the wire. Bad codes don't block submit (backend is fail-soft).
         */
        referralCode: String? = null,
    ) {
        _uiState.value = AuthUiState(loading = true)
        viewModelScope.launch {
            val language = currentLanguageCode()
            authRepository.register(
                email = email,
                password = password,
                firstName = firstName,
                lastName = lastName,
                language = language,
                referralCode = referralCode?.trim()?.uppercase()?.ifBlank { null },
            )
                .onSuccess { _uiState.value = AuthUiState(outcome = AuthOutcome.NeedsEmailConfirm(email)) }
                .onFailure {
                    snackbar.showErrorKey(R.string.error_generic_unknown)
                    _uiState.value = AuthUiState()
                }
        }
    }

    fun confirmEmail(code: String) {
        _uiState.value = AuthUiState(loading = true)
        viewModelScope.launch {
            _uiState.value = when (val result = authRepository.confirmEmail(code)) {
                is AuthResult.Success -> AuthUiState(outcome = AuthOutcome.SignedIn)
                is AuthResult.EmailUnconfirmed -> {
                    // Unexpected but harmless — server said not confirmed after a successful confirm call.
                    snackbar.showWarningKey(R.string.error_auth_invalid_confirmation_code)
                    AuthUiState()
                }
                is AuthResult.Error -> {
                    snackbar.showError(result.message)
                    AuthUiState()
                }
            }
        }
    }

    fun resendConfirmationEmail(email: String) {
        _uiState.value = AuthUiState(loading = true)
        viewModelScope.launch {
            val language = currentLanguageCode()
            authRepository.resendConfirmationEmail(email, language)
                .onSuccess {
                    snackbar.showSuccessKey(R.string.auth_resend_success)
                    _uiState.value = AuthUiState()
                }
                .onFailure {
                    snackbar.showErrorKey(R.string.error_email_sending_failed)
                    _uiState.value = AuthUiState()
                }
        }
    }

    /** User tapped the screen-level "dismiss" / after navigation handled an outcome, UI clears it. */
    fun clearState() {
        _uiState.value = AuthUiState()
    }

    /** The language code the backend expects on emails sent to this user. */
    private suspend fun currentLanguageCode(): String =
        settings.settings.first().language.tag ?: "en"
}

data class AuthUiState(
    val loading: Boolean = false,
    val outcome: AuthOutcome? = null,
)

sealed class AuthOutcome {
    /** Logged in with a valid confirmed-email session. Navigate to Home. */
    data object SignedIn : AuthOutcome()

    /** Login / register succeeded but user needs to verify email. Email pre-filled for the next screen. */
    data class NeedsEmailConfirm(val email: String) : AuthOutcome()
}
