package cz.cleansia.partner.features.profile

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
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
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import javax.inject.Inject

data class DocumentsSectionUiState(
    val isLoading: Boolean = false,
    val isUploading: Boolean = false,
    val deletingId: String? = null,
    val documents: List<GetMyDocumentsMyDocumentDto> = emptyList(),
    val error: String? = null,
    val uploadSuccess: Boolean = false,
)

@HiltViewModel
class DocumentsSectionViewModel @Inject constructor(
    private val profileRepository: ProfileRepository,
    private val errorTranslator: ApiErrorTranslator,
) : ViewModel() {

    private val _uiState = MutableStateFlow(DocumentsSectionUiState())
    val uiState: StateFlow<DocumentsSectionUiState> = _uiState.asStateFlow()

    init { refresh() }

    fun refresh() {
        viewModelScope.launch {
            _uiState.update { it.copy(isLoading = true, error = null) }
            when (val result = profileRepository.getMyDocuments()) {
                is ApiResult.Success -> _uiState.update {
                    it.copy(isLoading = false, documents = result.data)
                }
                is ApiResult.Error -> _uiState.update {
                    it.copy(isLoading = false, error = errorTranslator.translate(result.error))
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
        viewModelScope.launch {
            _uiState.update { it.copy(isUploading = true, error = null) }
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
                    _uiState.update { it.copy(isUploading = false, uploadSuccess = true) }
                    refresh()
                }
                is ApiResult.Error -> _uiState.update {
                    it.copy(isUploading = false, error = errorTranslator.translate(result.error))
                }
            }
        }
    }

    fun delete(documentId: String) {
        viewModelScope.launch {
            _uiState.update { it.copy(deletingId = documentId, error = null) }
            when (val result = profileRepository.deleteDocument(documentId)) {
                is ApiResult.Success -> {
                    _uiState.update { it.copy(deletingId = null) }
                    refresh()
                }
                is ApiResult.Error -> _uiState.update {
                    it.copy(deletingId = null, error = errorTranslator.translate(result.error))
                }
            }
        }
    }

    fun clearError() = _uiState.update { it.copy(error = null) }
    fun clearUploadSuccess() = _uiState.update { it.copy(uploadSuccess = false) }
}
