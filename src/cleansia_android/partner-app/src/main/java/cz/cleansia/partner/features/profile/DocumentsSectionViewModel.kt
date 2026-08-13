package cz.cleansia.partner.features.profile

import android.content.Context
import android.net.Uri
import android.util.Base64
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.core.media.ImageCompressor
import cz.cleansia.core.media.isImageMimeType
import cz.cleansia.core.media.jpegFileName
import cz.cleansia.core.media.queryDisplayName
import cz.cleansia.core.media.queryMimeType
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.core.ui.state.ActionState
import cz.cleansia.partner.R
import cz.cleansia.partner.api.model.BlobFileDto
import cz.cleansia.partner.api.model.DocumentType
import cz.cleansia.partner.api.model.GetMyDocumentsMyDocumentDto
import cz.cleansia.partner.api.model.SaveMyDocumentsDocumentToSave
import cz.cleansia.partner.core.network.ApiErrorTranslator
import cz.cleansia.core.network.ApiResult
import cz.cleansia.partner.data.profile.ProfileRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import dagger.hilt.android.qualifiers.ApplicationContext
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import javax.inject.Inject

sealed interface DocumentsSectionUiState {
    data object Loading : DocumentsSectionUiState
    data object Error : DocumentsSectionUiState
    data class Loaded(val documents: List<GetMyDocumentsMyDocumentDto>) : DocumentsSectionUiState
}

/**
 * A picked file that has already been read and encoded, waiting for the user to
 * choose a document type in the upload dialog.
 *
 * Lives in the ViewModel rather than in `remember` on the screen because
 * producing it is now an off-main-thread job that outlives a recomposition and
 * can fail.
 */
internal data class PendingUpload(
    val fileName: String,
    val contentType: String,
    val base64: String,
)

@HiltViewModel
class DocumentsSectionViewModel @Inject constructor(
    private val profileRepository: ProfileRepository,
    private val errorTranslator: ApiErrorTranslator,
    private val snackbar: SnackbarController,
    @ApplicationContext private val appContext: Context,
) : ViewModel() {

    private val _uiState = MutableStateFlow<DocumentsSectionUiState>(DocumentsSectionUiState.Loading)
    val uiState: StateFlow<DocumentsSectionUiState> = _uiState.asStateFlow()

    private val _uploadState = MutableStateFlow<ActionState>(ActionState.Idle)
    val uploadState: StateFlow<ActionState> = _uploadState.asStateFlow()

    private val _deletingId = MutableStateFlow<String?>(null)
    val deletingId: StateFlow<String?> = _deletingId.asStateFlow()

    private val _pendingFile = MutableStateFlow<PendingUpload?>(null)
    internal val pendingFile: StateFlow<PendingUpload?> = _pendingFile.asStateFlow()

    /** True while a picked file is being read/compressed — drives the FAB spinner. */
    private val _isPreparing = MutableStateFlow(false)
    val isPreparing: StateFlow<Boolean> = _isPreparing.asStateFlow()

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

    /**
     * Read a picked file and stage it for the type-and-description dialog.
     *
     * All of this used to run inline in the picker callback, which the
     * `ActivityResultRegistry` dispatches on the main thread — so opening the
     * stream, reading every byte and base64-encoding them froze the UI — and
     * this picker has no MIME filter, so a 10 MB PDF got exactly that treatment.
     *
     * Images go through [ImageCompressor], which downscales them to 1920px and
     * re-encodes them as a JPEG carrying no EXIF block — no capture GPS, no
     * device serial. Everything else is uploaded **byte-identical**: this
     * picker accepts any type, and re-encoding a PDF or a Word file as a JPEG
     * would destroy it. [isImageMimeType] is the whole test, and it answers
     * false for a provider that declares no type at all, which is the safe
     * direction — a passthrough of an image still uploads, a JPEG re-encode of
     * a contract does not.
     */
    fun stageFile(uri: Uri) {
        // Guard and flag both flip before the launch: two picker callbacks are
        // delivered on the main thread before either coroutine body starts, so
        // a guard inside the launch would let both past.
        if (_isPreparing.value) return
        _isPreparing.value = true
        viewModelScope.launch {
            val prepared = prepare(uri)
            _isPreparing.value = false
            if (prepared == null) {
                // The read used to fail silently here, which after a file pick
                // reads as the app ignoring the tap. Compression adds a second
                // way to fail (an undecodable image), so it has to be visible.
                snackbar.showError(appContext.getString(R.string.document_read_failed))
                return@launch
            }
            _pendingFile.value = prepared
        }
    }

    /** Drops the staged file — dialog dismissed, or its upload has been fired. */
    fun clearPendingFile() {
        _pendingFile.value = null
    }

    private suspend fun prepare(uri: Uri): PendingUpload? {
        val (displayName, mimeType) = withContext(Dispatchers.IO) {
            queryDisplayName(appContext, uri) to queryMimeType(appContext, uri)
        }
        if (isImageMimeType(mimeType)) {
            val encoded = ImageCompressor.compressToBase64(appContext.contentResolver, uri)
                ?: return null
            return PendingUpload(
                // The user's own name with a .jpg extension, not the
                // compressor's generic photo.jpg: this list renders the name.
                fileName = jpegFileName(displayName),
                contentType = encoded.contentType,
                base64 = encoded.base64,
            )
        }
        // Passthrough. Still off the main thread, and still base64-encoded in
        // the same hop as the read — the base64 of a large PDF is itself the
        // expensive half.
        val base64 = withContext(Dispatchers.IO) {
            runCatching {
                appContext.contentResolver.openInputStream(uri)?.use {
                    Base64.encodeToString(it.readBytes(), Base64.NO_WRAP)
                }
            }.getOrNull()
        } ?: return null
        return PendingUpload(
            fileName = displayName ?: DEFAULT_DOCUMENT_FILE_NAME,
            contentType = mimeType ?: DEFAULT_DOCUMENT_CONTENT_TYPE,
            base64 = base64,
        )
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

    private companion object {
        /** Fallbacks for a provider that reports neither name nor type. */
        const val DEFAULT_DOCUMENT_FILE_NAME = "document"
        const val DEFAULT_DOCUMENT_CONTENT_TYPE = "application/octet-stream"
    }
}
