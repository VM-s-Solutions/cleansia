package cz.cleansia.partner.features.profile

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.core.ui.state.ActionState
import cz.cleansia.partner.R
import cz.cleansia.partner.api.model.LegalDocumentType
import cz.cleansia.partner.core.network.ApiErrorTranslator
import cz.cleansia.partner.core.settings.AppSettingsRepository
import cz.cleansia.partner.data.profile.CleanerLegalDocument
import cz.cleansia.partner.data.profile.ProfileRepository
import cz.cleansia.partner.features.orders.hasKey
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableSharedFlow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.SharedFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asSharedFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch
import javax.inject.Inject

sealed interface LegalDocumentsUiState {
    data object Loading : LegalDocumentsUiState
    data object Error : LegalDocumentsUiState
    data class Loaded(val documents: List<CleanerLegalDocument>) : LegalDocumentsUiState
}

enum class LegalDocumentNotice { TextUpdated }

/**
 * The cleaner's contract documents and the acceptance of each. `legal.document_not_in_force` is the
 * one refusal handled here: a newer version came into force while the old text was on screen, so the
 * list is re-read and the cleaner is told to read the document again before accepting.
 */
@HiltViewModel
class LegalDocumentsViewModel @Inject constructor(
    private val profileRepository: ProfileRepository,
    private val appSettingsRepository: AppSettingsRepository,
    private val errorTranslator: ApiErrorTranslator,
    private val snackbar: SnackbarController,
) : ViewModel() {

    private val _uiState = MutableStateFlow<LegalDocumentsUiState>(LegalDocumentsUiState.Loading)
    val uiState: StateFlow<LegalDocumentsUiState> = _uiState.asStateFlow()

    private val _actionState = MutableStateFlow<ActionState>(ActionState.Idle)
    val actionState: StateFlow<ActionState> = _actionState.asStateFlow()

    private val _notice = MutableStateFlow<LegalDocumentNotice?>(null)
    val notice: StateFlow<LegalDocumentNotice?> = _notice.asStateFlow()

    private val _accepted = MutableSharedFlow<LegalDocumentType>(extraBufferCapacity = 1)
    val accepted: SharedFlow<LegalDocumentType> = _accepted.asSharedFlow()

    init {
        load()
    }

    fun retry() = load()

    /** Clears what the last opened document left behind, so the next one opens clean. */
    fun onDocumentClosed() {
        if (_actionState.value is ActionState.Submitting) return
        _actionState.value = ActionState.Idle
        _notice.value = null
    }

    fun accept(document: CleanerLegalDocument) {
        if (_actionState.value is ActionState.Submitting) return
        _actionState.value = ActionState.Submitting
        viewModelScope.launch {
            when (val result = profileRepository.acceptLegalDocument(document.legalDocumentTextId)) {
                is ApiResult.Success -> {
                    _actionState.value = ActionState.Idle
                    _notice.value = null
                    snackbar.showSuccessKey(R.string.legal_documents_accepted_toast)
                    _accepted.emit(document.type)
                    load()
                }
                is ApiResult.Error ->
                    if (result.error.hasKey(NOT_IN_FORCE)) {
                        _notice.value = LegalDocumentNotice.TextUpdated
                        _actionState.value = ActionState.Idle
                        load()
                    } else {
                        _actionState.value = ActionState.Error(errorTranslator.translate(result.error))
                    }
            }
        }
    }

    /** A re-read keeps the list on screen, so an open document is replaced rather than closed. */
    private fun load() {
        viewModelScope.launch {
            if (_uiState.value !is LegalDocumentsUiState.Loaded) _uiState.value = LegalDocumentsUiState.Loading
            val language = appSettingsRepository.emailLanguageTag()
            when (val result = profileRepository.getLegalDocuments(language)) {
                is ApiResult.Success -> _uiState.value = LegalDocumentsUiState.Loaded(result.data)
                is ApiResult.Error ->
                    if (_uiState.value is LegalDocumentsUiState.Loaded) {
                        snackbar.showError(errorTranslator.translate(result.error))
                    } else {
                        _uiState.value = LegalDocumentsUiState.Error
                    }
            }
        }
    }

    private companion object {
        const val NOT_IN_FORCE = "legal.document_not_in_force"
    }
}
