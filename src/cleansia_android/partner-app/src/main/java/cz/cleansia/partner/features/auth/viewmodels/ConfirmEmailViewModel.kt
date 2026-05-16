package cz.cleansia.partner.features.auth.viewmodels

import androidx.lifecycle.SavedStateHandle
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.partner.core.network.ApiError
import cz.cleansia.partner.core.network.ApiErrorTranslator
import cz.cleansia.partner.core.network.ApiResult
import cz.cleansia.partner.domain.repositories.AuthRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import javax.inject.Inject

data class ConfirmEmailUiState(
    val email: String = "",
    val code: String = "",
    val isLoading: Boolean = false,
    val isResending: Boolean = false,
    val error: String? = null,
    val codeError: String? = null,
    val isConfirmationSuccessful: Boolean = false,
    val resendSuccessMessage: String? = null
)

@HiltViewModel
class ConfirmEmailViewModel @Inject constructor(
    savedStateHandle: SavedStateHandle,
    private val authRepository: AuthRepository,
    private val errorTranslator: ApiErrorTranslator
) : ViewModel() {

    private val _uiState = MutableStateFlow(ConfirmEmailUiState())
    val uiState: StateFlow<ConfirmEmailUiState> = _uiState.asStateFlow()

    init {
        // Get email from navigation arguments
        val email = savedStateHandle.get<String>("email") ?: ""
        _uiState.update { it.copy(email = email) }
    }

    fun onCodeChange(code: String) {
        // Only allow digits and limit to 6 characters
        val filteredCode = code.filter { it.isDigit() }.take(6)
        _uiState.update {
            it.copy(
                code = filteredCode,
                codeError = null,
                error = null
            )
        }
    }

    fun confirmEmail() {
        val state = _uiState.value

        // Validate code
        if (state.code.isBlank()) {
            _uiState.update { it.copy(codeError = "Verification code is required") }
            return
        }

        if (state.code.length != 6) {
            _uiState.update { it.copy(codeError = "Please enter a valid 6-digit code") }
            return
        }

        viewModelScope.launch {
            _uiState.update { it.copy(isLoading = true, error = null) }

            when (val result = authRepository.confirmEmail(state.email, state.code)) {
                is ApiResult.Success -> {
                    _uiState.update {
                        it.copy(
                            isLoading = false,
                            isConfirmationSuccessful = true
                        )
                    }
                }

                is ApiResult.Error -> {
                    val errorMessage = when (result.error) {
                        is ApiError.BadRequest -> "Invalid verification code. Please try again."
                        is ApiError.Network -> "Unable to connect. Please check your internet connection."
                        else -> errorTranslator.translateError(result.error)
                    }

                    _uiState.update {
                        it.copy(
                            isLoading = false,
                            error = errorMessage
                        )
                    }
                }
            }
        }
    }

    fun resendCode() {
        val state = _uiState.value

        if (state.email.isBlank()) {
            _uiState.update { it.copy(error = "Email not found. Please go back and try again.") }
            return
        }

        viewModelScope.launch {
            _uiState.update { it.copy(isResending = true, error = null) }

            when (val result = authRepository.resendConfirmationEmail(state.email)) {
                is ApiResult.Success -> {
                    _uiState.update {
                        it.copy(
                            isResending = false,
                            resendSuccessMessage = "A new verification code has been sent to your email."
                        )
                    }
                }

                is ApiResult.Error -> {
                    val errorMessage = when (result.error) {
                        is ApiError.Network -> "Unable to connect. Please check your internet connection."
                        else -> errorTranslator.translateError(result.error)
                    }

                    _uiState.update {
                        it.copy(
                            isResending = false,
                            error = errorMessage
                        )
                    }
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
