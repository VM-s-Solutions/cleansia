package cz.cleansia.customer.features.disputes

import android.content.Context
import androidx.lifecycle.SavedStateHandle
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.customer.R
import cz.cleansia.customer.core.disputes.DisputeDetailsDto
import cz.cleansia.customer.core.disputes.DisputeRepository
import cz.cleansia.core.snackbar.SnackbarController
import dagger.hilt.android.lifecycle.HiltViewModel
import dagger.hilt.android.qualifiers.ApplicationContext
import javax.inject.Inject
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch

/**
 * ViewModel for DisputeDetailScreen. Loads a single dispute on init (no
 * caching in the repo — details always hit the network), and handles posting
 * new messages via [DisputeRepository.addMessage]. After a successful send,
 * re-fetches the dispute to pick up the persisted reply (the repo doesn't
 * append to cache because there's no details cache).
 *
 * Wave 3 Phase D1 adds [uploadEvidence]: client-side validates size + content
 * type, then forwards to [DisputeRepository.uploadEvidence]. On success we
 * trigger a [load] so the new evidence row appears in the LazyColumn — same
 * "fire-and-refresh" pattern as messages, since the repo has no detail cache.
 *
 * UiState mirrors the standard Loading/Loaded/Error funnel — the missing-arg
 * path collapses to Error immediately so the screen shows the retry/back UI
 * rather than crashing. DisputeRepository already surfaces snackbar errors
 * for network + non-2xx; the VM just translates null into the terminal state.
 */
@HiltViewModel
class DisputeDetailViewModel @Inject constructor(
    private val disputeRepository: DisputeRepository,
    val snackbar: SnackbarController,
    @ApplicationContext private val appContext: Context,
    savedStateHandle: SavedStateHandle,
) : ViewModel() {

    sealed interface UiState {
        data object Loading : UiState
        data class Loaded(val dispute: DisputeDetailsDto) : UiState
        data object Error : UiState
    }

    private val disputeId: String? = savedStateHandle.get<String>("disputeId")

    private val _state = MutableStateFlow<UiState>(UiState.Loading)
    val state: StateFlow<UiState> = _state.asStateFlow()

    private val _sending = MutableStateFlow(false)
    val sending: StateFlow<Boolean> = _sending.asStateFlow()

    /**
     * True while at least one evidence upload is in flight. Multi-file picks
     * are uploaded sequentially (see [uploadEvidence] callers), so this stays
     * true across the whole batch — the UI shows a single global spinner on
     * the "Add evidence" button rather than per-file progress.
     */
    private val _uploadingEvidence = MutableStateFlow(false)
    val uploadingEvidence: StateFlow<Boolean> = _uploadingEvidence.asStateFlow()

    init { load() }

    fun load() {
        val id = disputeId ?: run {
            _state.value = UiState.Error
            return
        }
        viewModelScope.launch {
            _state.value = UiState.Loading
            val result = disputeRepository.getById(id)
            _state.value = if (result != null) UiState.Loaded(result) else UiState.Error
        }
    }

    /**
     * Post a reply to this dispute. Trims the body client-side to avoid
     * submitting whitespace-only content; no-ops on empty / oversized
     * (the send button is disabled on those too, this is a defense-in-depth).
     *
     * On success, triggers a re-fetch so the new message appears in the thread.
     */
    fun sendMessage(content: String) {
        val id = disputeId ?: return
        val trimmed = content.trim()
        if (trimmed.length !in 1..2000) return
        viewModelScope.launch {
            _sending.value = true
            val ok = disputeRepository.addMessage(id, trimmed)
            _sending.value = false
            if (ok) load()
        }
    }

    /**
     * Upload a single evidence file. Mirrors the backend's accepted MIME types
     * + 10MB cap so a doomed request doesn't even hit the network.
     *
     * Whitelist matches the backend `UploadDisputeEvidenceCommand` validator —
     * if it grows there, mirror it here.
     *
     * On success, triggers [load] to refresh the dispute and pick up the
     * persisted evidence row. Multi-file callers should `await` each upload
     * sequentially (the screen's launcher does this in a single coroutine) so
     * we don't race overlapping reloads.
     */
    fun uploadEvidence(bytes: ByteArray, fileName: String, mimeType: String) {
        val id = disputeId ?: return
        if (bytes.size > MAX_EVIDENCE_BYTES) {
            snackbar.showError(appContext.getString(R.string.dispute_evidence_too_large))
            return
        }
        if (mimeType.lowercase() !in ALLOWED_EVIDENCE_MIME_TYPES) {
            snackbar.showError(appContext.getString(R.string.dispute_evidence_unsupported_type))
            return
        }
        viewModelScope.launch {
            _uploadingEvidence.value = true
            val response = disputeRepository.uploadEvidence(id, bytes, fileName, mimeType)
            _uploadingEvidence.value = false
            if (response != null) {
                // Re-fetch detail to pick up the persisted evidence row. The
                // returned DTO carries the SAS-signed blobUrl too, but going
                // through load() also picks up any other thread changes that
                // happened on the server side meanwhile.
                load()
            }
        }
    }

    companion object {
        private const val MAX_EVIDENCE_BYTES: Int = 10 * 1024 * 1024
        private val ALLOWED_EVIDENCE_MIME_TYPES: Set<String> = setOf(
            "image/jpeg",
            "image/jpg",
            "image/png",
            "image/webp",
            "application/pdf",
        )
    }
}
