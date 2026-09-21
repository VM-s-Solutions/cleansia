package cz.cleansia.customer.features.disputes

import android.content.Context
import androidx.lifecycle.SavedStateHandle
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.customer.R
import cz.cleansia.customer.core.disputes.DisputeLineRequest
import cz.cleansia.customer.core.disputes.DisputeRepository
import cz.cleansia.customer.core.orders.OrderRepository
import cz.cleansia.customer.ui.state.ActionState
import cz.cleansia.core.network.ApiError
import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.snackbar.SnackbarController
import dagger.hilt.android.lifecycle.HiltViewModel
import dagger.hilt.android.qualifiers.ApplicationContext
import java.util.UUID
import javax.inject.Inject
import kotlinx.coroutines.flow.MutableSharedFlow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.SharedFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asSharedFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch

/**
 * ViewModel for CreateDisputeScreen. Reads the optional `orderId` query param
 * from the back stack, validates the user-entered reason + description, and
 * submits via [DisputeRepository.create], then uploads every picked evidence
 * file to the new dispute in order.
 *
 * On success: refreshes the singleton list cache (so the new dispute shows up
 * when the user returns to the list) and emits the new dispute id on
 * [createdDisputeId] — the screen observes this SharedFlow to navigate.
 *
 * Missing orderId is surfaced as an [ActionState.Error] on submit so the screen
 * renders an inline retry hint + the standing missing-order banner (driven by
 * [orderId] being null), rather than routing through a crash. The FAB on the
 * list screen lands here deliberately in this state.
 */
@HiltViewModel
class CreateDisputeViewModel @Inject constructor(
    private val disputeRepository: DisputeRepository,
    private val orderRepository: OrderRepository,
    val snackbar: SnackbarController,
    savedStateHandle: SavedStateHandle,
    @ApplicationContext private val appContext: Context,
) : ViewModel() {

    /** Nullable — the FAB flow routes here without an orderId on purpose. */
    val orderId: String? = savedStateHandle.get<String>("orderId")?.takeIf { it.isNotBlank() }

    private val _submitState = MutableStateFlow<ActionState>(ActionState.Idle)
    val submitState: StateFlow<ActionState> = _submitState.asStateFlow()

    private val _createdDisputeId = MutableSharedFlow<String>(extraBufferCapacity = 1)
    val createdDisputeId: SharedFlow<String> = _createdDisputeId.asSharedFlow()

    /**
     * What was ON this order — the services bought alone, and the services inside each package — so
     * the customer can point at the parts that were not done properly.
     *
     * <p>Empty until the order loads, empty for the FAB flow that arrives with no order at all, and
     * empty if the fetch fails. In every one of those cases the screen simply omits the section: a
     * dispute names no items by default, and failing to load them must not stop someone filing one.
     * This is the whole reason the load is silent — no spinner, no error, no retry.</p>
     */
    private val _lineOptions = MutableStateFlow<List<DisputeLineOption>>(emptyList())
    val lineOptions: StateFlow<List<DisputeLineOption>> = _lineOptions.asStateFlow()

    /** Ticked rows, by [DisputeLineOption.key]. */
    private val _pickedLineKeys = MutableStateFlow<Set<String>>(emptySet())
    val pickedLineKeys: StateFlow<Set<String>> = _pickedLineKeys.asStateFlow()

    private val _pickedEvidence = MutableStateFlow<List<PickedEvidence>>(emptyList())
    val pickedEvidence: StateFlow<List<PickedEvidence>> = _pickedEvidence.asStateFlow()

    /**
     * Set the moment the server acknowledges the create. A later submit — after an upload failed —
     * resumes from here instead of filing a second dispute about the same money.
     */
    private var createdId: String? = null

    init {
        val id = orderId
        if (id != null) {
            viewModelScope.launch {
                val result = orderRepository.getById(id)
                if (result is ApiResult.Success) {
                    _lineOptions.value = buildDisputeLineOptions(result.data)
                }
            }
        }
    }

    fun toggleLine(key: String) {
        _pickedLineKeys.value = _pickedLineKeys.value.let { current ->
            if (key in current) current - key else current + key
        }
    }

    fun addEvidence(bytes: ByteArray, fileName: String, mimeType: String) {
        if (_submitState.value is ActionState.Submitting) return
        if (bytes.size > DisputeFormConstants.EVIDENCE_MAX_BYTES) {
            snackbar.showError(appContext.getString(R.string.dispute_evidence_too_large))
            return
        }
        if (mimeType.lowercase() !in DisputeFormConstants.EVIDENCE_ALLOWED_MIME_TYPES) {
            snackbar.showError(appContext.getString(R.string.dispute_evidence_unsupported_type))
            return
        }
        _pickedEvidence.update { it + PickedEvidence(UUID.randomUUID().toString(), fileName, mimeType, bytes) }
    }

    fun removeEvidence(key: String) {
        if (_submitState.value is ActionState.Submitting) return
        _pickedEvidence.update { list -> list.filterNot { it.key == key } }
    }

    fun submit(reason: Int, description: String) {
        if (_submitState.value is ActionState.Submitting) return
        val id = orderId ?: run {
            _submitState.value = ActionState.Error(appContext.getString(R.string.dispute_create_missing_order))
            return
        }
        if (description.length !in DisputeFormConstants.DESCRIPTION_MIN_LENGTH..DisputeFormConstants.DESCRIPTION_MAX_LENGTH) return
        if (reason !in 1..7) return

        _submitState.value = ActionState.Submitting
        viewModelScope.launch {
            val disputeId = createdId ?: create(id, reason, description) ?: return@launch
            uploadPending(disputeId)
            _submitState.value = ActionState.Idle
            _createdDisputeId.emit(disputeId)
        }
    }

    private suspend fun create(orderId: String, reason: Int, description: String): String? {
        // Only rows still on the loaded order. Nothing can go stale here today — the order is
        // fixed by the route — but the filter costs nothing and keeps the invariant local.
        val picked = _pickedLineKeys.value
        val lines = _lineOptions.value
            .filter { it.key in picked }
            .map { DisputeLineRequest(serviceId = it.serviceId, packageId = it.packageId) }

        return when (val result = disputeRepository.create(orderId, reason, description.trim(), lines)) {
            is ApiResult.Success -> {
                createdId = result.data
                disputeRepository.refresh()
                result.data
            }
            is ApiResult.Error -> {
                if (result.error !is ApiError.Network) {
                    snackbar.showError(result.error)
                    // Anything that is not a transport failure means the server answered, so the
                    // dispute may well exist despite the error — a refused response body is
                    // exactly that case. Refresh so the customer finds it in the list instead of
                    // filing a second dispute about the same money.
                    disputeRepository.refresh()
                }
                _submitState.value = ActionState.Error(appContext.getString(R.string.dispute_create_retry_hint))
                null
            }
        }
    }

    /**
     * One file at a time, in the order picked. A failure marks its own row and moves on: the dispute
     * exists either way, and the customer is told which files to add again from its detail.
     */
    private suspend fun uploadPending(disputeId: String) {
        val failedNames = mutableListOf<String>()
        for (file in _pickedEvidence.value) {
            if (file.upload == EvidenceUploadState.Uploaded) continue
            mark(file.key, EvidenceUploadState.Uploading)
            val result = disputeRepository.uploadEvidence(disputeId, file.bytes, file.fileName, file.mimeType)
            when (result) {
                is ApiResult.Success -> mark(file.key, EvidenceUploadState.Uploaded)
                is ApiResult.Error -> {
                    mark(file.key, EvidenceUploadState.Failed)
                    failedNames += file.fileName
                }
            }
        }
        if (failedNames.isNotEmpty()) {
            snackbar.showError(
                appContext.getString(R.string.dispute_create_evidence_partial, failedNames.joinToString(", ")),
            )
        }
    }

    private fun mark(key: String, upload: EvidenceUploadState) {
        _pickedEvidence.update { list -> list.map { if (it.key == key) it.withUpload(upload) else it } }
    }

    fun clearError() {
        if (_submitState.value is ActionState.Error) _submitState.value = ActionState.Idle
    }
}
