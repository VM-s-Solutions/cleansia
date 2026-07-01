package cz.cleansia.partner.features.profile

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.partner.api.model.ContractStatus
import cz.cleansia.partner.api.model.EmployeeItem
import cz.cleansia.partner.core.network.ApiErrorTranslator
import cz.cleansia.core.network.ApiResult
import cz.cleansia.partner.data.auth.AuthRepository
import cz.cleansia.partner.data.profile.ProfileRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableSharedFlow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.SharedFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asSharedFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch
import javax.inject.Inject

/**
 * Drives the profile overview screen. Owns the loaded [EmployeeItem] and
 * exposes a refresh hook that section editors can call after they save —
 * so when the user closes an editor and returns to the overview, the
 * displayed values reflect the change without a manual reload.
 *
 * Contract status is read from getRegistrationStatus() — the
 * EmployeeItem DTO doesn't carry it (intentional, partners only see
 * fields they can edit). Surfaced on [Loaded] so the profile hero can
 * render the read-only status chip.
 */
sealed interface ProfileUiState {
    data object Loading : ProfileUiState
    data object Error : ProfileUiState
    data class Loaded(
        val employee: EmployeeItem,
        val contractStatus: ContractStatus? = null,
    ) : ProfileUiState
}

@HiltViewModel
class ProfileViewModel @Inject constructor(
    private val profileRepository: ProfileRepository,
    private val authRepository: AuthRepository,
    private val errorTranslator: ApiErrorTranslator,
    private val snackbar: SnackbarController,
) : ViewModel() {

    private val _uiState = MutableStateFlow<ProfileUiState>(ProfileUiState.Loading)
    val uiState: StateFlow<ProfileUiState> = _uiState.asStateFlow()

    private val _signedOut = MutableSharedFlow<Unit>(extraBufferCapacity = 1)
    val signedOut: SharedFlow<Unit> = _signedOut.asSharedFlow()

    init {
        refresh()
    }

    fun refresh() {
        viewModelScope.launch {
            if (_uiState.value !is ProfileUiState.Loaded) _uiState.value = ProfileUiState.Loading
            when (val result = profileRepository.getCurrentEmployee()) {
                is ApiResult.Success -> {
                    _uiState.value = ProfileUiState.Loaded(employee = result.data)
                }
                is ApiResult.Error -> {
                    snackbar.showError(errorTranslator.translate(result.error))
                    if (_uiState.value !is ProfileUiState.Loaded) _uiState.value = ProfileUiState.Error
                }
            }
            // Status is fire-and-forget — failing to load it shouldn't
            // block the rest of the profile, so we don't surface its
            // error to the snackbar. The chip just stays hidden.
            val status = (profileRepository.getRegistrationStatus() as? ApiResult.Success)?.data
            (_uiState.value as? ProfileUiState.Loaded)?.let { loaded ->
                _uiState.value = loaded.copy(contractStatus = status?.contractStatus ?: loaded.contractStatus)
            }
        }
    }

    fun signOut() {
        viewModelScope.launch {
            authRepository.logout()
            _signedOut.emit(Unit)
        }
    }
}
