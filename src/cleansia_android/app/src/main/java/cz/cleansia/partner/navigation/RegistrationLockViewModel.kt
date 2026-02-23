package cz.cleansia.partner.navigation

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.partner.core.network.ApiResult
import cz.cleansia.partner.domain.models.profile.RegistrationCompletionStatus
import cz.cleansia.partner.domain.repositories.ProfileRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch
import javax.inject.Inject

@HiltViewModel
class RegistrationLockViewModel @Inject constructor(
    private val profileRepository: ProfileRepository
) : ViewModel() {

    private val _registrationStatus = MutableStateFlow<RegistrationCompletionStatus?>(null)
    val registrationStatus: StateFlow<RegistrationCompletionStatus?> = _registrationStatus.asStateFlow()

    private val _isLoading = MutableStateFlow(true)
    val isLoading: StateFlow<Boolean> = _isLoading.asStateFlow()

    init {
        checkRegistrationStatus()
    }

    fun checkRegistrationStatus() {
        viewModelScope.launch {
            _isLoading.value = true
            when (val result = profileRepository.checkRegistrationStatus()) {
                is ApiResult.Success -> {
                    _registrationStatus.value = result.data
                }
                is ApiResult.Error -> {
                    // On error, assume complete to avoid blocking the user
                    _registrationStatus.value = RegistrationCompletionStatus(
                        areDocumentsUploaded = true,
                        hasCompletedProfile = true
                    )
                }
            }
            _isLoading.value = false
        }
    }

    fun refresh() {
        checkRegistrationStatus()
    }
}
