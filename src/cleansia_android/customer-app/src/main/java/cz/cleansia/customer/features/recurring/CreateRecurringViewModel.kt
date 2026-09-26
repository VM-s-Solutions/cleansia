package cz.cleansia.customer.features.recurring

import androidx.lifecycle.SavedStateHandle
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import android.content.Context
import cz.cleansia.core.network.ApiError
import cz.cleansia.core.network.userMessage
import dagger.hilt.android.qualifiers.ApplicationContext
import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.network.networkCall
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.customer.core.booking.BookingApi
import cz.cleansia.customer.core.booking.CashEligibility
import cz.cleansia.customer.core.booking.QuoteOrderCommand
import cz.cleansia.customer.core.catalog.CatalogRepository
import cz.cleansia.customer.core.catalog.PackageListItem
import cz.cleansia.customer.core.catalog.ServiceListItem
import cz.cleansia.customer.core.data.AddressRepository
import cz.cleansia.customer.core.data.UserAddress
import cz.cleansia.customer.core.market.MarketRepository
import cz.cleansia.customer.core.market.countryId
import cz.cleansia.customer.core.orders.OrderRepository
import cz.cleansia.customer.core.recurring.CreateRecurringBookingRequest
import cz.cleansia.customer.core.recurring.RecurrenceFrequency
import cz.cleansia.customer.R
import cz.cleansia.customer.core.recurring.RecurringBookingRepository
import cz.cleansia.customer.core.recurring.UpdateRecurringBookingRequest
import cz.cleansia.customer.ui.state.ActionState
import dagger.hilt.android.lifecycle.HiltViewModel
import javax.inject.Inject
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.FlowPreview
import kotlinx.coroutines.flow.MutableSharedFlow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.SharedFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asSharedFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.SharingStarted
import kotlinx.coroutines.flow.collectLatest
import kotlinx.coroutines.flow.combine
import kotlinx.coroutines.flow.debounce
import kotlinx.coroutines.flow.distinctUntilChanged
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.flow.map
import kotlinx.coroutines.flow.stateIn
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import kotlinx.datetime.Instant
import kotlinx.datetime.TimeZone
import kotlinx.datetime.toLocalDateTime

/**
 * Shared form state for the recurring-booking form, backing three paths: blank create, create pre-filled
 * from a completed order, and edit.
 *
 * **Pre-fill is deliberately partial.** Order history does not carry a saved-address id — orders snapshot
 * the inline address — so the user picks one explicitly and the template is always resolvable by the
 * materializer, with no string matching.
 * -> /flows/booking-and-pricing#recurring-bookings
 */
@HiltViewModel
class CreateRecurringViewModel @Inject constructor(
    savedStateHandle: SavedStateHandle,
    private val recurringRepo: RecurringBookingRepository,
    private val orderRepo: OrderRepository,
    private val catalogRepo: CatalogRepository,
    private val addressRepo: AddressRepository,
    private val marketRepo: MarketRepository,
    private val bookingApi: BookingApi,
    private val snackbar: SnackbarController,
    @ApplicationContext private val appContext: Context,
) : ViewModel() {

    /** Optional source order id for Path B pre-fill. Null → Path A blank slate. */
    val sourceOrderId: String? = savedStateHandle.get<String>("orderId")?.takeIf { it.isNotBlank() }

    /** Optional template id for Path C. Null → the form creates rather than updates. */
    val editingTemplateId: String? = savedStateHandle.get<String>("templateId")?.takeIf { it.isNotBlank() }

    val isEditing: Boolean = editingTemplateId != null

    private val _state = MutableStateFlow(CreateRecurringFormState())
    val state: StateFlow<CreateRecurringFormState> = _state.asStateFlow()

    private val _step = MutableStateFlow(1)
    val step: StateFlow<Int> = _step.asStateFlow()

    val canStepBack: StateFlow<Boolean> = _step
        .map { it > 1 }
        .stateIn(viewModelScope, SharingStarted.Eagerly, false)

    private val _catalogState = MutableStateFlow<RecurringCatalogState>(RecurringCatalogState.Loading)
    /**
     * The catalogue as this form sees it. A failed entry read used to leave the form on an empty
     * catalogue for good — nothing retried it, and a selection prefilled from the source order went
     * to the server unpruned against a catalogue the customer never saw.
     */
    val catalogState: StateFlow<RecurringCatalogState> = _catalogState.asStateFlow()

    val canAdvance: StateFlow<Boolean> = combine(_state, _step, _catalogState) { s, step, catalog ->
        when (step) {
            1 -> s.timeOfDay.isNotBlank()
            2 -> s.selectedServiceIds.isNotEmpty() || s.selectedPackageIds.isNotEmpty()
            3 -> s.savedAddressId.isNotBlank() &&
                s.startsOnIso.isNotBlank() &&
                s.paymentType != null &&
                catalog is RecurringCatalogState.Loaded
            else -> false
        }
    }.stateIn(viewModelScope, SharingStarted.Eagerly, false)

    val savedAddresses: StateFlow<List<UserAddress>> = addressRepo.addresses
        .stateIn(viewModelScope, SharingStarted.Eagerly, emptyList())

    val services: StateFlow<List<ServiceListItem>> = catalogRepo.services
    val packages: StateFlow<List<PackageListItem>> = catalogRepo.packages

    private val _submitState = MutableStateFlow<ActionState>(ActionState.Idle)
    val submitState: StateFlow<ActionState> = _submitState.asStateFlow()

    /** One-shot success effect — the screen navigates on emit (snackbar fires in the VM). */
    private val _submitted = MutableSharedFlow<Unit>(extraBufferCapacity = 1)
    val submitted: SharedFlow<Unit> = _submitted.asSharedFlow()

    /** The crew the server last quoted, with the selection it was quoted for. */
    private val quotedCrew = MutableStateFlow<QuotedCrew?>(null)

    /** A schedule is always an account's, so only the crew decides. */
    val cashEligibility: StateFlow<CashEligibility> = combine(_state, quotedCrew) { s, crew -> cashEligibilityOf(s, crew) }
        .stateIn(viewModelScope, SharingStarted.Eagerly, CashEligibility.Pending)

    /** A cash choice was taken away because it stopped being allowed; cleared by the next choice. */
    private val _cashCleared = MutableStateFlow(false)

    /** Said only while cash is still not available; a selection that allows it again needs no warning. */
    val cashClearedNotice: StateFlow<Boolean> = combine(_cashCleared, cashEligibility) { cleared, cash ->
        cleared && cash != CashEligibility.Available
    }.stateIn(viewModelScope, SharingStarted.Eagerly, false)

    @OptIn(FlowPreview::class)
    private val crewWatcher = viewModelScope.launch {
        _state
            .map { it.pricedSelection() }
            .distinctUntilChanged()
            .debounce(QUOTE_DEBOUNCE_MS)
            .collectLatest { quoteCrew(it) }
    }

    private val cashWatcher = viewModelScope.launch {
        combine(_state, cashEligibility) { s, cash -> s.paymentType == PAYMENT_CASH && cash.isRefused }
            .distinctUntilChanged()
            .collect { refused -> if (refused) dropCash() }
    }

    init {
        // The market watcher starts only once the entry refresh has answered: the repository skips a
        // refresh while one is in flight, so a concurrent reload for the address's country would be
        // dropped and the picks pruned against the wrong catalogue.
        viewModelScope.launch {
            loadCatalog(marketRepo.ensureLoaded().countryId)
            _state
                .map { it.savedAddressId }
                .distinctUntilChanged()
                .collectLatest { addressId -> followMarket(resolveCountryId(addressId)) }
        }
        if (editingTemplateId != null) {
            prefillFromTemplate(editingTemplateId)
        } else {
            // Default the savedAddressId to the user's default address so Path B
            // and Path A both start with a sensible pick.
            viewModelScope.launch {
                val addresses = addressRepo.addresses.first()
                val defaultAddr = addresses.firstOrNull { it.isDefault } ?: addresses.firstOrNull()
                defaultAddr?.serverId?.let { serverId ->
                    _state.value = _state.value.copy(savedAddressId = serverId)
                }
            }
            if (sourceOrderId != null) prefillFromOrder(sourceOrderId)
        }
    }

    // ─── Mutators (one per field; called from Compose) ───

    fun setFrequency(f: RecurrenceFrequency) { _state.update { it.copy(frequency = f) } }
    fun setDayOfWeek(dow: Int) { _state.update { it.copy(dayOfWeek = dow) } }
    fun setTimeOfDay(time: String) { _state.update { it.copy(timeOfDay = time) } }
    fun setRooms(n: Int) { _state.update { it.copy(rooms = n.coerceAtLeast(0)) } }
    fun setBathrooms(n: Int) { _state.update { it.copy(bathrooms = n.coerceAtLeast(0)) } }
    fun setSavedAddressId(id: String) { _state.update { it.copy(savedAddressId = id) } }
    fun toggleService(id: String) {
        _state.update {
            val current = it.selectedServiceIds.toMutableSet()
            if (!current.add(id)) current.remove(id)
            it.copy(selectedServiceIds = current)
        }
    }
    fun togglePackage(id: String) {
        _state.update {
            val current = it.selectedPackageIds.toMutableSet()
            if (!current.add(id)) current.remove(id)
            it.copy(selectedPackageIds = current)
        }
    }
    fun setPaymentType(t: Int) {
        if (t == PAYMENT_CASH && cashEligibilityOf(_state.value, quotedCrew.value) != CashEligibility.Available) return
        _cashCleared.value = false
        _state.update { it.copy(paymentType = t) }
    }
    fun setStartsOn(iso: String) { _state.update { it.copy(startsOnIso = iso) } }

    fun nextStep() { _step.update { (it + 1).coerceAtMost(TOTAL_STEPS) } }
    fun previousStep() { _step.update { (it - 1).coerceAtLeast(1) } }

    // ─── Validation + submit ───

    /**
     * The form is complete enough to send. The screen already disables the
     * submit button in that case but we double-check here so callers can't
     * bypass — and in edit mode an unresolved template leaves the form empty,
     * so this is what stops defaults being written over a stored schedule.
     */
    private fun CreateRecurringFormState.isSubmittable(): Boolean =
        savedAddressId.isNotBlank() &&
            (selectedServiceIds.isNotEmpty() || selectedPackageIds.isNotEmpty()) &&
            startsOnIso.isNotBlank() &&
            timeOfDay.isNotBlank() &&
            paymentType != null

    private fun CreateRecurringFormState.toCreateRequest(paymentType: Int) = CreateRecurringBookingRequest(
        frequency = frequency.code,
        dayOfWeek = dayOfWeek,
        timeOfDay = timeOfDay,
        rooms = rooms,
        bathrooms = bathrooms,
        savedAddressId = savedAddressId,
        selectedServiceIds = selectedServiceIds.toList(),
        selectedPackageIds = selectedPackageIds.toList(),
        paymentType = paymentType,
        startsOn = startsOnIso,
    )

    /**
     * The backend's `UpdateSchedule` rewrites every schedule column from the
     * command, so a field the form does not echo back is not "left alone" —
     * it is erased. `endsOn` has no editor in this wizard, which is exactly
     * why the stored value has to ride along.
     */
    private fun CreateRecurringFormState.toUpdateRequest(templateId: String, paymentType: Int) =
        UpdateRecurringBookingRequest(
            templateId = templateId,
            frequency = frequency.code,
            dayOfWeek = dayOfWeek,
            timeOfDay = timeOfDay,
            rooms = rooms,
            bathrooms = bathrooms,
            savedAddressId = savedAddressId,
            selectedServiceIds = selectedServiceIds.toList(),
            selectedPackageIds = selectedPackageIds.toList(),
            paymentType = paymentType,
            startsOn = startsOnIso,
            endsOn = endsOnIso,
        )

    /**
     * True when the form has the minimum data needed to submit. A schedule is only ever submitted
     * against a catalogue the customer could see: a prefilled selection no market has vetted is not a
     * booking.
     */
    val isValid: StateFlow<Boolean> = combine(_state, _catalogState) { s, catalog ->
        s.isSubmittable() && catalog is RecurringCatalogState.Loaded
    }.stateIn(viewModelScope, SharingStarted.Eagerly, false)

    fun submit() {
        if (_submitState.value is ActionState.Submitting) return
        if (_catalogState.value !is RecurringCatalogState.Loaded) return
        val form = _state.value
        if (!form.isSubmittable()) return
        val paymentType = form.paymentType ?: return
        _submitState.value = ActionState.Submitting
        viewModelScope.launch {
            if (paymentType == PAYMENT_CASH && !cashConfirmedFor(form)) return@launch
            val result = if (editingTemplateId != null) {
                recurringRepo.update(form.toUpdateRequest(editingTemplateId, paymentType))
            } else {
                recurringRepo.create(form.toCreateRequest(paymentType))
            }
            when (result) {
                is ApiResult.Success -> {
                    _submitState.value = ActionState.Idle
                    snackbar.showSuccessKey(
                        if (isEditing) R.string.recurring_edit_success else R.string.recurring_create_success,
                    )
                    _submitted.emit(Unit)
                }
                is ApiResult.Error -> {
                    if (result.error !is ApiError.Network) {
                        snackbar.showError(result.error)
                    }
                    _submitState.value = ActionState.Error(result.error.userMessage(appContext))
                }
            }
        }
    }

    /** Cash goes out only on a fresh quote for the form that says one cleaner does it. */
    private suspend fun cashConfirmedFor(form: CreateRecurringFormState): Boolean {
        val cash = CashEligibility.resolve(signedIn = true, requiredEmployees = quoteCrew(form.pricedSelection()))
        when {
            cash == CashEligibility.Available -> return true
            cash.isRefused -> {
                dropCash()
                _submitState.value = ActionState.Idle
            }
            else -> {
                snackbar.showErrorKey(R.string.recurring_cash_unchecked)
                _submitState.value = ActionState.Error(appContext.getString(R.string.recurring_cash_unchecked))
            }
        }
        return false
    }

    /** Never replaced by card: the customer is told and chooses again. */
    private fun dropCash() {
        if (_state.value.paymentType != PAYMENT_CASH) return
        _state.update { it.copy(paymentType = null) }
        _cashCleared.value = true
        snackbar.showInfoKey(R.string.recurring_cash_cleared)
    }

    private fun cashEligibilityOf(form: CreateRecurringFormState, crew: QuotedCrew?): CashEligibility =
        CashEligibility.resolve(
            signedIn = true,
            requiredEmployees = crew?.takeIf { it.selection == form.pricedSelection() }?.requiredEmployees,
        )

    /** The crew for [selection], or null when the server could not quote it. */
    private suspend fun quoteCrew(selection: PricedSelection): Int? {
        if (selection.serviceIds.isEmpty() && selection.packageIds.isEmpty()) {
            quotedCrew.value = null
            return null
        }
        val response = networkCall {
            bookingApi.quote(
                QuoteOrderCommand(
                    selectedServiceIds = selection.serviceIds.toList(),
                    selectedPackageIds = selection.packageIds.toList(),
                    rooms = selection.rooms,
                    bathrooms = selection.bathrooms,
                    countryId = resolveCountryId(selection.savedAddressId),
                ),
            )
        }
        val crew = response?.takeIf { it.isSuccessful }?.body()?.requiredEmployees ?: return null
        quotedCrew.value = QuotedCrew(selection, crew)
        return crew
    }

    /**
     * The country the schedule is priced in (ADR-0058 D4): the picked address's; the chosen market's
     * while no address is picked; null for an address with no country — the platform default.
     */
    private suspend fun resolveCountryId(savedAddressId: String): String? {
        if (savedAddressId.isBlank()) return marketRepo.state.value.countryId
        return addressRepo.addresses.first().firstOrNull { it.serverId == savedAddressId }?.countryId
    }

    /**
     * A retry lands the first catalogue the selection has ever been checked against: the market
     * watcher was idle while there was nothing to reload, and the prefill could not prune.
     */
    fun retryCatalog() {
        viewModelScope.launch {
            loadCatalog(resolveCountryId(_state.value.savedAddressId))
            if (_catalogState.value is RecurringCatalogState.Loaded && isCatalogueForSelectedMarket()) {
                pruneSelectionToCatalogue()
            }
        }
    }

    private suspend fun loadCatalog(countryId: String?) {
        _catalogState.value = RecurringCatalogState.Loading
        _catalogState.value = when (val result = catalogRepo.refresh(countryId)) {
            is ApiResult.Success -> RecurringCatalogState.Loaded
            is ApiResult.Error -> {
                if (result.error !is ApiError.Network) snackbar.showError(result.error)
                RecurringCatalogState.Error
            }
        }
    }

    /**
     * Re-read the catalogue for the address's market and drop whatever it no longer offers. A pick
     * with no price row in the new currency would be refused at submit, so it goes now, with a
     * notice, while the customer can still re-pick. A failed reload proves nothing and prunes nothing.
     */
    private suspend fun followMarket(countryId: String?) {
        if (catalogRepo.countryId.value == countryId) return
        if (catalogRepo.refresh(countryId) !is ApiResult.Success) return
        _catalogState.value = RecurringCatalogState.Loaded
        pruneSelectionToCatalogue()
    }

    /**
     * A market reload still in flight prunes when it lands; pruning against the catalogue it is
     * replacing would drop what the new market may well price.
     */
    private suspend fun isCatalogueForSelectedMarket(): Boolean =
        catalogRepo.loaded.value && catalogRepo.countryId.value == resolveCountryId(_state.value.savedAddressId)

    private fun pruneSelectionToCatalogue() {
        val services = catalogRepo.services.value.map { it.id }.toSet()
        val packages = catalogRepo.packages.value.map { it.id }.toSet()
        var dropped = false
        _state.update { s ->
            val kept = s.copy(
                selectedServiceIds = s.selectedServiceIds.filterTo(mutableSetOf()) { it in services },
                selectedPackageIds = s.selectedPackageIds.filterTo(mutableSetOf()) { it in packages },
            )
            dropped = kept != s
            kept
        }
        if (dropped) snackbar.showInfo(appContext.getString(R.string.booking_market_items_unavailable))
    }

    /**
     * The home carousel prices from the same repository, so a wizard left on a foreign address's
     * market must hand the chosen market back. `viewModelScope` is already closed here — the wizard
     * is popped before this runs — so the reload rides on a scope of its own.
     */
    override fun onCleared() {
        val market = marketRepo.state.value.countryId
        if (catalogRepo.countryId.value != market) {
            CoroutineScope(Dispatchers.Main.immediate).launch { catalogRepo.refresh(market) }
        }
        super.onCleared()
    }

    companion object {
        const val TOTAL_STEPS = 3

        /** The backend's `PaymentType`. */
        const val PAYMENT_CASH = 1
        const val PAYMENT_CARD = 2

        private const val QUOTE_DEBOUNCE_MS = 400L
    }

    // ─── Path C pre-fill ───

    private fun prefillFromTemplate(templateId: String) {
        viewModelScope.launch {
            val template = recurringRepo.templates.value.firstOrNull { it.id == templateId }
                ?: run {
                    recurringRepo.refresh()
                    recurringRepo.templates.value.firstOrNull { it.id == templateId }
                }
            if (template == null) {
                snackbar.showErrorKey(R.string.recurring_edit_load_failed)
                return@launch
            }
            _state.value = CreateRecurringFormState(
                frequency = RecurrenceFrequency.fromCode(template.frequency),
                dayOfWeek = template.dayOfWeek,
                timeOfDay = template.timeOfDay,
                rooms = template.rooms,
                bathrooms = template.bathrooms,
                savedAddressId = template.savedAddressId,
                selectedServiceIds = template.selectedServiceIds.toSet(),
                selectedPackageIds = template.selectedPackageIds.toSet(),
                paymentType = template.paymentType,
                startsOnIso = template.startsOn,
                endsOnIso = template.endsOn,
            )
        }
    }

    // ─── Path B pre-fill ───

    private fun prefillFromOrder(orderId: String) {
        viewModelScope.launch {
            val order = orderRepo.getById(orderId)
                .onError { error -> if (error !is ApiError.Network) snackbar.showError(error) }
                .getOrNull()
                ?: return@launch
            val timeOfDay = order.cleaningDateTime?.let { iso ->
                runCatching {
                    val instant = Instant.parse(iso)
                    val local = instant.toLocalDateTime(TimeZone.currentSystemDefault())
                    "%02d:%02d".format(local.hour, local.minute)
                }.getOrNull()
            }
            val dayOfWeek = order.cleaningDateTime?.let { iso ->
                runCatching {
                    val instant = Instant.parse(iso)
                    val local = instant.toLocalDateTime(TimeZone.currentSystemDefault())
                    // Java DayOfWeek: Mon=1..Sun=7. Backend wants .NET DayOfWeek: Sun=0..Sat=6.
                    local.dayOfWeek.value % 7
                }.getOrNull()
            }
            _state.update { current ->
                current.copy(
                    rooms = order.rooms.coerceAtLeast(0),
                    bathrooms = order.bathrooms.coerceAtLeast(0),
                    selectedServiceIds = order.selectedServices?.mapNotNull { it.id }?.toSet().orEmpty(),
                    selectedPackageIds = order.selectedPackages?.mapNotNull { it.id }?.toSet().orEmpty(),
                    paymentType = order.paymentType?.value ?: current.paymentType,
                    timeOfDay = timeOfDay ?: current.timeOfDay,
                    dayOfWeek = dayOfWeek ?: current.dayOfWeek,
                )
            }
            if (isCatalogueForSelectedMarket()) pruneSelectionToCatalogue()
        }
    }
}

/** What a quote prices. The day, the time and the way to pay move no money. */
private data class PricedSelection(
    val serviceIds: Set<String>,
    val packageIds: Set<String>,
    val rooms: Int,
    val bathrooms: Int,
    val savedAddressId: String,
)

private data class QuotedCrew(val selection: PricedSelection, val requiredEmployees: Int)

private fun CreateRecurringFormState.pricedSelection() = PricedSelection(
    serviceIds = selectedServiceIds,
    packageIds = selectedPackageIds,
    rooms = rooms,
    bathrooms = bathrooms,
    savedAddressId = savedAddressId,
)

sealed interface RecurringCatalogState {
    data object Loading : RecurringCatalogState
    data object Error : RecurringCatalogState
    data object Loaded : RecurringCatalogState
}

/** Form state — single object so Compose recomposes on any field change. */
data class CreateRecurringFormState(
    val frequency: RecurrenceFrequency = RecurrenceFrequency.Weekly,
    /**
     * .NET DayOfWeek (Sun=0..Sat=6). Default Thursday — mid-week is the
     * lowest-conflict slot for cleaning bookings (weekends fill up first,
     * Mondays often clash with work-from-home routines).
     */
    val dayOfWeek: Int = 4,
    /** "HH:mm" 24h. Default 10:00 — common booking time. */
    val timeOfDay: String = "10:00",
    val rooms: Int = 2,
    val bathrooms: Int = 1,
    val savedAddressId: String = "",
    val selectedServiceIds: Set<String> = emptySet(),
    val selectedPackageIds: Set<String> = emptySet(),
    /** The backend's `PaymentType`; null once a cash choice was taken away and nothing was chosen since. */
    val paymentType: Int? = CreateRecurringViewModel.PAYMENT_CARD,
    /** ISO-8601 instant. Default empty — UI must set before submit. */
    val startsOnIso: String = "",
    /** ISO-8601 instant. No editor in the wizard; carried so an edit doesn't erase it. */
    val endsOnIso: String? = null,
)
