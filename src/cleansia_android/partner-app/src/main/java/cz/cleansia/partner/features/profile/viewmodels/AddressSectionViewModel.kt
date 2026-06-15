package cz.cleansia.partner.features.profile.viewmodels

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.core.location.GeocodedAddress
import cz.cleansia.core.servicearea.ServiceAreaProvider
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.partner.api.client.CountryApi
import cz.cleansia.partner.api.model.CountryListItem
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
 * State for the partner Address section v2. The cleaner's address is
 * now picked on a map (full `AddressPickerScreen`) and applied to this
 * VM via [applyPick] — no more raw text fields for Street/City/ZIP/
 * Country. The optional `state` field stays inline for US/CA cases.
 *
 * [pickedAddress] is the most recent Mapbox result; it always carries
 * the lat/lng we trust on save. [countries] is the cached serviced
 * country list used to resolve the Mapbox alpha-2 ISO code into the
 * backend country id we send in the update command.
 */
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

data class AddressSectionUiState(
    val isLoading: Boolean = false,
    val isSaving: Boolean = false,
    val employeeId: String = "",
    val pickedAddress: GeocodedAddress? = null,
    val countries: List<CountryListItem> = emptyList(),
    val serviceAreaStatus: ServiceAreaStatus = ServiceAreaStatus.Unknown,
    val isSaved: Boolean = false,
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

@HiltViewModel
class AddressSectionViewModel @Inject constructor(
    private val profileRepository: ProfileRepository,
    private val countryApi: CountryApi,
    private val errorTranslator: ApiErrorTranslator,
    private val snackbar: SnackbarController,
    private val serviceAreaProvider: ServiceAreaProvider,
    private val json: Json,
) : ViewModel() {

    private val _uiState = MutableStateFlow(AddressSectionUiState())
    val uiState: StateFlow<AddressSectionUiState> = _uiState.asStateFlow()

    init { load() }

    private fun load() {
        viewModelScope.launch {
            _uiState.update { it.copy(isLoading = true) }

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
                    _uiState.update {
                        it.copy(
                            isLoading = false,
                            employeeId = e.id.orEmpty(),
                            pickedAddress = picked,
                            countries = countries,
                        )
                    }
                    // Kick off the service-area lookup for the loaded
                    // address so the indicator row reflects the saved
                    // state on screen entry (not just after re-picking).
                    if (picked != null && countryIso.isNotBlank()) {
                        refreshServiceArea(picked)
                    }
                }
                is ApiResult.Error -> {
                    _uiState.update { it.copy(isLoading = false, countries = countries) }
                    snackbar.showError(errorTranslator.translate(empResult.error))
                }
            }
        }
    }

    /** Called by the screen after the picker pops with a fresh pick. */
    fun applyPick(address: GeocodedAddress) {
        _uiState.update {
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
            _uiState.update { it.copy(serviceAreaStatus = ServiceAreaStatus.Unknown) }
            return
        }
        viewModelScope.launch {
            val mapboxCode = address.countryIsoCode.lowercase()
            val country = serviceAreaProvider.loadCountries().firstOrNull { c ->
                c.isoCode == mapboxCode || c.isoCode.startsWith(mapboxCode)
            }
            if (country == null) {
                _uiState.update { it.copy(serviceAreaStatus = ServiceAreaStatus.CountryNotServiced) }
                return@launch
            }
            val serviced = serviceAreaProvider.isCityServiced(country.id, cityName)
            _uiState.update {
                it.copy(
                    serviceAreaStatus = if (serviced)
                        ServiceAreaStatus.InServicedCity(cityName)
                    else ServiceAreaStatus.OutsideServicedCity,
                )
            }
        }
    }

    fun save() {
        val state = _uiState.value
        val picked = state.pickedAddress
        if (picked == null) {
            snackbar.showError("Pick your address on the map first")
            return
        }
        if (state.employeeId.isBlank()) {
            snackbar.showError("Profile not loaded yet")
            return
        }

        // Mapbox returns ISO-3166 alpha-2 (`cz`); backend's `Country.IsoCode`
        // column holds ISO-3 (`CZE`). Match prefix case-insensitively so we
        // can find the matching countryId — also catches existing employees
        // whose loaded address put the backend's alpha-3 in `countryIsoCode`.
        val mapboxCode = picked.countryIsoCode.lowercase()
        val countryId = state.countries.firstOrNull { c ->
            val iso = c.isoCode?.lowercase().orEmpty()
            iso == mapboxCode || iso.startsWith(mapboxCode)
        }?.id

        if (countryId.isNullOrBlank()) {
            snackbar.showError("This country isn't serviced yet")
            return
        }

        viewModelScope.launch {
            _uiState.update { it.copy(isSaving = true) }
            // Send coords only when the picked address actually has them
            // (a freshly map-picked address will; a back-fill from a
            // legacy server record with no lat/lng yet won't — falling
            // through to server-side geocoding is correct in that case).
            val hasCoords = picked.latitude != 0.0 && picked.longitude != 0.0
            val result = profileRepository.updateAddress(
                employeeId = state.employeeId,
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
                is ApiResult.Success -> _uiState.update { it.copy(isSaving = false, isSaved = true) }
                is ApiResult.Error -> {
                    _uiState.update { it.copy(isSaving = false) }
                    snackbar.showError(errorTranslator.translate(result.error))
                }
            }
        }
    }

    fun clearError() = _uiState.update { it } // No-op now — errors go via SnackbarController.
}
