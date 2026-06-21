package cz.cleansia.partner.features.profile

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.partner.core.network.ApiErrorTranslator
import cz.cleansia.core.network.ApiResult
import cz.cleansia.partner.data.profile.ProfileRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import javax.inject.Inject

data class EmergencySectionUiState(
    val isLoading: Boolean = false,
    val isSaving: Boolean = false,
    val employeeId: String = "",
    val name: String = "",
    val phone: String = "",
    val nameError: String? = null,
    val phoneError: String? = null,
    val error: String? = null,
    val isSaved: Boolean = false,
)

@HiltViewModel
class EmergencySectionViewModel @Inject constructor(
    private val profileRepository: ProfileRepository,
    private val errorTranslator: ApiErrorTranslator,
    private val snackbar: SnackbarController,
) : ViewModel() {

    private val _uiState = MutableStateFlow(EmergencySectionUiState())
    val uiState: StateFlow<EmergencySectionUiState> = _uiState.asStateFlow()

    init { load() }

    private fun load() {
        viewModelScope.launch {
            _uiState.update { it.copy(isLoading = true) }
            when (val result = profileRepository.getCurrentEmployee()) {
                is ApiResult.Success -> {
                    val e = result.data
                    _uiState.update {
                        it.copy(
                            isLoading = false,
                            employeeId = e.id.orEmpty(),
                            name = e.emergencyContactName.orEmpty(),
                            phone = e.emergencyContactPhone.orEmpty(),
                        )
                    }
                }
                is ApiResult.Error -> {
                    _uiState.update { it.copy(isLoading = false) }
                    snackbar.showError(errorTranslator.translate(result.error))
                }
            }
        }
    }

    fun onNameChange(v: String) = _uiState.update { it.copy(name = v, nameError = null) }
    fun onPhoneChange(v: String) = _uiState.update { it.copy(phone = v, phoneError = null) }

    fun save() {
        val state = _uiState.value
        var hasError = false
        if (state.name.isBlank()) { _uiState.update { it.copy(nameError = "Name is required") }; hasError = true }
        if (state.phone.isBlank()) { _uiState.update { it.copy(phoneError = "Phone is required") }; hasError = true }
        if (hasError) return
        if (state.employeeId.isBlank()) {
            snackbar.showError("Profile not loaded yet")
            return
        }
        viewModelScope.launch {
            _uiState.update { it.copy(isSaving = true) }
            val result = profileRepository.updateEmergencyContact(
                employeeId = state.employeeId,
                emergencyName = state.name.trim(),
                emergencyPhone = state.phone.trim(),
            )
            when (result) {
                is ApiResult.Success -> _uiState.update { it.copy(isSaving = false, isSaved = true) }
                is ApiResult.Error -> {
                    _uiState.update { it.copy(isSaving = false) }
                    snackbar.showError(errorTranslator.translate(result.error))
                }
            }
        }
    }

    fun clearError() = _uiState.update { it.copy(error = null) }
}
