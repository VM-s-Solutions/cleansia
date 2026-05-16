package cz.cleansia.customer.features.recurring

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.customer.core.recurring.RecurringBookingRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import javax.inject.Inject
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch

/**
 * Drives the recurring-bookings list. Pull-to-refresh on first composition,
 * delegates pause/resume + delete to the singleton repository (which keeps
 * its own cache fresh on every mutation).
 */
@HiltViewModel
class RecurringBookingsViewModel @Inject constructor(
    private val repository: RecurringBookingRepository,
) : ViewModel() {

    val templates: StateFlow<List<cz.cleansia.customer.core.recurring.RecurringBookingTemplateDto>> =
        repository.templates

    val loading: StateFlow<Boolean> = repository.loading
    val loaded: StateFlow<Boolean> = repository.loaded

    private val _mutating = MutableStateFlow<String?>(null)
    /** Id of the template currently being toggled/deleted, null otherwise. */
    val mutating: StateFlow<String?> = _mutating.asStateFlow()

    init {
        viewModelScope.launch { repository.refresh() }
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
