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
import cz.cleansia.customer.R
import cz.cleansia.customer.core.recurring.RecurringBookingRepository
import cz.cleansia.customer.core.recurring.UpdateRecurringBookingRequest
import cz.cleansia.customer.ui.state.ActionState
import dagger.hilt.android.lifecycle.HiltViewModel
import javax.inject.Inject
import kotlinx.coroutines.flow.MutableSharedFlow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.SharedFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asSharedFlow
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
 * Shared form state for the recurring-booking form. Backs three paths:
 * Path A (blank-slate create), Path B (create pre-filled from a Completed
 * order) and Path C (edit an existing template).
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
 *
 * Path C is keyed on the optional `templateId` nav arg and submits through
 * `update` instead of `create`. The backend update is a full replace, so the
 * form must start from the stored template rather than from defaults — a
 * template that can't be resolved leaves the form empty on purpose, which the
 * submit guard then refuses to send.
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

    /** Optional template id for Path C. Null → the form creates rather than updates. */
    val editingTemplateId: String? = savedStateHandle.get<String>("templateId")?.takeIf { it.isNotBlank() }

    val isEditing: Boolean = editingTemplateId != null

    private val _state = MutableStateFlow(CreateRecurringFormState())
    val state: StateFlow<CreateRecurringFormState> = _state.asStateFlow()

    private val _submitState = MutableStateFlow<ActionState>(ActionState.Idle)
    val submitState: StateFlow<ActionState> = _submitState.asStateFlow()

    /** One-shot success effect — the screen navigates on emit (snackbar fires in the VM). */
    private val _submitted = MutableSharedFlow<Unit>(extraBufferCapacity = 1)
    val submitted: SharedFlow<Unit> = _submitted.asSharedFlow()

    init {
        // Catalog + addresses are needed regardless of path. Refresh once on
        // entry; safe no-op if already loaded.
        viewModelScope.launch {
            catalogRepo.refresh().onError { error ->
                if (error !is ApiError.Network) snackbar.showError(error.getUserMessage())
            }
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
    fun setPaymentType(t: Int) { _state.update { it.copy(paymentType = t) } }
    fun setStartsOn(iso: String) { _state.update { it.copy(startsOnIso = iso) } }

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
            timeOfDay.isNotBlank()

    private fun CreateRecurringFormState.toCreateRequest() = CreateRecurringBookingRequest(
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
    private fun CreateRecurringFormState.toUpdateRequest(templateId: String) =
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

    /** True when the form has the minimum data needed to submit. */
    val isValid: StateFlow<Boolean> = _state
        .map { it.isSubmittable() }
        .stateIn(viewModelScope, SharingStarted.Eagerly, false)

    fun submit() {
        if (_submitState.value is ActionState.Submitting) return
        val form = _state.value
        if (!form.isSubmittable()) return
        _submitState.value = ActionState.Submitting
        viewModelScope.launch {
            val result = if (editingTemplateId != null) {
                recurringRepo.update(form.toUpdateRequest(editingTemplateId))
            } else {
                recurringRepo.create(form.toCreateRequest())
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
                        snackbar.showError(result.error.getUserMessage())
                    }
                    _submitState.value = ActionState.Error(result.error.getUserMessage())
                }
            }
        }
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
    /** ISO-8601 instant. No editor in the wizard; carried so an edit doesn't erase it. */
    val endsOnIso: String? = null,
)
