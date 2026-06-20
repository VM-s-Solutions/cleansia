package cz.cleansia.customer.features.profile

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.core.network.ApiError
import cz.cleansia.customer.R
import cz.cleansia.customer.core.user.UserRepository
import cz.cleansia.core.snackbar.SnackbarController
import dagger.hilt.android.lifecycle.HiltViewModel
import javax.inject.Inject
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch

@HiltViewModel
class DeleteAccountViewModel @Inject constructor(
    private val userRepository: UserRepository,
    private val snackbar: SnackbarController,
) : ViewModel() {

    private val _loading = MutableStateFlow(false)
    val loading: StateFlow<Boolean> = _loading.asStateFlow()

    fun deleteAccount() {
        if (_loading.value) return
        _loading.value = true
        viewModelScope.launch {
            val result = userRepository.deleteAccount()
            _loading.value = false
            result
                .onSuccess {
                    // Success — UserRepository emitted ForcedSignOut which navigates us to SignIn.
                    // Show a confirmation snackbar that survives the navigation.
                    snackbar.showSuccessKey(R.string.delete_account_success)
                }
                .onError { error ->
                    if (error !is ApiError.Network) snackbar.showError(error.getUserMessage())
                }
        }
    }
}
