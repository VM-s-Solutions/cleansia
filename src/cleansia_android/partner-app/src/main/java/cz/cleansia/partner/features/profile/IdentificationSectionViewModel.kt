package cz.cleansia.partner.features.profile

import android.content.Context
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.core.ui.state.ActionState
import cz.cleansia.partner.R
import cz.cleansia.partner.api.client.CountryApi
import cz.cleansia.partner.api.model.CountryListItem
import cz.cleansia.partner.api.model.EmployeeEntityType
import cz.cleansia.partner.core.network.ApiErrorTranslator
import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.network.safeApiCall
import cz.cleansia.partner.data.profile.ProfileRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import dagger.hilt.android.qualifiers.ApplicationContext
import kotlinx.coroutines.flow.MutableSharedFlow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.SharedFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asSharedFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import kotlinx.serialization.json.Json
import javax.inject.Inject

/**
 * Form for "Identification & business" — collects everything the
 * backend's `IsProfileComplete` check needs that doesn't live on the
 * other section screens: nationality + passport (the person), plus
 * entity type + business country + IČO + optional VAT + legal entity
 * name (the business).
 *
 * [businessCountryId] is pre-filled from the cleaner's address country
 * on load so the typical case (business registered where the cleaner
 * lives) is one tap. They can override it via the country picker.
 */
data class IdentificationForm(
    val employeeId: String = "",
    val countries: List<CountryListItem> = emptyList(),
    val nationalityId: String? = null,
    val passportId: String = "",
    val entityType: EmployeeEntityType = EmployeeEntityType._1,
    val businessCountryId: String? = null,
    val registrationNumber: String = "",
    val vatNumber: String = "",
    val legalEntityName: String = "",
)

sealed interface IdentificationSectionUiState {
    data object Loading : IdentificationSectionUiState
    data object Error : IdentificationSectionUiState
    data class Loaded(val form: IdentificationForm) : IdentificationSectionUiState
}

@HiltViewModel
class IdentificationSectionViewModel @Inject constructor(
    private val profileRepository: ProfileRepository,
    private val countryApi: CountryApi,
    private val errorTranslator: ApiErrorTranslator,
    private val snackbar: SnackbarController,
    private val json: Json,
    @ApplicationContext private val appContext: Context,
) : ViewModel() {

    private val _uiState = MutableStateFlow<IdentificationSectionUiState>(IdentificationSectionUiState.Loading)
    val uiState: StateFlow<IdentificationSectionUiState> = _uiState.asStateFlow()

    private val _saveState = MutableStateFlow<ActionState>(ActionState.Idle)
    val saveState: StateFlow<ActionState> = _saveState.asStateFlow()

    private val _saved = MutableSharedFlow<Unit>(extraBufferCapacity = 1)
    val saved: SharedFlow<Unit> = _saved.asSharedFlow()

    init { load() }

    private fun load() {
        viewModelScope.launch {
            _uiState.value = IdentificationSectionUiState.Loading
            val countries = (safeApiCall(json) { countryApi.countryGetOverview() } as? ApiResult.Success)
                ?.data.orEmpty()
            when (val empResult = profileRepository.getCurrentEmployee()) {
                is ApiResult.Success -> {
                    val e = empResult.data
                    _uiState.value = IdentificationSectionUiState.Loaded(
                        IdentificationForm(
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
                        ),
                    )
                }
                is ApiResult.Error -> {
                    snackbar.showError(errorTranslator.translate(empResult.error))
                    _uiState.value = IdentificationSectionUiState.Error
                }
            }
        }
    }

    fun onNationalitySelected(id: String) = updateForm { it.copy(nationalityId = id) }

    fun onPassportChange(v: String) = updateForm { it.copy(passportId = v) }

    fun onEntityTypeSelected(type: EmployeeEntityType) = updateForm {
        // Clear legal entity name when switching back to natural
        // person — backend ignores it but a stale value is confusing
        // if the user toggles back to legal entity later.
        it.copy(
            entityType = type,
            legalEntityName = if (type == EmployeeEntityType._2) it.legalEntityName else "",
        )
    }

    fun onBusinessCountrySelected(id: String) = updateForm { it.copy(businessCountryId = id) }

    fun onRegistrationNumberChange(v: String) = updateForm { it.copy(registrationNumber = v) }

    fun onVatNumberChange(v: String) = updateForm { it.copy(vatNumber = v) }

    fun onLegalEntityNameChange(v: String) = updateForm { it.copy(legalEntityName = v) }

    fun save() {
        val form = (_uiState.value as? IdentificationSectionUiState.Loaded)?.form ?: return
        if (_saveState.value is ActionState.Submitting) return
        if (form.employeeId.isBlank()) {
            snackbar.showError(appContext.getString(R.string.error_profile_not_loaded))
            return
        }

        val nationalityId = form.nationalityId
        val businessCountryId = form.businessCountryId
        // Client-side guards so the error message points at the right
        // field instead of bouncing through the generic server-side
        // "Required" message. The server still re-validates everything.
        if (nationalityId.isNullOrBlank()) {
            snackbar.showError(appContext.getString(R.string.error_pick_nationality)); return
        }
        if (form.passportId.isBlank()) {
            snackbar.showError(appContext.getString(R.string.error_passport_required)); return
        }
        if (businessCountryId.isNullOrBlank()) {
            snackbar.showError(appContext.getString(R.string.error_pick_business_country)); return
        }
        if (form.registrationNumber.isBlank()) {
            snackbar.showError(appContext.getString(R.string.error_registration_number_required)); return
        }
        if (form.entityType == EmployeeEntityType._2 && form.legalEntityName.isBlank()) {
            snackbar.showError(appContext.getString(R.string.error_legal_entity_name_required)); return
        }

        viewModelScope.launch {
            _saveState.value = ActionState.Submitting
            val result = profileRepository.updateIdentification(
                employeeId = form.employeeId,
                nationalityId = nationalityId,
                passportId = form.passportId,
                entityType = form.entityType,
                businessCountryId = businessCountryId,
                registrationNumber = form.registrationNumber,
                vatNumber = form.vatNumber.takeIf { it.isNotBlank() },
                legalEntityName = form.legalEntityName.takeIf { it.isNotBlank() },
            )
            when (result) {
                is ApiResult.Success -> {
                    _saveState.value = ActionState.Idle
                    _saved.emit(Unit)
                }
                is ApiResult.Error -> {
                    _saveState.value = ActionState.Idle
                    snackbar.showError(errorTranslator.translate(result.error))
                }
            }
        }
    }

    private inline fun updateForm(transform: (IdentificationForm) -> IdentificationForm) {
        _uiState.update { state ->
            if (state is IdentificationSectionUiState.Loaded) state.copy(form = transform(state.form)) else state
        }
    }
}
