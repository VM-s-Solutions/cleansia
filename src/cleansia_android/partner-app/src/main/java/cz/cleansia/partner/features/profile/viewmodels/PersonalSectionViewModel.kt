package cz.cleansia.partner.features.profile.viewmodels

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

data class PersonalSectionUiState(
    val isLoading: Boolean = false,
    val isSaving: Boolean = false,
    val employeeId: String = "",
    val firstName: String = "",
    val lastName: String = "",
    val birthDate: String = "",
    val phone: String = "",
    val email: String = "",
    val firstNameError: String? = null,
    val lastNameError: String? = null,
    val error: String? = null,
    val isSaved: Boolean = false,
)

@HiltViewModel
class PersonalSectionViewModel @Inject constructor(
    private val profileRepository: ProfileRepository,
    private val errorTranslator: ApiErrorTranslator,
    private val snackbar: SnackbarController,
) : ViewModel() {

    private val _uiState = MutableStateFlow(PersonalSectionUiState())
    val uiState: StateFlow<PersonalSectionUiState> = _uiState.asStateFlow()

    init {
        load()
    }

    private fun load() {
        viewModelScope.launch {
            _uiState.update { it.copy(isLoading = true, error = null) }
            when (val result = profileRepository.getCurrentEmployee()) {
                is ApiResult.Success -> {
                    val e = result.data
                    _uiState.update {
                        it.copy(
                            isLoading = false,
                            employeeId = e.id.orEmpty(),
                            firstName = e.firstName.orEmpty(),
                            lastName = e.lastName.orEmpty(),
                            birthDate = e.birthDate.orEmpty(),
                            phone = e.phoneNumber.orEmpty(),
                            email = e.email.orEmpty(),
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

    fun onFirstNameChange(v: String) = _uiState.update { it.copy(firstName = v, firstNameError = null, error = null) }
    fun onLastNameChange(v: String) = _uiState.update { it.copy(lastName = v, lastNameError = null, error = null) }
    fun onBirthDateChange(v: String) = _uiState.update { it.copy(birthDate = v, error = null) }
    fun onPhoneChange(v: String) = _uiState.update { it.copy(phone = v, error = null) }
    fun onEmailChange(v: String) = _uiState.update { it.copy(email = v, error = null) }

    fun save() {
        val state = _uiState.value
        var hasError = false
        if (state.firstName.isBlank()) {
            _uiState.update { it.copy(firstNameError = "First name is required") }
            hasError = true
        }
        if (state.lastName.isBlank()) {
            _uiState.update { it.copy(lastNameError = "Last name is required") }
            hasError = true
        }
        if (hasError) return
        if (state.employeeId.isBlank()) {
            snackbar.showError("Profile not loaded yet")
            return
        }

        viewModelScope.launch {
            _uiState.update { it.copy(isSaving = true, error = null) }
            val result = profileRepository.updatePersonalInfo(
                employeeId = state.employeeId,
                firstName = state.firstName.trim(),
                lastName = state.lastName.trim(),
                birthDate = state.birthDate.takeIf { it.isNotBlank() },
                phone = state.phone.takeIf { it.isNotBlank() },
                email = state.email.takeIf { it.isNotBlank() },
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
