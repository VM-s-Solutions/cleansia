package cz.cleansia.customer.features.disputes

import android.content.Context
import androidx.lifecycle.SavedStateHandle
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.customer.R
import cz.cleansia.customer.core.disputes.DisputeRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import dagger.hilt.android.qualifiers.ApplicationContext
import javax.inject.Inject
import kotlinx.coroutines.flow.MutableSharedFlow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.SharedFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asSharedFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch

/**
 * ViewModel for CreateDisputeScreen. Reads the optional `orderId` query param
 * from the back stack, validates the user-entered reason + description, and
 * submits via [DisputeRepository.create].
 *
 * On success: refreshes the singleton list cache (so the new dispute shows up
 * when the user returns to the list) and emits the new dispute id on
 * [createdDisputeId] — the screen observes this SharedFlow to navigate.
 *
 * Missing orderId is surfaced as an [error] string on init so the screen can
 * render an inline error row + disable submit, rather than routing through a
 * crash. The FAB on the list screen lands here deliberately in this state.
 */
@HiltViewModel
class CreateDisputeViewModel @Inject constructor(
    private val disputeRepository: DisputeRepository,
    savedStateHandle: SavedStateHandle,
    @ApplicationContext private val appContext: Context,
) : ViewModel() {

    /** Nullable — the FAB flow routes here without an orderId on purpose. */
    val orderId: String? = savedStateHandle.get<String>("orderId")?.takeIf { it.isNotBlank() }

    private val _submitting = MutableStateFlow(false)
    val submitting: StateFlow<Boolean> = _submitting.asStateFlow()

    private val _error = MutableStateFlow<String?>(null)
    val error: StateFlow<String?> = _error.asStateFlow()

    private val _createdDisputeId = MutableSharedFlow<String>(extraBufferCapacity = 1)
    val createdDisputeId: SharedFlow<String> = _createdDisputeId.asSharedFlow()

    fun submit(reason: Int, description: String) {
        val id = orderId ?: run {
            _error.value = appContext.getString(R.string.dispute_create_missing_order)
            return
        }
        // Defensive re-validation — the button is already disabled when these
        // don't hold, but handler guards keep the repo call clean.
        if (description.length !in DisputeFormConstants.DESCRIPTION_MIN_LENGTH..DisputeFormConstants.DESCRIPTION_MAX_LENGTH) return
        if (reason !in 1..7) return

        viewModelScope.launch {
            _submitting.value = true
            _error.value = null
            val disputeId = disputeRepository.create(id, reason, description.trim())
            _submitting.value = false
            if (disputeId != null) {
                // Refresh list cache so a subsequent nav to Disputes shows the
                // new entry immediately. Fire-and-forget — the nav happens
                // regardless of whether the refresh completes first.
                disputeRepository.refresh()
                _createdDisputeId.emit(disputeId)
            } else {
                // DisputeRepository.create already surfaced a snackbar; we
                // drop a short inline hint too so the form doesn't look inert.
                _error.value = appContext.getString(R.string.dispute_create_retry_hint)
            }
        }
    }

    fun clearError() {
        _error.value = null
    }
}
