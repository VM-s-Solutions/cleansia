package cz.cleansia.partner.features.profile.viewmodels

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.partner.api.model.ContractStatus
import cz.cleansia.partner.api.model.EmployeeItem
import cz.cleansia.partner.core.network.ApiErrorTranslator
import cz.cleansia.partner.core.network.ApiResult
import cz.cleansia.partner.data.auth.AuthRepository
import cz.cleansia.partner.data.profile.ProfileRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import javax.inject.Inject

/**
 * Drives the profile overview screen. Owns the loaded [EmployeeItem] and
 * exposes a refresh hook that section editors can call after they save —
 * so when the user closes an editor and returns to the overview, the
 * displayed values reflect the change without a manual reload.
 */
data class ProfileUiState(
    val isLoading: Boolean = false,
    val employee: EmployeeItem? = null,
    // Contract status is read from getRegistrationStatus() — the
    // EmployeeItem DTO doesn't carry it (intentional, partners only
    // see fields they can edit). Surfaced here so the profile hero
    // can render the read-only status chip.
    val contractStatus: ContractStatus? = null,
    val error: String? = null,
    val isSignedOut: Boolean = false,
)

@HiltViewModel
class ProfileViewModel @Inject constructor(
    private val profileRepository: ProfileRepository,
    private val authRepository: AuthRepository,
    private val errorTranslator: ApiErrorTranslator,
    private val snackbar: SnackbarController,
) : ViewModel() {

    private val _uiState = MutableStateFlow(ProfileUiState())
    val uiState: StateFlow<ProfileUiState> = _uiState.asStateFlow()

    init {
        refresh()
    }

    fun refresh() {
        viewModelScope.launch {
            _uiState.update { it.copy(isLoading = true, error = null) }
            when (val result = profileRepository.getCurrentEmployee()) {
                is ApiResult.Success -> {
                    _uiState.update { it.copy(isLoading = false, employee = result.data) }
                }
                is ApiResult.Error -> {
                    snackbar.showError(errorTranslator.translate(result.error))
                    _uiState.update { it.copy(isLoading = false) }
                }
            }
            // Status is fire-and-forget — failing to load it shouldn't
            // block the rest of the profile, so we don't surface its
            // error to the snackbar. The chip just stays hidden.
            (profileRepository.getRegistrationStatus() as? ApiResult.Success)?.let { status ->
                _uiState.update { it.copy(contractStatus = status.data.contractStatus) }
            }
        }
    }

    fun signOut() {
        viewModelScope.launch {
            authRepository.logout()
            _uiState.update { it.copy(isSignedOut = true) }
        }
    }

    fun clearError() {
        _uiState.update { it.copy(error = null) }
    }
}
