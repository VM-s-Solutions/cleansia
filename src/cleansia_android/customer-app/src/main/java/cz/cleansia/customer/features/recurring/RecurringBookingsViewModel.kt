package cz.cleansia.customer.features.recurring

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.network.WireContractViolation
import cz.cleansia.core.network.networkCall
import cz.cleansia.customer.core.catalog.CatalogApi
import cz.cleansia.customer.core.data.AddressRepository
import cz.cleansia.customer.core.data.UserAddress
import cz.cleansia.customer.core.memberships.MembershipRepository
import cz.cleansia.customer.core.recurring.RecurringBookingRepository
import cz.cleansia.customer.core.recurring.RecurringBookingTemplateDto
import dagger.hilt.android.lifecycle.HiltViewModel
import javax.inject.Inject
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.SharingStarted
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.combine
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.flow.map
import kotlinx.coroutines.flow.stateIn
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch

/**
 * Drives the recurring-bookings list. Refreshes on first composition, delegates
 * pause/resume + delete to the singleton repository (which keeps its own cache
 * fresh on every mutation).
 *
 * The membership answer reaches only [authoring]: pause, resume and delete are
 * ungated on the server and are ungated here, because a lapsed subscriber is
 * still being charged for every occurrence these schedules generate.
 */
@HiltViewModel
class RecurringBookingsViewModel @Inject constructor(
    private val repository: RecurringBookingRepository,
    private val membershipRepository: MembershipRepository,
    private val addressRepository: AddressRepository,
    private val catalogApi: CatalogApi,
) : ViewModel() {

    val templates: StateFlow<List<RecurringBookingTemplateDto>> = repository.templates

    val loading: StateFlow<Boolean> = repository.loading
    val loaded: StateFlow<Boolean> = repository.loaded

    val authoring: StateFlow<RecurringAuthoringGate> = membershipRepository.current
        .map { RecurringAuthoringGate.resolve(it) }
        .stateIn(viewModelScope, SharingStarted.Eagerly, RecurringAuthoringGate.Allowed)

    private val _mutating = MutableStateFlow<String?>(null)
    /** Id of the template currently being toggled/deleted, null otherwise. */
    val mutating: StateFlow<String?> = _mutating.asStateFlow()

    /** A schedule is priced in its saved address's country; null until the addresses land. */
    private val addresses = MutableStateFlow<List<UserAddress>?>(null)

    /** What each market's catalogue lists today — its service ids, then its package ids — keyed by country (null: the platform default). */
    private val catalogues = MutableStateFlow<Map<String?, Pair<Set<String>, Set<String>>>>(emptyMap())

    /**
     * The schedules holding a service or package their market's catalogue no longer lists. A schedule
     * whose market's catalogue has not been read is not judged: the card says nothing rather than guess.
     */
    val noLongerOffered: StateFlow<Set<String>> = combine(templates, addresses, catalogues) { templates, addresses, catalogues ->
        templates.filter { template ->
            val address = addresses?.firstOrNull { it.serverId == template.savedAddressId } ?: return@filter false
            val (services, packages) = catalogues[address.countryId] ?: return@filter false
            !services.containsAll(template.selectedServiceIds) || !packages.containsAll(template.selectedPackageIds)
        }.mapTo(mutableSetOf()) { it.id }
    }.stateIn(viewModelScope, SharingStarted.Eagerly, emptySet())

    init {
        viewModelScope.launch { load() }
        viewModelScope.launch {
            if (membershipRepository.staleness.isStale()) membershipRepository.refresh()
        }
    }

    fun refresh() {
        viewModelScope.launch { load() }
    }

    private suspend fun load() {
        repository.refresh()
        readCatalogues()
    }

    /**
     * Re-read on every visit, one catalogue per market the schedules are priced in, each on its own so
     * the catalogue Home and booking share stays theirs. A read that fails leaves what was last read; a
     * market never read stays unjudged. Nothing here is the customer's to act on, so a failure is silent.
     */
    private suspend fun readCatalogues() {
        val templates = repository.templates.value
        if (templates.isEmpty()) return
        if (addressRepository.refreshFromServer() !is ApiResult.Success) return
        val list = addressRepository.addresses.first()
        addresses.value = list
        val markets = templates.mapNotNull { template -> list.firstOrNull { it.serverId == template.savedAddressId } }
            .mapTo(mutableSetOf()) { it.countryId }
        for (countryId in markets) {
            val listed = listedIn(countryId) ?: continue
            catalogues.update { it + (countryId to listed) }
        }
    }

    private suspend fun listedIn(countryId: String?): Pair<Set<String>, Set<String>>? = try {
        val services = networkCall { catalogApi.getServices(countryId) }?.takeIf { it.isSuccessful }?.body()
        val packages = networkCall { catalogApi.getPackages(countryId) }?.takeIf { it.isSuccessful }?.body()
        if (services == null || packages == null) null
        else services.mapTo(mutableSetOf()) { it.id } to packages.mapTo(mutableSetOf()) { it.id }
    } catch (_: WireContractViolation) {
        null
    }

    fun toggleActive(templateId: String, currentlyActive: Boolean) {
        viewModelScope.launch {
            _mutating.value = templateId
            try {
                repository.setActive(templateId, !currentlyActive)
            } finally {
                _mutating.value = null
            }
        }
    }

    fun delete(templateId: String) {
        viewModelScope.launch {
            _mutating.value = templateId
            try {
                repository.delete(templateId)
            } finally {
                _mutating.value = null
            }
        }
    }
}
