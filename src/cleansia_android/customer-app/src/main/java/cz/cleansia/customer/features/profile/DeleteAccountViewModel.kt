package cz.cleansia.customer.features.profile

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.core.network.ApiError
import cz.cleansia.customer.R
import cz.cleansia.customer.core.user.UserRepository
import cz.cleansia.customer.ui.state.ActionState
import cz.cleansia.core.snackbar.SnackbarController
import dagger.hilt.android.lifecycle.HiltViewModel
import javax.inject.Inject
import kotlinx.coroutines.flow.MutableSharedFlow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.SharedFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asSharedFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch

@HiltViewModel
class DeleteAccountViewModel @Inject constructor(
    private val userRepository: UserRepository,
    private val snackbar: SnackbarController,
) : ViewModel() {

    private val _deleteState = MutableStateFlow<ActionState>(ActionState.Idle)
    val deleteState: StateFlow<ActionState> = _deleteState.asStateFlow()

    private val _accountDeleted = MutableSharedFlow<Unit>(extraBufferCapacity = 1)
    val accountDeleted: SharedFlow<Unit> = _accountDeleted.asSharedFlow()

    fun deleteAccount() {
        if (_deleteState.value is ActionState.Submitting) return
        _deleteState.value = ActionState.Submitting
        viewModelScope.launch {
            userRepository.deleteAccount()
                .onSuccess {
                    _deleteState.value = ActionState.Idle
                    snackbar.showSuccessKey(R.string.delete_account_success)
                    _accountDeleted.emit(Unit)
                }
                .onError { error ->
                    if (error !is ApiError.Network) snackbar.showError(error.getUserMessage())
                    _deleteState.value = ActionState.Error(error.getUserMessage())
                }
        }
    }
}
