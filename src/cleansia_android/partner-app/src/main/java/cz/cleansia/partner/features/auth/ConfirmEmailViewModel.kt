package cz.cleansia.partner.features.auth

import android.content.Context
import androidx.lifecycle.SavedStateHandle
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.partner.R
import cz.cleansia.partner.core.auth.UserProfileStore
import cz.cleansia.partner.core.network.ApiErrorTranslator
import cz.cleansia.core.network.ApiResult
import cz.cleansia.partner.core.settings.AppSettingsRepository
import cz.cleansia.partner.data.auth.AuthRepository
import cz.cleansia.partner.data.auth.LoginOutcome
import dagger.hilt.android.lifecycle.HiltViewModel
import dagger.hilt.android.qualifiers.ApplicationContext
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import javax.inject.Inject

data class ConfirmEmailUiState(
    val code: String = "",
    val email: String = "",
    val isLoading: Boolean = false,
    val isResending: Boolean = false,
    val error: String? = null,
    val isConfirmationSuccessful: Boolean = false,
    val resendSuccessMessage: String? = null,
)

@HiltViewModel
class ConfirmEmailViewModel @Inject constructor(
    savedStateHandle: SavedStateHandle,
    private val authRepository: AuthRepository,
    private val errorTranslator: ApiErrorTranslator,
    private val userProfileStore: UserProfileStore,
    private val appSettingsRepository: AppSettingsRepository,
    private val snackbar: SnackbarController,
    @ApplicationContext private val context: Context,
) : ViewModel() {

    private val _uiState = MutableStateFlow(ConfirmEmailUiState())
    val uiState: StateFlow<ConfirmEmailUiState> = _uiState.asStateFlow()

    /**
     * The address the code was issued to, carried on the route. Read via
     * [SavedStateHandle] rather than `toRoute<>()` so the ViewModel layer stays
     * free of androidx.navigation, matching OrderDetailViewModel.
     */
    private val routeEmail: String = savedStateHandle.get<String>("email").orEmpty()

    init {
        // The route arg is the primary source: a partner who has just
        // registered has no session, so UserProfileStore is empty for them.
        // The store survives as a fallback for a login path that omitted the
        // address on the wire. Branching (rather than always launching) also
        // means the two sources cannot race to overwrite each other.
        if (routeEmail.isNotBlank()) {
            _uiState.update { it.copy(email = routeEmail) }
        } else {
            viewModelScope.launch {
                // Login persisted the profile even with the email unconfirmed.
                userProfileStore.current()?.let { profile ->
                    _uiState.update { it.copy(email = profile.email) }
                }
            }
        }
    }

    fun onCodeChange(code: String) {
        val filtered = code.filter { it.isDigit() }.take(6)
        _uiState.update { it.copy(code = filtered, error = null) }
    }

    fun confirmEmail() {
        val state = _uiState.value
        if (state.code.length != 6) return
        if (state.email.isBlank()) {
            _uiState.update {
                it.copy(error = context.getString(R.string.error_generic))
            }
            return
        }

        viewModelScope.launch {
            _uiState.update { it.copy(isLoading = true, error = null) }
            when (val result = authRepository.confirmEmail(email = state.email, code = state.code)) {
                is ApiResult.Success -> when (result.data) {
                    // Only a real session counts: the repo maps a 200-without-token to
                    // UnverifiedEmail(hasToken = false), and navigating into the app on
                    // that would strand the user sessionless.
                    is LoginOutcome.Authenticated ->
                        _uiState.update { it.copy(isLoading = false, isConfirmationSuccessful = true) }
                    else -> {
                        snackbar.showError(context.getString(R.string.error_generic))
                        _uiState.update { it.copy(isLoading = false) }
                    }
                }
                is ApiResult.Error -> {
                    snackbar.showError(errorTranslator.translate(result.error))
                    _uiState.update { it.copy(isLoading = false) }
                }
            }
        }
    }

    fun resendCode() {
        val state = _uiState.value
        if (state.email.isBlank()) {
            _uiState.update {
                it.copy(error = context.getString(R.string.error_generic))
            }
            return
        }

        viewModelScope.launch {
            _uiState.update { it.copy(isResending = true, error = null) }
            // Not `settings.first().language.tag ?: "en"` — the default preference is
            // System, whose tag is null, so every re-sent code arrived in English.
            // See AppSettingsRepository.emailLanguageTag.
            val language = appSettingsRepository.emailLanguageTag()
            when (val result = authRepository.resendConfirmation(state.email, language)) {
                is ApiResult.Success -> {
                    snackbar.showSuccess(context.getString(R.string.confirm_email_subtitle))
                    _uiState.update { it.copy(isResending = false) }
                }
                is ApiResult.Error -> {
                    snackbar.showError(errorTranslator.translate(result.error))
                    _uiState.update { it.copy(isResending = false) }
                }
            }
        }
    }

    fun clearError() {
        _uiState.update { it.copy(error = null) }
    }

    fun clearResendSuccessMessage() {
        _uiState.update { it.copy(resendSuccessMessage = null) }
    }
}
