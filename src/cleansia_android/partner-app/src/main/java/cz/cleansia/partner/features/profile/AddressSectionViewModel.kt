package cz.cleansia.partner.features.profile

import android.content.Context
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.core.location.GeocodedAddress
import cz.cleansia.core.servicearea.ServiceAreaProvider
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.core.ui.state.ActionState
import cz.cleansia.partner.R
import cz.cleansia.partner.api.client.CountryApi
import cz.cleansia.partner.api.model.CountryListItem
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
 * Three-state indicator for "how serviced is this address?".
 *
 *  - [Unknown]: lookup hasn't completed (initial state, or refreshing
 *    after a new pick).
 *  - [InServicedCity]: the picked city matches a serviced city — the
 *    UI shows a reassuring green ✓ row.
 *  - [OutsideServicedCity]: country itself is serviced but the city
 *    isn't. Cleaner home addresses aren't blocked by city — only
 *    customer order creation is — so we show an info row reminding
 *    them they can still take jobs in nearby serviced cities.
 *  - [CountryNotServiced]: edge case (country IsServiced=false). The
 *    UpdateAddressInfo validator will reject this on save, but we
 *    surface it ahead of time so the cleaner doesn't waste their
 *    onboarding round-trip.
 */
sealed class ServiceAreaStatus {
    data object Unknown : ServiceAreaStatus()
    data class InServicedCity(val cityName: String) : ServiceAreaStatus()
    data object OutsideServicedCity : ServiceAreaStatus()
    data object CountryNotServiced : ServiceAreaStatus()
}

/**
 * Form for the partner Address section. The cleaner's address is
 * picked on a map (full `AddressPickerScreen`) and applied via
 * [AddressSectionViewModel.applyPick] — no raw text fields.
 *
 * [pickedAddress] is the most recent Mapbox result; it always carries
 * the lat/lng we trust on save. [countries] is the cached serviced
 * country list used to resolve the Mapbox alpha-2 ISO code into the
 * backend country id we send in the update command.
 */
data class AddressForm(
    val employeeId: String = "",
    val pickedAddress: GeocodedAddress? = null,
    val countries: List<CountryListItem> = emptyList(),
    val serviceAreaStatus: ServiceAreaStatus = ServiceAreaStatus.Unknown,
) {
    /** Single-line summary for the section's tappable picker card. */
    val summaryLine1: String?
        get() = pickedAddress?.let { picked ->
            picked.street.ifBlank { picked.formatted.substringBefore(",") }.takeIf { it.isNotBlank() }
        }

    val summaryLine2: String?
        get() = pickedAddress?.let { picked ->
            listOfNotNull(
                picked.zipCode.takeIf { it.isNotBlank() },
                picked.city.takeIf { it.isNotBlank() },
                picked.country.takeIf { it.isNotBlank() },
            ).joinToString(" · ").takeIf { it.isNotBlank() }
        }
}

sealed interface AddressSectionUiState {
    data object Loading : AddressSectionUiState
    data object Error : AddressSectionUiState
    data class Loaded(val form: AddressForm) : AddressSectionUiState
}

@HiltViewModel
class AddressSectionViewModel @Inject constructor(
    private val profileRepository: ProfileRepository,
    private val countryApi: CountryApi,
    private val errorTranslator: ApiErrorTranslator,
    private val snackbar: SnackbarController,
    private val serviceAreaProvider: ServiceAreaProvider,
    private val json: Json,
    @ApplicationContext private val appContext: Context,
) : ViewModel() {

    private val _uiState = MutableStateFlow<AddressSectionUiState>(AddressSectionUiState.Loading)
    val uiState: StateFlow<AddressSectionUiState> = _uiState.asStateFlow()

    private val _saveState = MutableStateFlow<ActionState>(ActionState.Idle)
    val saveState: StateFlow<ActionState> = _saveState.asStateFlow()

    private val _saved = MutableSharedFlow<Unit>(extraBufferCapacity = 1)
    val saved: SharedFlow<Unit> = _saved.asSharedFlow()

    init { load() }

    private fun load() {
        viewModelScope.launch {
            _uiState.value = AddressSectionUiState.Loading

            val countriesResult = safeApiCall(json) { countryApi.countryGetServiced() }
            val countries = (countriesResult as? ApiResult.Success)?.data.orEmpty()

            when (val empResult = profileRepository.getCurrentEmployee()) {
                is ApiResult.Success -> {
                    val e = empResult.data
                    // Reconstruct a GeocodedAddress from the stored fields
                    // so the summary card shows the cleaner's current
                    // address on first open. ISO code isn't known from
                    // backend response (only countryId) — we map back
                    // through the cached countries list when present.
                    val countryName = countries.firstOrNull { it.id == e.countryId }?.name.orEmpty()
                    val countryIso = countries.firstOrNull { it.id == e.countryId }?.isoCode.orEmpty()
                    val picked = if (e.street.isNullOrBlank()) null else GeocodedAddress(
                        latitude = 0.0, // existing record may lack coords; save
                                          // will pass nulls so backend re-geocodes
                        longitude = 0.0,
                        street = e.street.orEmpty(),
                        city = e.city.orEmpty(),
                        zipCode = e.zipCode.orEmpty(),
                        country = countryName,
                        countryIsoCode = countryIso,
                        formatted = listOf(e.street, e.city, e.zipCode)
                            .filterNot { it.isNullOrBlank() }
                            .joinToString(", "),
                    )
                    _uiState.value = AddressSectionUiState.Loaded(
                        AddressForm(
                            employeeId = e.id.orEmpty(),
                            pickedAddress = picked,
                            countries = countries,
                        ),
                    )
                    // Kick off the service-area lookup for the loaded
                    // address so the indicator row reflects the saved
                    // state on screen entry (not just after re-picking).
                    if (picked != null && countryIso.isNotBlank()) {
                        refreshServiceArea(picked)
                    }
                }
                is ApiResult.Error -> {
                    snackbar.showError(errorTranslator.translate(empResult.error))
                    _uiState.value = AddressSectionUiState.Error
                }
            }
        }
    }

    /** Called by the screen after the picker pops with a fresh pick. */
    fun applyPick(address: GeocodedAddress) {
        updateForm {
            it.copy(
                pickedAddress = address,
                // Reset to Unknown immediately so the row shows the
                // loading skeleton while the lookup re-runs.
                serviceAreaStatus = ServiceAreaStatus.Unknown,
            )
        }
        refreshServiceArea(address)
    }

    /**
     * Resolves the city's service-area status. Runs in a fire-and-
     * forget coroutine because the result only feeds the indicator
     * row — failures degrade to Unknown rather than blocking save.
     *
     * Reads from the cached `:core.ServiceAreaProvider`; first call
     * fetches over the network, subsequent calls are O(1).
     */
    private fun refreshServiceArea(address: GeocodedAddress) {
        val cityName = address.city.takeIf { it.isNotBlank() }
        if (cityName == null) {
            updateForm { it.copy(serviceAreaStatus = ServiceAreaStatus.Unknown) }
            return
        }
        viewModelScope.launch {
            val mapboxCode = address.countryIsoCode.lowercase()
            val country = serviceAreaProvider.loadCountries().firstOrNull { c ->
                c.isoCode == mapboxCode || c.isoCode.startsWith(mapboxCode)
            }
            if (country == null) {
                updateForm { it.copy(serviceAreaStatus = ServiceAreaStatus.CountryNotServiced) }
                return@launch
            }
            val serviced = serviceAreaProvider.isCityServiced(country.id, cityName)
            updateForm {
                it.copy(
                    serviceAreaStatus = if (serviced)
                        ServiceAreaStatus.InServicedCity(cityName)
                    else ServiceAreaStatus.OutsideServicedCity,
                )
            }
        }
    }

    fun save() {
        val form = (_uiState.value as? AddressSectionUiState.Loaded)?.form ?: return
        if (_saveState.value is ActionState.Submitting) return
        val picked = form.pickedAddress
        if (picked == null) {
            snackbar.showError(appContext.getString(R.string.error_pick_address_first))
            return
        }
        if (form.employeeId.isBlank()) {
            snackbar.showError(appContext.getString(R.string.error_profile_not_loaded))
            return
        }

        // Mapbox returns ISO-3166 alpha-2 (`cz`); backend's `Country.IsoCode`
        // column holds ISO-3 (`CZE`). Match prefix case-insensitively so we
        // can find the matching countryId — also catches existing employees
        // whose loaded address put the backend's alpha-3 in `countryIsoCode`.
        val mapboxCode = picked.countryIsoCode.lowercase()
        val countryId = form.countries.firstOrNull { c ->
            val iso = c.isoCode?.lowercase().orEmpty()
            iso == mapboxCode || iso.startsWith(mapboxCode)
        }?.id

        if (countryId.isNullOrBlank()) {
            snackbar.showError(appContext.getString(R.string.error_country_not_serviced))
            return
        }

        viewModelScope.launch {
            _saveState.value = ActionState.Submitting
            // Send coords only when the picked address actually has them
            // (a freshly map-picked address will; a back-fill from a
            // legacy server record with no lat/lng yet won't — falling
            // through to server-side geocoding is correct in that case).
            val hasCoords = picked.latitude != 0.0 && picked.longitude != 0.0
            val result = profileRepository.updateAddress(
                employeeId = form.employeeId,
                street = picked.street.trim(),
                city = picked.city.trim(),
                zipCode = picked.zipCode.trim(),
                countryId = countryId,
                // State stays nullable on the backend for US/CA expansion
                // but the EU-only partner mobile UI doesn't collect it.
                // Existing State values on the row (if any) stay
                // untouched because we always send null here — backend's
                // address.Update is full-replace, so this means any
                // pre-existing State gets cleared. That's fine: no
                // partner-mobile cleaner has ever set State (no UI to do
                // so), so we're not destroying real data.
                state = null,
                latitude = picked.latitude.takeIf { hasCoords },
                longitude = picked.longitude.takeIf { hasCoords },
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

    private inline fun updateForm(transform: (AddressForm) -> AddressForm) {
        _uiState.update { state ->
            if (state is AddressSectionUiState.Loaded) state.copy(form = transform(state.form)) else state
        }
    }
}
