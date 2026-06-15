package cz.cleansia.partner.features.orders.viewmodels

import androidx.lifecycle.SavedStateHandle
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.partner.R
import cz.cleansia.partner.core.auth.UserProfileStore
import cz.cleansia.partner.core.network.ApiErrorTranslator
import cz.cleansia.core.network.ApiResult
import cz.cleansia.partner.data.orders.OrdersRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.SharingStarted
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.map
import kotlinx.coroutines.flow.stateIn
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import javax.inject.Inject

/**
 * Backs the Notes & Issues section on Order Details: lists existing
 * notes/issues from the order detail payload, exposes Add / Edit / Delete
 * actions, and tracks per-row in-flight state so the right row can show
 * a spinner instead of locking the whole section.
 *
 * Reads notes/issues from a refresh callback rather than fetching them
 * itself — the parent [OrderDetailsViewModel] already pulls the full
 * order with notes/issues, so we let it own the source-of-truth list
 * and just push mutations through.
 */
data class OrderNotesUiState(
    val isSavingNote: Boolean = false,
    val isReportingIssue: Boolean = false,
    /** noteId or issueId currently being mutated (edit / delete). */
    val mutatingId: String? = null,
    val noteSaved: Boolean = false,
    val issueReported: Boolean = false,
    /**
     * Bumped on each successful mutation so the parent screen knows
     * to re-fetch the order (and pick up the new note/issue list).
     */
    val mutationVersion: Int = 0,
)

@HiltViewModel
class OrderNotesViewModel @Inject constructor(
    savedStateHandle: SavedStateHandle,
    private val ordersRepository: OrdersRepository,
    private val errorTranslator: ApiErrorTranslator,
    private val snackbar: SnackbarController,
    userProfileStore: UserProfileStore,
) : ViewModel() {

    private val orderId: String = savedStateHandle.get<String>("orderId")
        ?: error("orderId required for OrderNotes VM")

    private val _uiState = MutableStateFlow(OrderNotesUiState())
    val uiState: StateFlow<OrderNotesUiState> = _uiState.asStateFlow()

    /** Used to gate the edit/delete affordance per row to the author only. */
    val currentEmployeeId: StateFlow<String?> = userProfileStore.profile
        .map { it?.employeeId }
        .stateIn(viewModelScope, SharingStarted.Eagerly, null)

    fun addNote(content: String) {
        if (content.isBlank()) return
        viewModelScope.launch {
            _uiState.update { it.copy(isSavingNote = true) }
            when (val result = ordersRepository.addNote(orderId, content.trim())) {
                is ApiResult.Success -> {
                    snackbar.showSuccessKey(R.string.note_saved_toast)
                    _uiState.update {
                        it.copy(
                            isSavingNote = false,
                            noteSaved = true,
                            mutationVersion = it.mutationVersion + 1,
                        )
                    }
                }
                is ApiResult.Error -> {
                    snackbar.showError(errorTranslator.translate(result.error))
                    _uiState.update { it.copy(isSavingNote = false) }
                }
            }
        }
    }

    fun updateNote(noteId: String, content: String) {
        if (content.isBlank()) return
        viewModelScope.launch {
            _uiState.update { it.copy(mutatingId = noteId) }
            when (val result = ordersRepository.updateNote(orderId, noteId, content.trim())) {
                is ApiResult.Success -> {
                    snackbar.showSuccessKey(R.string.note_saved_toast)
                    _uiState.update {
                        it.copy(
                            mutatingId = null,
                            mutationVersion = it.mutationVersion + 1,
                        )
                    }
                }
                is ApiResult.Error -> {
                    snackbar.showError(errorTranslator.translate(result.error))
                    _uiState.update { it.copy(mutatingId = null) }
                }
            }
        }
    }

    fun deleteNote(noteId: String) {
        viewModelScope.launch {
            _uiState.update { it.copy(mutatingId = noteId) }
            when (val result = ordersRepository.deleteNote(orderId, noteId)) {
                is ApiResult.Success -> {
                    snackbar.showSuccessKey(R.string.note_deleted_toast)
                    _uiState.update {
                        it.copy(
                            mutatingId = null,
                            mutationVersion = it.mutationVersion + 1,
                        )
                    }
                }
                is ApiResult.Error -> {
                    snackbar.showError(errorTranslator.translate(result.error))
                    _uiState.update { it.copy(mutatingId = null) }
                }
            }
        }
    }

    fun reportIssue(content: String) {
        if (content.isBlank()) return
        viewModelScope.launch {
            _uiState.update { it.copy(isReportingIssue = true) }
            when (val result = ordersRepository.reportIssue(orderId, content.trim())) {
                is ApiResult.Success -> {
                    snackbar.showSuccessKey(R.string.issue_reported_toast)
                    _uiState.update {
                        it.copy(
                            isReportingIssue = false,
                            issueReported = true,
                            mutationVersion = it.mutationVersion + 1,
                        )
                    }
                }
                is ApiResult.Error -> {
                    snackbar.showError(errorTranslator.translate(result.error))
                    _uiState.update { it.copy(isReportingIssue = false) }
                }
            }
        }
    }

    fun updateIssue(issueId: String, description: String) {
        if (description.isBlank()) return
        viewModelScope.launch {
            _uiState.update { it.copy(mutatingId = issueId) }
            when (val result = ordersRepository.updateIssue(orderId, issueId, description.trim())) {
                is ApiResult.Success -> {
                    snackbar.showSuccessKey(R.string.issue_updated_toast)
                    _uiState.update {
                        it.copy(
                            mutatingId = null,
                            mutationVersion = it.mutationVersion + 1,
                        )
                    }
                }
                is ApiResult.Error -> {
                    snackbar.showError(errorTranslator.translate(result.error))
                    _uiState.update { it.copy(mutatingId = null) }
                }
            }
        }
    }

    fun deleteIssue(issueId: String) {
        viewModelScope.launch {
            _uiState.update { it.copy(mutatingId = issueId) }
            when (val result = ordersRepository.deleteIssue(orderId, issueId)) {
                is ApiResult.Success -> {
                    snackbar.showSuccessKey(R.string.issue_deleted_toast)
                    _uiState.update {
                        it.copy(
                            mutatingId = null,
                            mutationVersion = it.mutationVersion + 1,
                        )
                    }
                }
                is ApiResult.Error -> {
                    snackbar.showError(errorTranslator.translate(result.error))
                    _uiState.update { it.copy(mutatingId = null) }
                }
            }
        }
    }

    fun clearNoteSaved() = _uiState.update { it.copy(noteSaved = false) }
    fun clearIssueReported() = _uiState.update { it.copy(issueReported = false) }
}
