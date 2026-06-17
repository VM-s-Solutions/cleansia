package cz.cleansia.customer.features.recurring

import androidx.lifecycle.SavedStateHandle
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.core.network.ApiError
import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.customer.core.catalog.CatalogRepository
import cz.cleansia.customer.core.data.AddressRepository
import cz.cleansia.customer.core.orders.OrderRepository
import cz.cleansia.customer.core.recurring.CreateRecurringBookingRequest
import cz.cleansia.customer.core.recurring.RecurrenceFrequency
import cz.cleansia.customer.core.recurring.RecurringBookingRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import javax.inject.Inject
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.SharingStarted
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.flow.map
import kotlinx.coroutines.flow.stateIn
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import kotlinx.datetime.Instant
import kotlinx.datetime.TimeZone
import kotlinx.datetime.toLocalDateTime

/**
 * Shared form state for the "create recurring booking" screen. Backs both
 * Path A (blank-slate) and Path B (pre-filled from a Completed order).
 *
 * Path B is keyed on the optional `orderId` nav arg. When present, init()
 * fetches the order detail and copies services/packages/rooms/bathrooms/
 * paymentType/timeOfDay into the form. The user still picks frequency,
 * savedAddress, and startsOn.
 *
 * Why pre-fill is partial: order history doesn't carry SavedAddressId
 * (orders snapshot the inline address only). Forcing the user to pick a
 * saved address explicitly means we always end up with a valid template
 * the materializer can resolve, no string-matching gymnastics needed.
 */
@HiltViewModel
class CreateRecurringViewModel @Inject constructor(
    savedStateHandle: SavedStateHandle,
    private val recurringRepo: RecurringBookingRepository,
    private val orderRepo: OrderRepository,
    private val catalogRepo: CatalogRepository,
    private val addressRepo: AddressRepository,
    private val snackbar: SnackbarController,
) : ViewModel() {

    /** Optional source order id for Path B pre-fill. Null → Path A blank slate. */
    val sourceOrderId: String? = savedStateHandle.get<String>("orderId")?.takeIf { it.isNotBlank() }

    private val _state = MutableStateFlow(CreateRecurringFormState())
    val state: StateFlow<CreateRecurringFormState> = _state.asStateFlow()

    private val _submitting = MutableStateFlow(false)
    val submitting: StateFlow<Boolean> = _submitting.asStateFlow()

    /** Outcome of the latest submit attempt — UI listens to navigate / snackbar. */
    private val _submitOutcome = MutableStateFlow<SubmitOutcome?>(null)
    val submitOutcome: StateFlow<SubmitOutcome?> = _submitOutcome.asStateFlow()

    init {
        // Catalog + addresses are needed regardless of path. Refresh once on
        // entry; safe no-op if already loaded.
        viewModelScope.launch {
            catalogRepo.refresh().onError { error ->
                if (error !is ApiError.Network) snackbar.showError(error.getUserMessage())
            }
        }
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
    fun setPaymentType(t: Int) { _state.update { it.copy(paymentType = t) } }
    fun setStartsOn(iso: String) { _state.update { it.copy(startsOnIso = iso) } }

    // ─── Validation + submit ───

    /**
     * Build the wire request from current state. Returns null if the form is
     * incomplete — the screen already disables the submit button in that case
     * but we double-check here so callers can't bypass.
     */
    private fun buildRequest(): CreateRecurringBookingRequest? {
        val s = _state.value
        if (s.savedAddressId.isBlank()) return null
        if (s.selectedServiceIds.isEmpty() && s.selectedPackageIds.isEmpty()) return null
        if (s.startsOnIso.isBlank()) return null
        return CreateRecurringBookingRequest(
            frequency = s.frequency.code,
            dayOfWeek = s.dayOfWeek,
            timeOfDay = s.timeOfDay,
            rooms = s.rooms,
            bathrooms = s.bathrooms,
            savedAddressId = s.savedAddressId,
            selectedServiceIds = s.selectedServiceIds.toList(),
            selectedPackageIds = s.selectedPackageIds.toList(),
            paymentType = s.paymentType,
            startsOn = s.startsOnIso,
        )
    }

    /** True when the form has the minimum data needed to submit. */
    val isValid: StateFlow<Boolean> = _state
        .map { s ->
            s.savedAddressId.isNotBlank()
                && (s.selectedServiceIds.isNotEmpty() || s.selectedPackageIds.isNotEmpty())
                && s.startsOnIso.isNotBlank()
                && s.timeOfDay.isNotBlank()
        }
        .stateIn(viewModelScope, SharingStarted.Eagerly, false)

    fun submit() {
        if (_submitting.value) return
        val request = buildRequest() ?: return
        viewModelScope.launch {
            _submitting.value = true
            try {
                val result = recurringRepo.create(request)
                _submitOutcome.value = if (result is ApiResult.Success) SubmitOutcome.Success else SubmitOutcome.Failed
            } finally {
                _submitting.value = false
            }
        }
    }

    fun consumeOutcome() { _submitOutcome.value = null }

    // ─── Path B pre-fill ───

    private fun prefillFromOrder(orderId: String) {
        viewModelScope.launch {
            val order = orderRepo.getById(orderId)
                .onError { error -> if (error !is ApiError.Network) snackbar.showError(error.getUserMessage()) }
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
        }
    }
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
    /** 1 = Cash, 2 = Card. Default Cash (matches the old single-payment default). */
    val paymentType: Int = 1,
    /** ISO-8601 instant. Default empty — UI must set before submit. */
    val startsOnIso: String = "",
)

sealed interface SubmitOutcome {
    data object Success : SubmitOutcome
    data object Failed : SubmitOutcome
}
