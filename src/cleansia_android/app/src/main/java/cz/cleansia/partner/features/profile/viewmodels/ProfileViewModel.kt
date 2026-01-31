package cz.cleansia.partner.features.profile.viewmodels

import androidx.appcompat.app.AppCompatDelegate
import androidx.core.os.LocaleListCompat
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.partner.core.network.ApiResult
import cz.cleansia.partner.core.storage.PreferencesManager
import cz.cleansia.partner.core.storage.TokenManager
import cz.cleansia.partner.domain.models.profile.DayAvailability
import cz.cleansia.partner.domain.models.profile.EmployeeDocument
import cz.cleansia.partner.domain.models.profile.EmployeeProfile
import cz.cleansia.partner.domain.repositories.AuthRepository
import cz.cleansia.partner.domain.repositories.ProfileRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.SharingStarted
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.stateIn
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import javax.inject.Inject

enum class ProfileSection {
    PERSONAL, IDENTIFICATION, ADDRESS, BANK, EMERGENCY
}

data class ProfileUiState(
    val isLoading: Boolean = false,
    val isRefreshing: Boolean = false,
    val isSaving: Boolean = false,
    val savingSection: ProfileSection? = null,
    val isUploadingDocument: Boolean = false,
    val isDeletingDocument: Boolean = false,
    val isSavingAvailability: Boolean = false,
    val error: String? = null,
    val saveSuccess: Boolean = false,
    val uploadSuccess: Boolean = false,
    val deleteSuccess: Boolean = false,
    val availabilitySaveSuccess: Boolean = false,
    val profile: EmployeeProfile? = null,
    val documents: List<EmployeeDocument> = emptyList(),
    val availability: List<DayAvailability> = emptyList(),
    val isLoggingOut: Boolean = false,
    val logoutSuccess: Boolean = false,
    val editingSections: Set<ProfileSection> = emptySet(),
    val editFormState: ProfileEditFormState = ProfileEditFormState()
) {
    // Keep backward compatibility
    val isEditMode: Boolean get() = editingSections.isNotEmpty()
}

/**
 * Form state for editing profile
 */
data class ProfileEditFormState(
    val firstName: String = "",
    val lastName: String = "",
    val phoneNumber: String = "",
    val dateOfBirth: String = "",
    // Additional personal info
    val nationality: String = "",
    val nationalId: String = "",
    val passportId: String = "",
    val taxId: String = "",
    // Address
    val street: String = "",
    val city: String = "",
    val zipCode: String = "",
    val country: String = "",
    val countryId: String = "",
    // Bank
    val iban: String = "",
    // Emergency contact
    val emergencyContactName: String = "",
    val emergencyContactRelationship: String = "",
    val emergencyContactPhone: String = "",
    val emergencyContactEmail: String = ""
) {
    companion object {
        fun fromProfile(profile: EmployeeProfile?): ProfileEditFormState {
            return profile?.let {
                ProfileEditFormState(
                    firstName = it.firstName ?: "",
                    lastName = it.lastName ?: "",
                    phoneNumber = it.phoneNumber ?: "",
                    dateOfBirth = it.dateOfBirth ?: "",
                    nationality = it.nationality ?: "",
                    nationalId = it.nationalId ?: "",
                    passportId = it.passportId ?: "",
                    taxId = it.taxId ?: "",
                    street = it.street ?: "",
                    city = it.city ?: "",
                    zipCode = it.zipCode ?: "",
                    country = it.country ?: "",
                    countryId = it.countryId ?: "",
                    iban = it.iban ?: "",
                    emergencyContactName = it.emergencyContactName ?: "",
                    emergencyContactRelationship = it.emergencyContactRelationship ?: "",
                    emergencyContactPhone = it.emergencyContactPhone ?: "",
                    emergencyContactEmail = it.emergencyContactEmail ?: ""
                )
            } ?: ProfileEditFormState()
        }
    }
}

@HiltViewModel
class ProfileViewModel @Inject constructor(
    private val profileRepository: ProfileRepository,
    private val authRepository: AuthRepository,
    private val preferencesManager: PreferencesManager,
    private val tokenManager: TokenManager
) : ViewModel() {

    private val _uiState = MutableStateFlow(ProfileUiState())
    val uiState: StateFlow<ProfileUiState> = _uiState.asStateFlow()

    // Settings state from DataStore
    val currentLanguage: StateFlow<String> = preferencesManager.language
        .stateIn(viewModelScope, SharingStarted.WhileSubscribed(5000), "cs")

    val currentTheme: StateFlow<String> = preferencesManager.theme
        .stateIn(viewModelScope, SharingStarted.WhileSubscribed(5000), "system")

    val notificationsEnabled: StateFlow<Boolean> = preferencesManager.notificationEnabled
        .stateIn(viewModelScope, SharingStarted.WhileSubscribed(5000), true)

    val biometricEnabled: StateFlow<Boolean> = preferencesManager.biometricEnabled
        .stateIn(viewModelScope, SharingStarted.WhileSubscribed(5000), false)

    init {
        loadProfile()
    }

    fun loadProfile() {
        viewModelScope.launch {
            _uiState.update { it.copy(isLoading = true, error = null) }

            // Load profile
            when (val profileResult = profileRepository.getCurrentEmployee()) {
                is ApiResult.Success -> {
                    _uiState.update { it.copy(profile = profileResult.data) }
                    tokenManager.saveUserName(profileResult.data.firstName, profileResult.data.lastName)
                }
                is ApiResult.Error -> {
                    _uiState.update { it.copy(error = profileResult.error.getUserMessage()) }
                }
            }

            // Load documents
            when (val docsResult = profileRepository.getMyDocuments()) {
                is ApiResult.Success -> {
                    _uiState.update { it.copy(documents = docsResult.data) }
                }
                is ApiResult.Error -> {
                    // Don't override main error
                }
            }

            _uiState.update { it.copy(isLoading = false) }
        }
    }

    fun refresh() {
        viewModelScope.launch {
            _uiState.update { it.copy(isRefreshing = true, error = null) }

            // Load profile
            when (val profileResult = profileRepository.getCurrentEmployee()) {
                is ApiResult.Success -> {
                    _uiState.update { it.copy(profile = profileResult.data) }
                }
                is ApiResult.Error -> {
                    _uiState.update { it.copy(error = profileResult.error.getUserMessage()) }
                }
            }

            // Load documents
            when (val docsResult = profileRepository.getMyDocuments()) {
                is ApiResult.Success -> {
                    _uiState.update { it.copy(documents = docsResult.data) }
                }
                is ApiResult.Error -> { }
            }

            _uiState.update { it.copy(isRefreshing = false) }
        }
    }

    fun logout() {
        viewModelScope.launch {
            _uiState.update { it.copy(isLoggingOut = true) }
            authRepository.logout()
            _uiState.update { it.copy(isLoggingOut = false, logoutSuccess = true) }
        }
    }

    fun clearError() {
        _uiState.update { it.copy(error = null) }
    }

    fun clearSaveSuccess() {
        _uiState.update { it.copy(saveSuccess = false) }
    }

    fun clearUploadSuccess() {
        _uiState.update { it.copy(uploadSuccess = false) }
    }

    fun clearDeleteSuccess() {
        _uiState.update { it.copy(deleteSuccess = false) }
    }

    // Per-section edit methods
    fun startEditingSection(section: ProfileSection) {
        val formState = ProfileEditFormState.fromProfile(_uiState.value.profile)
        _uiState.update {
            it.copy(
                editingSections = it.editingSections + section,
                editFormState = formState
            )
        }
    }

    fun cancelEditingSection(section: ProfileSection) {
        _uiState.update {
            it.copy(editingSections = it.editingSections - section)
        }
    }

    fun saveSection(section: ProfileSection) {
        val currentProfile = _uiState.value.profile ?: return
        val formState = _uiState.value.editFormState

        val updatedProfile = when (section) {
            ProfileSection.PERSONAL -> currentProfile.copy(
                firstName = formState.firstName.takeIf { it.isNotBlank() },
                lastName = formState.lastName.takeIf { it.isNotBlank() },
                phoneNumber = formState.phoneNumber.takeIf { it.isNotBlank() },
                dateOfBirth = formState.dateOfBirth.takeIf { it.isNotBlank() }
            )
            ProfileSection.IDENTIFICATION -> currentProfile.copy(
                nationality = formState.nationality.takeIf { it.isNotBlank() },
                nationalId = formState.nationalId.takeIf { it.isNotBlank() },
                passportId = formState.passportId.takeIf { it.isNotBlank() },
                taxId = formState.taxId.takeIf { it.isNotBlank() }
            )
            ProfileSection.ADDRESS -> currentProfile.copy(
                street = formState.street.takeIf { it.isNotBlank() },
                city = formState.city.takeIf { it.isNotBlank() },
                zipCode = formState.zipCode.takeIf { it.isNotBlank() },
                country = formState.country.takeIf { it.isNotBlank() },
                countryId = formState.countryId.takeIf { it.isNotBlank() }
            )
            ProfileSection.BANK -> currentProfile.copy(
                iban = formState.iban.takeIf { it.isNotBlank() }
            )
            ProfileSection.EMERGENCY -> currentProfile.copy(
                emergencyContactName = formState.emergencyContactName.takeIf { it.isNotBlank() },
                emergencyContactRelationship = formState.emergencyContactRelationship.takeIf { it.isNotBlank() },
                emergencyContactPhone = formState.emergencyContactPhone.takeIf { it.isNotBlank() },
                emergencyContactEmail = formState.emergencyContactEmail.takeIf { it.isNotBlank() }
            )
        }

        viewModelScope.launch {
            _uiState.update { it.copy(savingSection = section, error = null) }

            when (val result = profileRepository.updateEmployee(updatedProfile)) {
                is ApiResult.Success -> {
                    _uiState.update {
                        it.copy(
                            savingSection = null,
                            editingSections = it.editingSections - section,
                            profile = result.data,
                            saveSuccess = true
                        )
                    }
                    tokenManager.saveUserName(result.data.firstName, result.data.lastName)
                }
                is ApiResult.Error -> {
                    _uiState.update {
                        it.copy(
                            savingSection = null,
                            error = result.error.getUserMessage()
                        )
                    }
                }
            }
        }
    }

    // Legacy methods for backward compatibility
    fun enterEditMode() {
        val formState = ProfileEditFormState.fromProfile(_uiState.value.profile)
        _uiState.update {
            it.copy(
                editingSections = setOf(
                    ProfileSection.PERSONAL, ProfileSection.IDENTIFICATION,
                    ProfileSection.ADDRESS, ProfileSection.BANK, ProfileSection.EMERGENCY
                ),
                editFormState = formState
            )
        }
    }

    fun exitEditMode() {
        _uiState.update { it.copy(editingSections = emptySet()) }
    }

    // Form field update methods
    fun updateFirstName(value: String) {
        _uiState.update { it.copy(editFormState = it.editFormState.copy(firstName = value)) }
    }

    fun updateLastName(value: String) {
        _uiState.update { it.copy(editFormState = it.editFormState.copy(lastName = value)) }
    }

    fun updatePhoneNumber(value: String) {
        _uiState.update { it.copy(editFormState = it.editFormState.copy(phoneNumber = value)) }
    }

    fun updateDateOfBirth(value: String) {
        _uiState.update { it.copy(editFormState = it.editFormState.copy(dateOfBirth = value)) }
    }

    fun updateStreet(value: String) {
        _uiState.update { it.copy(editFormState = it.editFormState.copy(street = value)) }
    }

    fun updateCity(value: String) {
        _uiState.update { it.copy(editFormState = it.editFormState.copy(city = value)) }
    }

    fun updateZipCode(value: String) {
        _uiState.update { it.copy(editFormState = it.editFormState.copy(zipCode = value)) }
    }

    fun updateCountry(value: String) {
        _uiState.update { it.copy(editFormState = it.editFormState.copy(country = value)) }
    }

    fun updateIban(value: String) {
        _uiState.update { it.copy(editFormState = it.editFormState.copy(iban = value)) }
    }

    // Additional personal info
    fun updateNationality(value: String) {
        _uiState.update { it.copy(editFormState = it.editFormState.copy(nationality = value)) }
    }

    fun updateNationalId(value: String) {
        _uiState.update { it.copy(editFormState = it.editFormState.copy(nationalId = value)) }
    }

    fun updatePassportId(value: String) {
        _uiState.update { it.copy(editFormState = it.editFormState.copy(passportId = value)) }
    }

    fun updateTaxId(value: String) {
        _uiState.update { it.copy(editFormState = it.editFormState.copy(taxId = value)) }
    }

    fun updateCountryId(value: String) {
        _uiState.update { it.copy(editFormState = it.editFormState.copy(countryId = value)) }
    }

    // Emergency contact
    fun updateEmergencyContactName(value: String) {
        _uiState.update { it.copy(editFormState = it.editFormState.copy(emergencyContactName = value)) }
    }

    fun updateEmergencyContactRelationship(value: String) {
        _uiState.update { it.copy(editFormState = it.editFormState.copy(emergencyContactRelationship = value)) }
    }

    fun updateEmergencyContactPhone(value: String) {
        _uiState.update { it.copy(editFormState = it.editFormState.copy(emergencyContactPhone = value)) }
    }

    fun updateEmergencyContactEmail(value: String) {
        _uiState.update { it.copy(editFormState = it.editFormState.copy(emergencyContactEmail = value)) }
    }

    fun saveProfile() {
        val currentProfile = _uiState.value.profile ?: return
        val formState = _uiState.value.editFormState

        val updatedProfile = currentProfile.copy(
            firstName = formState.firstName.takeIf { it.isNotBlank() },
            lastName = formState.lastName.takeIf { it.isNotBlank() },
            phoneNumber = formState.phoneNumber.takeIf { it.isNotBlank() },
            dateOfBirth = formState.dateOfBirth.takeIf { it.isNotBlank() },
            nationality = formState.nationality.takeIf { it.isNotBlank() },
            nationalId = formState.nationalId.takeIf { it.isNotBlank() },
            passportId = formState.passportId.takeIf { it.isNotBlank() },
            taxId = formState.taxId.takeIf { it.isNotBlank() },
            street = formState.street.takeIf { it.isNotBlank() },
            city = formState.city.takeIf { it.isNotBlank() },
            zipCode = formState.zipCode.takeIf { it.isNotBlank() },
            country = formState.country.takeIf { it.isNotBlank() },
            countryId = formState.countryId.takeIf { it.isNotBlank() },
            iban = formState.iban.takeIf { it.isNotBlank() },
            emergencyContactName = formState.emergencyContactName.takeIf { it.isNotBlank() },
            emergencyContactRelationship = formState.emergencyContactRelationship.takeIf { it.isNotBlank() },
            emergencyContactPhone = formState.emergencyContactPhone.takeIf { it.isNotBlank() },
            emergencyContactEmail = formState.emergencyContactEmail.takeIf { it.isNotBlank() }
        )

        viewModelScope.launch {
            _uiState.update { it.copy(isSaving = true, error = null) }

            when (val result = profileRepository.updateEmployee(updatedProfile)) {
                is ApiResult.Success -> {
                    _uiState.update {
                        it.copy(
                            isSaving = false,
                            editingSections = emptySet(),
                            profile = result.data,
                            saveSuccess = true
                        )
                    }
                }
                is ApiResult.Error -> {
                    _uiState.update {
                        it.copy(
                            isSaving = false,
                            error = result.error.getUserMessage()
                        )
                    }
                }
            }
        }
    }

    // Document management methods
    fun uploadDocument(data: ByteArray, fileName: String) {
        viewModelScope.launch {
            _uiState.update { it.copy(isUploadingDocument = true, error = null) }

            when (val result = profileRepository.saveDocuments(listOf(data to fileName))) {
                is ApiResult.Success -> {
                    _uiState.update {
                        it.copy(
                            isUploadingDocument = false,
                            documents = it.documents + result.data,
                            uploadSuccess = true
                        )
                    }
                }
                is ApiResult.Error -> {
                    _uiState.update {
                        it.copy(
                            isUploadingDocument = false,
                            error = result.error.getUserMessage()
                        )
                    }
                }
            }
        }
    }

    fun deleteDocument(documentId: String) {
        viewModelScope.launch {
            _uiState.update { it.copy(isDeletingDocument = true, error = null) }

            when (val result = profileRepository.deleteDocument(documentId)) {
                is ApiResult.Success -> {
                    _uiState.update {
                        it.copy(
                            isDeletingDocument = false,
                            documents = it.documents.filter { doc -> doc.id != documentId },
                            deleteSuccess = true
                        )
                    }
                }
                is ApiResult.Error -> {
                    _uiState.update {
                        it.copy(
                            isDeletingDocument = false,
                            error = result.error.getUserMessage()
                        )
                    }
                }
            }
        }
    }

    // Availability methods
    fun updateAvailability(availability: List<DayAvailability>) {
        _uiState.update { it.copy(availability = availability) }
    }

    fun saveAvailability() {
        val availability = _uiState.value.availability
        viewModelScope.launch {
            _uiState.update { it.copy(isSavingAvailability = true, error = null) }

            // Note: API endpoint for saving availability would need to be implemented
            // For now, we simulate a successful save
            kotlinx.coroutines.delay(500)
            _uiState.update {
                it.copy(
                    isSavingAvailability = false,
                    availabilitySaveSuccess = true
                )
            }
        }
    }

    fun clearAvailabilitySaveSuccess() {
        _uiState.update { it.copy(availabilitySaveSuccess = false) }
    }

    // Settings methods
    fun setLanguage(languageCode: String) {
        viewModelScope.launch {
            preferencesManager.setLanguage(languageCode)
        }
        // Apply locale change immediately (no activity recreation due to configChanges in manifest)
        val localeList = LocaleListCompat.forLanguageTags(languageCode)
        AppCompatDelegate.setApplicationLocales(localeList)
    }

    fun setTheme(themeCode: String) {
        viewModelScope.launch {
            preferencesManager.setTheme(themeCode)
        }
    }

    fun setNotificationsEnabled(enabled: Boolean) {
        viewModelScope.launch {
            preferencesManager.setNotificationEnabled(enabled)
        }
    }

    fun setBiometricEnabled(enabled: Boolean) {
        viewModelScope.launch {
            preferencesManager.setBiometricEnabled(enabled)
        }
    }
}
