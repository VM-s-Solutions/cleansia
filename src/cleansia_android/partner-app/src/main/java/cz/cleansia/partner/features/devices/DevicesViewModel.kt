package cz.cleansia.partner.features.devices

import android.content.Context
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.core.auth.ForcedSignOutReason
import cz.cleansia.core.auth.SessionManager
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.core.ui.state.ActionState
import cz.cleansia.partner.R
import cz.cleansia.partner.core.devices.DevicesRepository
import cz.cleansia.partner.core.devices.UserDeviceDto
import cz.cleansia.partner.core.network.ApiErrorTranslator
import cz.cleansia.partner.data.auth.AuthRepository
import cz.cleansia.core.network.ApiResult
import dagger.hilt.android.lifecycle.HiltViewModel
import dagger.hilt.android.qualifiers.ApplicationContext
import kotlinx.coroutines.flow.MutableSharedFlow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.SharedFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asSharedFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch
import javax.inject.Inject

sealed interface DevicesUiState {
    data object Loading : DevicesUiState
    data object Error : DevicesUiState
    data class Loaded(val devices: List<UserDeviceDto>) : DevicesUiState
}

@HiltViewModel
class DevicesViewModel @Inject constructor(
    private val devicesRepository: DevicesRepository,
    private val authRepository: AuthRepository,
    private val sessionManager: SessionManager,
    private val errorTranslator: ApiErrorTranslator,
    private val snackbar: SnackbarController,
    @ApplicationContext private val appContext: Context,
) : ViewModel() {

    private val _state = MutableStateFlow<DevicesUiState>(DevicesUiState.Loading)
    val state: StateFlow<DevicesUiState> = _state.asStateFlow()

    private val _revokeState = MutableStateFlow<ActionState>(ActionState.Idle)
    val revokeState: StateFlow<ActionState> = _revokeState.asStateFlow()

    private val _revoked = MutableSharedFlow<String>(extraBufferCapacity = 1)
    val revoked: SharedFlow<String> = _revoked.asSharedFlow()

    init {
        load()
    }

    fun load() {
        viewModelScope.launch {
            _state.value = DevicesUiState.Loading
            when (val result = devicesRepository.getMyDevices()) {
                is ApiResult.Success -> _state.value = DevicesUiState.Loaded(result.data)
                is ApiResult.Error -> {
                    snackbar.showError(errorTranslator.translate(result.error))
                    _state.value = DevicesUiState.Error
                }
            }
        }
    }

    fun revoke(device: UserDeviceDto) {
        if (_revokeState.value is ActionState.Submitting) return
        _revokeState.value = ActionState.Submitting
        viewModelScope.launch {
            when (val result = devicesRepository.revoke(device.id)) {
                is ApiResult.Success -> {
                    _revokeState.value = ActionState.Idle
                    _revoked.emit(device.id)
                    if (device.isCurrent) {
                        // Revoking THIS device killed our own session server-side; don't wait for
                        // the ≤30s revocation directory to bounce us — sign out at 0s. Local wipe
                        // ONLY (the server logout would be a redundant round trip against an
                        // already-dead session), then the root-level forced-sign-out observer
                        // routes to Login — that collector never unmounts, so the navigation can't
                        // be lost to a screen teardown. No snackbar: the login screen is the
                        // feedback.
                        authRepository.signOutLocal()
                        sessionManager.emitForcedSignOut(ForcedSignOutReason.UserInitiated)
                    } else {
                        snackbar.showSuccess(appContext.getString(R.string.devices_revoke_success))
                        (_state.value as? DevicesUiState.Loaded)?.let { loaded ->
                            _state.value = DevicesUiState.Loaded(loaded.devices.filterNot { it.id == device.id })
                        }
                    }
                }
                is ApiResult.Error -> {
                    snackbar.showError(errorTranslator.translate(result.error))
                    _revokeState.value = ActionState.Error(appContext.getString(R.string.devices_revoke_retry_hint))
                }
            }
        }
    }
}
