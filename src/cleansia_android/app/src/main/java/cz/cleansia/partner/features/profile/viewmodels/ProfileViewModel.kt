package cz.cleansia.partner.features.profile.viewmodels

import androidx.appcompat.app.AppCompatDelegate
import androidx.core.os.LocaleListCompat
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import android.net.Uri
import cz.cleansia.partner.core.network.ApiErrorTranslator
import cz.cleansia.partner.core.network.ApiResult
import cz.cleansia.partner.core.storage.PreferencesManager
import cz.cleansia.partner.core.storage.TokenManager
import cz.cleansia.partner.domain.models.profile.AvailabilityUtils
import cz.cleansia.partner.domain.models.profile.Country
import cz.cleansia.partner.domain.models.profile.DateOverride
import cz.cleansia.partner.domain.models.profile.DayAvailability
import cz.cleansia.partner.domain.models.profile.DocumentType
import cz.cleansia.partner.domain.models.profile.EmployeeDocument
import cz.cleansia.partner.domain.models.profile.EmployeeProfile
import cz.cleansia.partner.domain.models.profile.UpdateAddressInfoRequest
import cz.cleansia.partner.domain.models.profile.UpdateAvailabilityRequest
import cz.cleansia.partner.domain.models.profile.UpdateBankDetailsRequest
import cz.cleansia.partner.domain.models.profile.UpdateEmergencyContactRequest
import cz.cleansia.partner.domain.models.profile.UpdateIdentificationInfoRequest
import cz.cleansia.partner.domain.models.profile.UpdatePersonalInfoRequest
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
    val countries: List<Country> = emptyList(),
    val documents: List<EmployeeDocument> = emptyList(),
    val availability: List<DayAvailability> = emptyList(),
    val dateOverrides: List<DateOverride> = emptyList(),
    val isLoggingOut: Boolean = false,
    val logoutSuccess: Boolean = false,
    val isUploadingPhoto: Boolean = false,
    val photoUploadSuccess: Boolean = false,
    val profilePhotoUri: Uri? = null,
    val editingSections: Set<ProfileSection> = emptySet(),
    val editFormState: ProfileEditFormState = ProfileEditFormState(),
    val validationErrors: Map<String, String> = emptyMap()
) {
    // Keep backward compatibility
    val isEditMode: Boolean get() = editingSections.isNotEmpty()
}

@HiltViewModel
class ProfileViewModel @Inject constructor(
    private val profileRepository: ProfileRepository,
    private val authRepository: AuthRepository,
    private val preferencesManager: PreferencesManager,
    private val tokenManager: TokenManager,
    private val errorTranslator: ApiErrorTranslator
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

            // Load countries
            when (val countriesResult = profileRepository.getCountries()) {
                is ApiResult.Success -> {
                    _uiState.update { it.copy(countries = countriesResult.data) }
                }
                is ApiResult.Error -> { /* Countries load failure is non-fatal */ }
            }

            // Load profile
            when (val profileResult = profileRepository.getCurrentEmployee()) {
                is ApiResult.Success -> {
                    _uiState.update {
                        it.copy(
                            profile = profileResult.data,
                            availability = AvailabilityUtils.apiToUiAvailability(profileResult.data.availability),
                            dateOverrides = AvailabilityUtils.apiToUiDateOverrides(profileResult.data.availability)
                        )
                    }
                    tokenManager.saveUserName(profileResult.data.firstName, profileResult.data.lastName)
                }
                is ApiResult.Error -> {
                    _uiState.update { it.copy(error = errorTranslator.translateError(profileResult.error)) }
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
                    _uiState.update {
                        it.copy(
                            profile = profileResult.data,
                            availability = AvailabilityUtils.apiToUiAvailability(profileResult.data.availability),
                            dateOverrides = AvailabilityUtils.apiToUiDateOverrides(profileResult.data.availability)
                        )
                    }
                }
                is ApiResult.Error -> {
                    _uiState.update { it.copy(error = errorTranslator.translateError(profileResult.error)) }
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
                editFormState = formState,
                validationErrors = emptyMap()
            )
        }
    }

    fun cancelEditingSection(section: ProfileSection) {
        _uiState.update {
            it.copy(
                editingSections = it.editingSections - section,
                validationErrors = emptyMap()
            )
        }
    }

    fun saveSection(section: ProfileSection) {
        val currentProfile = _uiState.value.profile ?: return
        val formState = _uiState.value.editFormState

        // Validate before saving
        val errors = ProfileValidator.validateSection(section, formState)
        if (errors.isNotEmpty()) {
            _uiState.update { it.copy(validationErrors = errors) }
            return
        }

        viewModelScope.launch {
            _uiState.update { it.copy(savingSection = section, error = null, validationErrors = emptyMap()) }

            val result = when (section) {
                ProfileSection.PERSONAL -> profileRepository.updatePersonalInfo(
                    UpdatePersonalInfoRequest(
                        employeeId = currentProfile.id,
                        firstName = formState.firstName,
                        lastName = formState.lastName,
                        birthDate = formState.dateOfBirth,
                        phone = formState.phoneNumber,
                        email = formState.email
                    )
                )
                ProfileSection.IDENTIFICATION -> profileRepository.updateIdentificationInfo(
                    UpdateIdentificationInfoRequest(
                        employeeId = currentProfile.id,
                        nationalityId = formState.nationalityId,
                        passportId = formState.passportId,
                        taxId = formState.taxId.ifBlank { null }
                    )
                )
                ProfileSection.ADDRESS -> profileRepository.updateAddressInfo(
                    UpdateAddressInfoRequest(
                        employeeId = currentProfile.id,
                        street = formState.street,
                        city = formState.city,
                        zipCode = formState.zipCode,
                        countryId = formState.countryId
                    )
                )
                ProfileSection.BANK -> profileRepository.updateBankDetails(
                    UpdateBankDetailsRequest(
                        employeeId = currentProfile.id,
                        iban = formState.iban.uppercase().replace(" ", "")
                    )
                )
                ProfileSection.EMERGENCY -> profileRepository.updateEmergencyContact(
                    UpdateEmergencyContactRequest(
                        employeeId = currentProfile.id,
                        emergencyName = formState.emergencyContactName.ifBlank { null },
                        emergencyPhone = formState.emergencyContactPhone.ifBlank { null }
                    )
                )
            }

            when (result) {
                is ApiResult.Success -> {
                    // Update local profile with the saved values
                    val updatedProfile = when (section) {
                        ProfileSection.PERSONAL -> currentProfile.copy(
                            firstName = formState.firstName.ifBlank { null },
                            lastName = formState.lastName.ifBlank { null },
                            email = formState.email,
                            phoneNumber = formState.phoneNumber.ifBlank { null },
                            dateOfBirth = formState.dateOfBirth.ifBlank { null }
                        )
                        ProfileSection.IDENTIFICATION -> currentProfile.copy(
                            passportId = formState.passportId.ifBlank { null },
                            taxId = formState.taxId.ifBlank { null },
                            nationalityId = formState.nationalityId.ifBlank { null }
                        )
                        ProfileSection.ADDRESS -> currentProfile.copy(
                            street = formState.street.ifBlank { null },
                            city = formState.city.ifBlank { null },
                            zipCode = formState.zipCode.ifBlank { null },
                            countryId = formState.countryId.ifBlank { null }
                        )
                        ProfileSection.BANK -> currentProfile.copy(
                            iban = formState.iban.uppercase().replace(" ", "").ifBlank { null }
                        )
                        ProfileSection.EMERGENCY -> currentProfile.copy(
                            emergencyContactName = formState.emergencyContactName.ifBlank { null },
                            emergencyContactPhone = formState.emergencyContactPhone.ifBlank { null }
                        )
                    }

                    _uiState.update {
                        it.copy(
                            savingSection = null,
                            editingSections = it.editingSections - section,
                            profile = updatedProfile,
                            saveSuccess = true
                        )
                    }
                    if (section == ProfileSection.PERSONAL) {
                        tokenManager.saveUserName(updatedProfile.firstName, updatedProfile.lastName)
                    }
                }
                is ApiResult.Error -> {
                    _uiState.update {
                        it.copy(
                            savingSection = null,
                            error = errorTranslator.translateError(result.error)
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
                editFormState = formState,
                validationErrors = emptyMap()
            )
        }
    }

    fun exitEditMode() {
        _uiState.update { it.copy(editingSections = emptySet(), validationErrors = emptyMap()) }
    }

    // Form field update methods
    fun updateFirstName(value: String) {
        _uiState.update { it.copy(editFormState = it.editFormState.copy(firstName = value)) }
    }

    fun updateLastName(value: String) {
        _uiState.update { it.copy(editFormState = it.editFormState.copy(lastName = value)) }
    }

    fun updateEmail(value: String) {
        _uiState.update { it.copy(editFormState = it.editFormState.copy(email = value)) }
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

    fun updateIban(value: String) {
        _uiState.update { it.copy(editFormState = it.editFormState.copy(iban = value)) }
    }

    // Identification
    fun selectNationality(countryId: String) {
        _uiState.update { it.copy(editFormState = it.editFormState.copy(nationalityId = countryId)) }
    }

    fun updatePassportId(value: String) {
        _uiState.update { it.copy(editFormState = it.editFormState.copy(passportId = value)) }
    }

    fun updateTaxId(value: String) {
        _uiState.update { it.copy(editFormState = it.editFormState.copy(taxId = value)) }
    }

    // Address
    fun selectCountry(countryId: String) {
        _uiState.update { it.copy(editFormState = it.editFormState.copy(countryId = countryId)) }
    }

    // Emergency contact
    fun updateEmergencyContactName(value: String) {
        _uiState.update { it.copy(editFormState = it.editFormState.copy(emergencyContactName = value)) }
    }

    fun updateEmergencyContactPhone(value: String) {
        _uiState.update { it.copy(editFormState = it.editFormState.copy(emergencyContactPhone = value)) }
    }

    fun getCountryName(countryId: String?, languageCode: String): String {
        if (countryId.isNullOrBlank()) return ""
        return _uiState.value.countries.find { it.id == countryId }?.getLocalizedName(languageCode) ?: ""
    }

    fun setProfilePhotoUri(uri: Uri?) {
        _uiState.update { it.copy(profilePhotoUri = uri) }
    }

    fun uploadProfilePhoto(data: ByteArray, fileName: String) {
        viewModelScope.launch {
            _uiState.update { it.copy(isUploadingPhoto = true, error = null) }

            when (val result = profileRepository.uploadProfilePhoto(data, fileName)) {
                is ApiResult.Success -> {
                    _uiState.update {
                        it.copy(
                            isUploadingPhoto = false,
                            profile = result.data,
                            photoUploadSuccess = true
                        )
                    }
                }
                is ApiResult.Error -> {
                    _uiState.update {
                        it.copy(
                            isUploadingPhoto = false,
                            error = errorTranslator.translateError(result.error)
                        )
                    }
                }
            }
        }
    }

    fun clearPhotoUploadSuccess() {
        _uiState.update { it.copy(photoUploadSuccess = false) }
    }

    fun saveProfile() {
        val currentProfile = _uiState.value.profile ?: return
        val formState = _uiState.value.editFormState

        val updatedProfile = currentProfile.copy(
            firstName = formState.firstName.takeIf { it.isNotBlank() },
            lastName = formState.lastName.takeIf { it.isNotBlank() },
            phoneNumber = formState.phoneNumber.takeIf { it.isNotBlank() },
            dateOfBirth = formState.dateOfBirth.takeIf { it.isNotBlank() },
            nationalityId = formState.nationalityId.takeIf { it.isNotBlank() },
            passportId = formState.passportId.takeIf { it.isNotBlank() },
            taxId = formState.taxId.takeIf { it.isNotBlank() },
            street = formState.street.takeIf { it.isNotBlank() },
            city = formState.city.takeIf { it.isNotBlank() },
            zipCode = formState.zipCode.takeIf { it.isNotBlank() },
            countryId = formState.countryId.takeIf { it.isNotBlank() },
            iban = formState.iban.takeIf { it.isNotBlank() },
            emergencyContactName = formState.emergencyContactName.takeIf { it.isNotBlank() },
            emergencyContactPhone = formState.emergencyContactPhone.takeIf { it.isNotBlank() }
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
                            error = errorTranslator.translateError(result.error)
                        )
                    }
                }
            }
        }
    }

    // Document management methods
    fun uploadDocument(data: ByteArray, fileName: String, documentType: DocumentType = DocumentType.OTHER) {
        viewModelScope.launch {
            _uiState.update { it.copy(isUploadingDocument = true, error = null) }

            when (val result = profileRepository.saveDocuments(listOf(Triple(data, fileName, documentType)))) {
                is ApiResult.Success -> {
                    _uiState.update {
                        it.copy(
                            isUploadingDocument = false,
                            documents = result.data,
                            uploadSuccess = true
                        )
                    }
                }
                is ApiResult.Error -> {
                    _uiState.update {
                        it.copy(
                            isUploadingDocument = false,
                            error = errorTranslator.translateError(result.error)
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
                            error = errorTranslator.translateError(result.error)
                        )
                    }
                }
            }
        }
    }

    fun downloadDocument(documentId: String, fileName: String) {
        viewModelScope.launch {
            when (val result = profileRepository.downloadDocument(documentId)) {
                is ApiResult.Success -> {
                    // Download successful - the actual file save to device storage
                    // would need to be handled by the UI layer with context
                }
                is ApiResult.Error -> {
                    _uiState.update { it.copy(error = errorTranslator.translateError(result.error)) }
                }
            }
        }
    }

    // Availability methods
    fun updateAvailability(availability: List<DayAvailability>) {
        _uiState.update { it.copy(availability = availability) }
    }

    fun saveAvailability() {
        val currentProfile = _uiState.value.profile ?: return
        val availability = _uiState.value.availability
        val dateOverrides = _uiState.value.dateOverrides

        viewModelScope.launch {
            _uiState.update { it.copy(isSavingAvailability = true, error = null) }

            // Merge weekly schedule and date overrides into a single API map
            val apiAvailability = AvailabilityUtils.uiToApiAvailability(availability) +
                AvailabilityUtils.uiToApiDateOverrides(dateOverrides)
            val request = UpdateAvailabilityRequest(
                employeeId = currentProfile.id,
                availability = apiAvailability
            )

            when (val result = profileRepository.updateAvailability(request)) {
                is ApiResult.Success -> {
                    _uiState.update {
                        it.copy(
                            isSavingAvailability = false,
                            availabilitySaveSuccess = true
                        )
                    }
                }
                is ApiResult.Error -> {
                    _uiState.update {
                        it.copy(
                            isSavingAvailability = false,
                            error = errorTranslator.translateError(result.error)
                        )
                    }
                }
            }
        }
    }

    fun clearAvailabilitySaveSuccess() {
        _uiState.update { it.copy(availabilitySaveSuccess = false) }
    }

    // Date override methods
    fun updateDateOverrides(overrides: List<DateOverride>) {
        _uiState.update { it.copy(dateOverrides = overrides) }
    }

    fun addDateOverride(override: DateOverride) {
        _uiState.update { state ->
            val existing = state.dateOverrides.toMutableList()
            val index = existing.indexOfFirst { it.date == override.date }
            if (index >= 0) {
                existing[index] = override
            } else {
                existing.add(override)
            }
            state.copy(dateOverrides = existing.sortedBy { it.date })
        }
    }

    fun removeDateOverride(date: String) {
        _uiState.update { state ->
            state.copy(dateOverrides = state.dateOverrides.filter { it.date != date })
        }
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
