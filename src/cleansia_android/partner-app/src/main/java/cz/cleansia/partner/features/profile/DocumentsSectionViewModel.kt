package cz.cleansia.partner.features.profile

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.core.ui.state.ActionState
import cz.cleansia.partner.api.model.BlobFileDto
import cz.cleansia.partner.api.model.DocumentType
import cz.cleansia.partner.api.model.GetMyDocumentsMyDocumentDto
import cz.cleansia.partner.api.model.SaveMyDocumentsDocumentToSave
import cz.cleansia.partner.core.network.ApiErrorTranslator
import cz.cleansia.core.network.ApiResult
import cz.cleansia.partner.data.profile.ProfileRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch
import javax.inject.Inject

sealed interface DocumentsSectionUiState {
    data object Loading : DocumentsSectionUiState
    data object Error : DocumentsSectionUiState
    data class Loaded(val documents: List<GetMyDocumentsMyDocumentDto>) : DocumentsSectionUiState
}

@HiltViewModel
class DocumentsSectionViewModel @Inject constructor(
    private val profileRepository: ProfileRepository,
    private val errorTranslator: ApiErrorTranslator,
    private val snackbar: SnackbarController,
) : ViewModel() {

    private val _uiState = MutableStateFlow<DocumentsSectionUiState>(DocumentsSectionUiState.Loading)
    val uiState: StateFlow<DocumentsSectionUiState> = _uiState.asStateFlow()

    private val _uploadState = MutableStateFlow<ActionState>(ActionState.Idle)
    val uploadState: StateFlow<ActionState> = _uploadState.asStateFlow()

    private val _deletingId = MutableStateFlow<String?>(null)
    val deletingId: StateFlow<String?> = _deletingId.asStateFlow()

    init { refresh() }

    fun refresh() {
        viewModelScope.launch {
            _uiState.value = DocumentsSectionUiState.Loading
            when (val result = profileRepository.getMyDocuments()) {
                is ApiResult.Success -> _uiState.value = DocumentsSectionUiState.Loaded(result.data)
                is ApiResult.Error -> {
                    snackbar.showError(errorTranslator.translate(result.error))
                    _uiState.value = DocumentsSectionUiState.Error
                }
            }
        }
    }

    fun upload(
        documentType: DocumentType,
        fileName: String,
        contentType: String,
        base64Content: String,
        description: String?,
    ) {
        if (_uploadState.value is ActionState.Submitting) return
        viewModelScope.launch {
            _uploadState.value = ActionState.Submitting
            val payload = listOf(
                SaveMyDocumentsDocumentToSave(
                    documentType = documentType,
                    file = BlobFileDto(
                        fileName = fileName,
                        base64Content = base64Content,
                        contentType = contentType,
                    ),
                    description = description?.takeIf { it.isNotBlank() },
                ),
            )
            when (val result = profileRepository.saveDocuments(payload)) {
                is ApiResult.Success -> {
                    _uploadState.value = ActionState.Idle
                    refresh()
                }
                is ApiResult.Error -> {
                    _uploadState.value = ActionState.Idle
                    snackbar.showError(errorTranslator.translate(result.error))
                }
            }
        }
    }

    fun delete(documentId: String) {
        if (_deletingId.value != null) return
        viewModelScope.launch {
            _deletingId.value = documentId
            when (val result = profileRepository.deleteDocument(documentId)) {
                is ApiResult.Success -> {
                    _deletingId.value = null
                    refresh()
                }
                is ApiResult.Error -> {
                    _deletingId.value = null
                    snackbar.showError(errorTranslator.translate(result.error))
                }
            }
        }
    }
}
