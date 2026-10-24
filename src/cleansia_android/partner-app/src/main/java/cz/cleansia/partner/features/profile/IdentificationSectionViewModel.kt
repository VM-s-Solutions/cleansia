package cz.cleansia.partner.features.profile

import android.content.Context
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.core.ui.state.ActionState
import cz.cleansia.partner.R
import cz.cleansia.partner.api.client.CountryApi
import cz.cleansia.partner.api.model.GetCountryFieldLabelsCountryFieldLabelsDto
import cz.cleansia.partner.api.model.CountryListItem
import cz.cleansia.partner.api.model.EmployeeEntityType
import cz.cleansia.partner.core.network.ApiErrorTranslator
import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.network.safeApiCall
import cz.cleansia.partner.data.profile.ProfileRepository
import cz.cleansia.partner.features.orders.hasKey
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

/** `ValidatePassportId`'s bounds, counted on the trimmed value the save sends. */
private val PassportIdLength = 5..20

private const val PassportIdInvalidLength = "validation.passport_id.invalid_length"

/** What `UpdateIdentificationInfo` can refuse about the registration number the cleaner typed. */
private val RegistrationNumberRefusals = listOf(
    "validation.registration_number.invalid_format",
    "validation.registration_number.not_registered",
    "employee.business_ceased",
    "employee.trade_licence_inactive",
)

/**
 * Form for "Identification & business" — collects everything the
 * backend's `IsProfileComplete` check needs that doesn't live on the
 * other section screens: nationality + passport (the person), plus
 * business country + IČO (the business). A cleaner contracts as a
 * natural person, so there is no entity type to choose.
 *
 * [businessCountryId] is pre-filled from the cleaner's address country
 * on load so the typical case (business registered where the cleaner
 * lives) is one tap. They can override it via the country picker.
 *
 * [storedLegalEntityName] is display only: an operator may onboard a
 * company by hand, and what the server holds for such a row is shown,
 * never edited or sent from here. Such a row with no name cannot pass
 * the registration lock, and only support can add the name, so
 * [legalEntityNameError] says so under the locked field.
 */
data class IdentificationForm(
    val employeeId: String = "",
    val countries: List<CountryListItem> = emptyList(),
    val nationalityId: String? = null,
    val passportId: String = "",
    val businessCountryId: String? = null,
    val registrationNumber: String = "",
    val isLegalEntity: Boolean = false,
    val storedLegalEntityName: String? = null,
    val legalEntityNameError: String? = null,
    val nationalityError: String? = null,
    val passportError: String? = null,
    val businessCountryError: String? = null,
    val registrationNumberError: String? = null,
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

    /**
     * What the chosen business country calls its registration number and VAT id.
     *
     * These have been in `CountryConfiguration` since it was seeded and nothing returned them, so the
     * label was hardcoded to the Czech "IČO" in all five translation files — which a Polish or
     * Ukrainian partner then read, and Poland has no IČO. Null here means the country configures no
     * label and the screen falls back to its own neutral wording.
     */
    private val _fieldLabels = MutableStateFlow<GetCountryFieldLabelsCountryFieldLabelsDto?>(null)
    val fieldLabels: StateFlow<GetCountryFieldLabelsCountryFieldLabelsDto?> = _fieldLabels.asStateFlow()

    /** Guards against a stale answer overwriting a newer one when the picker is tapped twice. */
    private var fieldLabelsFor: String? = null

    init { load() }

    fun retry() = load()

    private fun load() {
        viewModelScope.launch {
            _uiState.value = IdentificationSectionUiState.Loading
            val countries = (safeApiCall(json) { countryApi.countryGetOverview() } as? ApiResult.Success)
                ?.data.orEmpty()
            when (val empResult = profileRepository.getCurrentEmployee()) {
                is ApiResult.Success -> {
                    val e = empResult.data
                    val isLegalEntity = e.entityType == EmployeeEntityType._2
                    _uiState.value = IdentificationSectionUiState.Loaded(
                        IdentificationForm(
                            employeeId = e.id.orEmpty(),
                            countries = countries,
                            nationalityId = e.nationalityId,
                            passportId = e.passportId.orEmpty(),
                            // Pre-fill business country with the address
                            // country so the typical case is zero-tap.
                            businessCountryId = e.countryId,
                            registrationNumber = e.registrationNumber.orEmpty(),
                            isLegalEntity = isLegalEntity,
                            storedLegalEntityName = e.legalEntityName?.takeIf { isLegalEntity },
                            legalEntityNameError = if (isLegalEntity && e.legalEntityName.isNullOrBlank()) {
                                appContext.getString(R.string.error_legal_entity_name_missing)
                            } else {
                                null
                            },
                        ),
                    )
                    e.countryId?.let { loadFieldLabels(it) }
                }
                is ApiResult.Error -> {
                    snackbar.showError(errorTranslator.translate(empResult.error))
                    _uiState.value = IdentificationSectionUiState.Error
                }
            }
        }
    }

    fun onNationalitySelected(id: String) = updateForm { it.copy(nationalityId = id, nationalityError = null) }

    fun onPassportChange(v: String) = updateForm { it.copy(passportId = v, passportError = null) }

    fun onBusinessCountrySelected(id: String) {
        updateForm { it.copy(businessCountryId = id, businessCountryError = null) }
        loadFieldLabels(id)
    }

    /**
     * Silent on failure, and silent on 404. A country we hold no configuration for is one whose word
     * for this we do not know — the screen's neutral fallback is the honest answer, and an error
     * banner would be reporting a missing translation as a broken form.
     */
    private fun loadFieldLabels(countryId: String) {
        if (fieldLabelsFor == countryId) return
        fieldLabelsFor = countryId
        viewModelScope.launch {
            val result = safeApiCall(json) { countryApi.countryGetFieldLabels(countryId) }
            // Only publish if the picker has not moved on since this call was made.
            if (fieldLabelsFor == countryId) {
                _fieldLabels.value = (result as? ApiResult.Success)?.data
            }
        }
    }

    fun onRegistrationNumberChange(v: String) =
        updateForm { it.copy(registrationNumber = v, registrationNumberError = null) }

    fun save() {
        val form = (_uiState.value as? IdentificationSectionUiState.Loaded)?.form ?: return
        if (_saveState.value is ActionState.Submitting) return
        if (form.employeeId.isBlank()) {
            snackbar.showError(appContext.getString(R.string.error_profile_not_loaded))
            return
        }

        val nationalityId = form.nationalityId?.takeUnless { it.isBlank() }
        val passportId = form.passportId.trim()
        val businessCountryId = form.businessCountryId?.takeUnless { it.isBlank() }
        val registrationNumber = form.registrationNumber.trim()
        var hasError = false
        if (nationalityId == null) {
            updateForm { it.copy(nationalityError = appContext.getString(R.string.error_pick_nationality)) }
            hasError = true
        }
        if (passportId.isEmpty()) {
            updateForm { it.copy(passportError = appContext.getString(R.string.error_passport_required)) }
            hasError = true
        } else if (passportId.length !in PassportIdLength) {
            updateForm {
                it.copy(passportError = appContext.getString(R.string.error_validation_passport_id_invalid_length))
            }
            hasError = true
        }
        if (businessCountryId == null) {
            updateForm { it.copy(businessCountryError = appContext.getString(R.string.error_pick_business_country)) }
            hasError = true
        }
        if (registrationNumber.isEmpty()) {
            updateForm {
                it.copy(registrationNumberError = appContext.getString(R.string.error_registration_number_required))
            }
            hasError = true
        }
        if (hasError || nationalityId == null || businessCountryId == null) return

        viewModelScope.launch {
            _saveState.value = ActionState.Submitting
            val result = profileRepository.updateIdentification(
                employeeId = form.employeeId,
                nationalityId = nationalityId,
                passportId = passportId,
                businessCountryId = businessCountryId,
                registrationNumber = registrationNumber,
            )
            when (result) {
                is ApiResult.Success -> {
                    _saveState.value = ActionState.Idle
                    _saved.emit(Unit)
                }
                is ApiResult.Error -> {
                    _saveState.value = ActionState.Idle
                    val error = result.error
                    when {
                        RegistrationNumberRefusals.any { error.hasKey(it) } ->
                            updateForm { it.copy(registrationNumberError = errorTranslator.translate(error)) }
                        error.hasKey(PassportIdInvalidLength) ->
                            updateForm { it.copy(passportError = errorTranslator.translate(error)) }
                        else -> snackbar.showError(errorTranslator.translate(error))
                    }
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
