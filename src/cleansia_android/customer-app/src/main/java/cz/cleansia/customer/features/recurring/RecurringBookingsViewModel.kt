package cz.cleansia.customer.features.recurring

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.customer.core.memberships.MembershipRepository
import cz.cleansia.customer.core.recurring.RecurringBookingRepository
import cz.cleansia.customer.core.recurring.RecurringBookingTemplateDto
import dagger.hilt.android.lifecycle.HiltViewModel
import javax.inject.Inject
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.SharingStarted
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.map
import kotlinx.coroutines.flow.stateIn
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
) : ViewModel() {

    val templates: StateFlow<List<RecurringBookingTemplateDto>> = repository.templates

    val loading: StateFlow<Boolean> = repository.loading
    val loaded: StateFlow<Boolean> = repository.loaded

    val authoring: StateFlow<RecurringAuthoringGate> = membershipRepository.current
        .map { RecurringAuthoringGate.resolve(it?.hasMembership) }
        .stateIn(viewModelScope, SharingStarted.Eagerly, RecurringAuthoringGate.Allowed)

    private val _mutating = MutableStateFlow<String?>(null)
    /** Id of the template currently being toggled/deleted, null otherwise. */
    val mutating: StateFlow<String?> = _mutating.asStateFlow()

    init {
        viewModelScope.launch { repository.refresh() }
        viewModelScope.launch {
            if (membershipRepository.staleness.isStale()) membershipRepository.refresh()
        }
    }

    fun refresh() {
        viewModelScope.launch { repository.refresh() }
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
