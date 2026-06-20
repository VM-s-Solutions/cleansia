package cz.cleansia.customer.features.profile

import android.content.Context
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.core.network.ApiError
import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.customer.R
import cz.cleansia.customer.core.auth.ApiErrorParser
import cz.cleansia.customer.core.devices.DeviceManagementRepository
import cz.cleansia.customer.core.devices.UserDeviceDto
import cz.cleansia.customer.ui.state.ActionState
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
    private val repository: DeviceManagementRepository,
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
            _state.value = when (val result = repository.getMyDevices()) {
                is ApiResult.Success -> DevicesUiState.Loaded(result.data)
                is ApiResult.Error -> {
                    surfaceError(result.error)
                    DevicesUiState.Error
                }
            }
        }
    }

    fun revoke(deviceRowId: String) {
        if (_revokeState.value is ActionState.Submitting) return
        _revokeState.value = ActionState.Submitting
        viewModelScope.launch {
            when (val result = repository.revoke(deviceRowId)) {
                is ApiResult.Success -> {
                    snackbar.showSuccess(appContext.getString(R.string.devices_revoke_success))
                    _revokeState.value = ActionState.Idle
                    _revoked.emit(deviceRowId)
                    (_state.value as? DevicesUiState.Loaded)?.let { loaded ->
                        _state.value = DevicesUiState.Loaded(loaded.devices.filterNot { it.id == deviceRowId })
                    }
                }
                is ApiResult.Error -> {
                    surfaceError(result.error)
                    _revokeState.value = ActionState.Error(appContext.getString(R.string.devices_revoke_retry_hint))
                }
            }
        }
    }

    private fun surfaceError(error: ApiError) {
        // Infrastructure failures are toasted globally by NetworkErrorInterceptor;
        // surfacing them here would double-toast, so stay silent for those.
        if (error is ApiError.Network) return
        snackbar.showError(ApiErrorParser.parseToUserMessage(appContext, error))
    }
}
