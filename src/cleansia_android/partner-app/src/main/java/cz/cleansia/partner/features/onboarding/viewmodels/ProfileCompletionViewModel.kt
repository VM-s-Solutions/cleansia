package cz.cleansia.partner.features.onboarding.viewmodels

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.partner.core.network.ApiErrorTranslator
import cz.cleansia.partner.core.network.ApiResult
import cz.cleansia.partner.core.storage.PreferencesManager
import cz.cleansia.partner.domain.models.profile.Country
import cz.cleansia.partner.domain.models.profile.DayAvailability
import cz.cleansia.partner.domain.models.profile.EmployeeProfile
import cz.cleansia.partner.domain.repositories.ProfileRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import javax.inject.Inject

enum class CompletionStep(val index: Int, val title: String) {
    PERSONAL(0, "Personal"),
    ADDRESS(1, "Address"),
    BANK(2, "Banking"),
    AVAILABILITY(3, "Schedule")
}

data class ProfileCompletionUiState(
    val isLoading: Boolean = true,
    val isSaving: Boolean = false,
    val error: String? = null,
    val currentStep: CompletionStep = CompletionStep.PERSONAL,
    val profile: EmployeeProfile? = null,
    val countries: List<Country> = emptyList(),
    val currentLanguage: String = "en",
    // Personal info
    val firstName: String = "",
    val lastName: String = "",
    val phoneNumber: String = "",
    val dateOfBirth: String = "",
    // Identification
    val nationalityId: String = "",
    val passportId: String = "",
    val taxId: String = "",
    // Address
    val street: String = "",
    val city: String = "",
    val zipCode: String = "",
    val state: String = "",
    val countryId: String = "",
    // Bank
    val iban: String = "",
    // Emergency contact
    val emergencyContactName: String = "",
    val emergencyContactPhone: String = "",
    // Availability
    val availability: List<DayAvailability> = emptyList(),
    // Completion tracking
    val completedSteps: Set<CompletionStep> = emptySet()
) {
    val isPersonalComplete: Boolean
        get() = firstName.isNotBlank() && lastName.isNotBlank() && phoneNumber.isNotBlank()

    val isAddressComplete: Boolean
        get() = street.isNotBlank() && city.isNotBlank() && zipCode.isNotBlank()

    val isBankComplete: Boolean
        get() = iban.isNotBlank()

    val canFinish: Boolean
        get() = isPersonalComplete && isAddressComplete && isBankComplete
}

@HiltViewModel
class ProfileCompletionViewModel @Inject constructor(
    private val profileRepository: ProfileRepository,
    private val preferencesManager: PreferencesManager,
    private val errorTranslator: ApiErrorTranslator
) : ViewModel() {

    private val _uiState = MutableStateFlow(ProfileCompletionUiState())
    val uiState: StateFlow<ProfileCompletionUiState> = _uiState.asStateFlow()

    init {
        loadProfile()
    }

    private fun loadProfile() {
        viewModelScope.launch {
            _uiState.update { it.copy(isLoading = true) }

            // Load countries
            when (val countriesResult = profileRepository.getCountries()) {
                is ApiResult.Success -> {
                    _uiState.update { it.copy(countries = countriesResult.data) }
                }
                is ApiResult.Error -> { /* Countries are optional, continue */ }
            }

            // Detect language
            val lang = java.util.Locale.getDefault().language
            _uiState.update { it.copy(currentLanguage = lang) }

            when (val result = profileRepository.getCurrentEmployee()) {
                is ApiResult.Success -> {
                    val profile = result.data
                    _uiState.update {
                        it.copy(
                            isLoading = false,
                            profile = profile,
                            firstName = profile.firstName ?: "",
                            lastName = profile.lastName ?: "",
                            phoneNumber = profile.phoneNumber ?: "",
                            dateOfBirth = profile.dateOfBirth ?: "",
                            nationalityId = profile.nationalityId ?: "",
                            passportId = profile.passportId ?: "",
                            taxId = profile.taxId ?: "",
                            street = profile.street ?: "",
                            city = profile.city ?: "",
                            zipCode = profile.zipCode ?: "",
                            state = profile.state ?: "",
                            countryId = profile.countryId ?: "",
                            iban = profile.iban ?: "",
                            emergencyContactName = profile.emergencyContactName ?: "",
                            emergencyContactPhone = profile.emergencyContactPhone ?: ""
                        )
                    }
                }
                is ApiResult.Error -> {
                    _uiState.update {
                        it.copy(isLoading = false, error = errorTranslator.translateError(result.error))
                    }
                }
            }
        }
    }

    fun setStep(step: CompletionStep) {
        _uiState.update { it.copy(currentStep = step) }
    }

    fun nextStep() {
        val current = _uiState.value.currentStep
        val next = CompletionStep.entries.getOrNull(current.index + 1)
        if (next != null) {
            _uiState.update {
                it.copy(
                    currentStep = next,
                    completedSteps = it.completedSteps + current
                )
            }
        }
    }

    fun previousStep() {
        val current = _uiState.value.currentStep
        val prev = CompletionStep.entries.getOrNull(current.index - 1)
        if (prev != null) {
            _uiState.update { it.copy(currentStep = prev) }
        }
    }

    // Personal info updates
    fun updateFirstName(value: String) { _uiState.update { it.copy(firstName = value) } }
    fun updateLastName(value: String) { _uiState.update { it.copy(lastName = value) } }
    fun updatePhoneNumber(value: String) { _uiState.update { it.copy(phoneNumber = value) } }
    fun updateDateOfBirth(value: String) { _uiState.update { it.copy(dateOfBirth = value) } }

    // Identification updates
    fun updateNationalityId(value: String) { _uiState.update { it.copy(nationalityId = value) } }
    fun updatePassportId(value: String) { _uiState.update { it.copy(passportId = value) } }
    fun updateTaxId(value: String) { _uiState.update { it.copy(taxId = value) } }

    // Address updates
    fun updateStreet(value: String) { _uiState.update { it.copy(street = value) } }
    fun updateCity(value: String) { _uiState.update { it.copy(city = value) } }
    fun updateZipCode(value: String) { _uiState.update { it.copy(zipCode = value) } }
    fun updateState(value: String) { _uiState.update { it.copy(state = value) } }
    fun updateCountryId(value: String) { _uiState.update { it.copy(countryId = value) } }

    // Bank updates
    fun updateIban(value: String) { _uiState.update { it.copy(iban = value) } }

    // Emergency contact updates
    fun updateEmergencyContactName(value: String) { _uiState.update { it.copy(emergencyContactName = value) } }
    fun updateEmergencyContactPhone(value: String) { _uiState.update { it.copy(emergencyContactPhone = value) } }

    // Availability updates
    fun updateAvailability(availability: List<DayAvailability>) {
        _uiState.update { it.copy(availability = availability) }
    }

    fun clearError() {
        _uiState.update { it.copy(error = null) }
    }

    fun finishProfile() {
        val state = _uiState.value
        val profile = state.profile ?: return

        viewModelScope.launch {
            _uiState.update { it.copy(isSaving = true, error = null) }

            val updatedProfile = profile.copy(
                firstName = state.firstName,
                lastName = state.lastName,
                phoneNumber = state.phoneNumber,
                dateOfBirth = state.dateOfBirth.ifBlank { null },
                nationalityId = state.nationalityId.ifBlank { null },
                passportId = state.passportId.ifBlank { null },
                taxId = state.taxId.ifBlank { null },
                street = state.street,
                city = state.city,
                zipCode = state.zipCode,
                state = state.state.ifBlank { null },
                countryId = state.countryId.ifBlank { null },
                iban = state.iban,
                emergencyContactName = state.emergencyContactName.ifBlank { null },
                emergencyContactPhone = state.emergencyContactPhone.ifBlank { null }
            )

            when (val result = profileRepository.updateEmployee(updatedProfile)) {
                is ApiResult.Success -> {
                    preferencesManager.setProfileCompleted(true)
                    _uiState.update { it.copy(isSaving = false) }
                }
                is ApiResult.Error -> {
                    _uiState.update {
                        it.copy(isSaving = false, error = errorTranslator.translateError(result.error))
                    }
                }
            }
        }
    }

    fun skipForNow() {
        viewModelScope.launch {
            preferencesManager.setProfileCompleted(true)
        }
    }
}
