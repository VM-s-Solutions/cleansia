package cz.cleansia.partner.features.profile.viewmodels

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.partner.api.client.CountryApi
import cz.cleansia.partner.api.model.CountryListItem
import cz.cleansia.partner.api.model.EmployeeEntityType
import cz.cleansia.partner.core.network.ApiErrorTranslator
import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.network.safeApiCall
import cz.cleansia.partner.data.profile.ProfileRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import kotlinx.serialization.json.Json
import javax.inject.Inject

/**
 * State for "Identification & business" — collects everything the
 * backend's `IsProfileComplete` check needs that doesn't live on the
 * other section screens: nationality + passport (the person), plus
 * entity type + business country + IČO + optional VAT + legal entity
 * name (the business).
 *
 * [businessCountryId] is pre-filled from the cleaner's address country
 * on load so the typical case (business registered where the cleaner
 * lives) is one tap. They can override it via the country picker.
 */
data class IdentificationSectionUiState(
    val isLoading: Boolean = false,
    val isSaving: Boolean = false,
    val employeeId: String = "",
    val countries: List<CountryListItem> = emptyList(),
    // Identification (person)
    val nationalityId: String? = null,
    val passportId: String = "",
    // Business identity
    val entityType: EmployeeEntityType = EmployeeEntityType._1,
    val businessCountryId: String? = null,
    val registrationNumber: String = "",
    val vatNumber: String = "",
    val legalEntityName: String = "",
    val error: String? = null,
    val isSaved: Boolean = false,
)

@HiltViewModel
class IdentificationSectionViewModel @Inject constructor(
    private val profileRepository: ProfileRepository,
    private val countryApi: CountryApi,
    private val errorTranslator: ApiErrorTranslator,
    private val snackbar: SnackbarController,
    private val json: Json,
) : ViewModel() {

    private val _uiState = MutableStateFlow(IdentificationSectionUiState())
    val uiState: StateFlow<IdentificationSectionUiState> = _uiState.asStateFlow()

    init { load() }

    private fun load() {
        viewModelScope.launch {
            _uiState.update { it.copy(isLoading = true, error = null) }
            val countries = (safeApiCall(json) { countryApi.countryGetOverview() } as? ApiResult.Success)
                ?.data.orEmpty()
            when (val empResult = profileRepository.getCurrentEmployee()) {
                is ApiResult.Success -> {
                    val e = empResult.data
                    _uiState.update {
                        it.copy(
                            isLoading = false,
                            employeeId = e.id.orEmpty(),
                            countries = countries,
                            nationalityId = e.nationalityId,
                            passportId = e.passportId.orEmpty(),
                            entityType = e.entityType ?: EmployeeEntityType._1,
                            // Pre-fill business country with the address
                            // country so the typical case is zero-tap.
                            businessCountryId = e.countryId,
                            registrationNumber = e.registrationNumber.orEmpty(),
                            vatNumber = e.vatNumber.orEmpty(),
                            legalEntityName = e.legalEntityName.orEmpty(),
                        )
                    }
                }
                is ApiResult.Error -> {
                    _uiState.update { it.copy(isLoading = false, countries = countries) }
                    snackbar.showError(errorTranslator.translate(empResult.error))
                }
            }
        }
    }

    fun onNationalitySelected(id: String) =
        _uiState.update { it.copy(nationalityId = id, error = null) }

    fun onPassportChange(v: String) =
        _uiState.update { it.copy(passportId = v, error = null) }

    fun onEntityTypeSelected(type: EmployeeEntityType) =
        _uiState.update {
            // Clear legal entity name when switching back to natural
            // person — backend ignores it but a stale value is confusing
            // if the user toggles back to legal entity later.
            it.copy(
                entityType = type,
                legalEntityName = if (type == EmployeeEntityType._2) it.legalEntityName else "",
                error = null,
            )
        }

    fun onBusinessCountrySelected(id: String) =
        _uiState.update { it.copy(businessCountryId = id, error = null) }

    fun onRegistrationNumberChange(v: String) =
        _uiState.update { it.copy(registrationNumber = v, error = null) }

    fun onVatNumberChange(v: String) =
        _uiState.update { it.copy(vatNumber = v, error = null) }

    fun onLegalEntityNameChange(v: String) =
        _uiState.update { it.copy(legalEntityName = v, error = null) }

    fun save() {
        val state = _uiState.value
        if (state.employeeId.isBlank()) {
            snackbar.showError("Profile not loaded yet")
            return
        }

        val nationalityId = state.nationalityId
        val businessCountryId = state.businessCountryId
        // Client-side guards so the error message points at the right
        // field instead of bouncing through the generic server-side
        // "Required" message. The server still re-validates everything.
        if (nationalityId.isNullOrBlank()) {
            snackbar.showError("Pick your nationality"); return
        }
        if (state.passportId.isBlank()) {
            snackbar.showError("Enter your passport / ID number"); return
        }
        if (businessCountryId.isNullOrBlank()) {
            snackbar.showError("Pick the country your business is registered in"); return
        }
        if (state.registrationNumber.isBlank()) {
            snackbar.showError("Enter your registration number (IČO)"); return
        }
        if (state.entityType == EmployeeEntityType._2 && state.legalEntityName.isBlank()) {
            snackbar.showError("Enter the legal entity name"); return
        }

        viewModelScope.launch {
            _uiState.update { it.copy(isSaving = true) }
            val result = profileRepository.updateIdentification(
                employeeId = state.employeeId,
                nationalityId = nationalityId,
                passportId = state.passportId,
                entityType = state.entityType,
                businessCountryId = businessCountryId,
                registrationNumber = state.registrationNumber,
                vatNumber = state.vatNumber.takeIf { it.isNotBlank() },
                legalEntityName = state.legalEntityName.takeIf { it.isNotBlank() },
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
