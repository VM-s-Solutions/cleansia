package cz.cleansia.partner.features.auth

import android.content.Context
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.partner.R
import cz.cleansia.partner.core.auth.UserProfileStore
import cz.cleansia.partner.core.network.ApiErrorTranslator
import cz.cleansia.core.network.ApiResult
import cz.cleansia.partner.core.settings.AppSettingsRepository
import cz.cleansia.partner.data.auth.AuthRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import dagger.hilt.android.qualifiers.ApplicationContext
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.first
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
    private val authRepository: AuthRepository,
    private val errorTranslator: ApiErrorTranslator,
    private val userProfileStore: UserProfileStore,
    private val appSettingsRepository: AppSettingsRepository,
    private val snackbar: SnackbarController,
    @ApplicationContext private val context: Context,
) : ViewModel() {

    private val _uiState = MutableStateFlow(ConfirmEmailUiState())
    val uiState: StateFlow<ConfirmEmailUiState> = _uiState.asStateFlow()

    init {
        viewModelScope.launch {
            // Email comes from the just-stored profile (login persisted it
            // even when the email wasn't yet confirmed). Used only for the
            // resend call + screen subtitle, not for the confirm call itself
            // — the confirm endpoint matches on the active session.
            userProfileStore.current()?.let { profile ->
                _uiState.update { it.copy(email = profile.email) }
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

        viewModelScope.launch {
            _uiState.update { it.copy(isLoading = true, error = null) }
            when (val result = authRepository.confirmEmail(state.code)) {
                is ApiResult.Success -> {
                    _uiState.update { it.copy(isLoading = false, isConfirmationSuccessful = true) }
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
            val language = appSettingsRepository.settings.first().language.tag ?: "en"
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
