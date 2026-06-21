package cz.cleansia.partner.features.invoices

import android.content.Context
import android.net.Uri
import androidx.lifecycle.SavedStateHandle
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import androidx.core.content.FileProvider
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.partner.R
import cz.cleansia.partner.api.model.EmployeeInvoiceDetailDto
import cz.cleansia.partner.core.network.ApiErrorTranslator
import cz.cleansia.core.network.ApiResult
import cz.cleansia.partner.data.invoices.InvoicesRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import dagger.hilt.android.qualifiers.ApplicationContext
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import java.io.File
import javax.inject.Inject

sealed interface InvoiceDetailUiState {
    data object Loading : InvoiceDetailUiState
    data object Error : InvoiceDetailUiState
    data class Loaded(val invoice: EmployeeInvoiceDetailDto) : InvoiceDetailUiState
}

sealed interface DownloadState {
    data object Idle : DownloadState
    data object Downloading : DownloadState
    data class Ready(val uri: Uri) : DownloadState
}

@HiltViewModel
class InvoiceDetailViewModel @Inject constructor(
    savedStateHandle: SavedStateHandle,
    private val invoicesRepository: InvoicesRepository,
    private val errorTranslator: ApiErrorTranslator,
    private val snackbar: SnackbarController,
    @ApplicationContext private val appContext: Context,
) : ViewModel() {

    private val invoiceId: String = savedStateHandle.get<String>("invoiceId")
        ?: error("invoiceId required for InvoiceDetail")

    private val _uiState = MutableStateFlow<InvoiceDetailUiState>(InvoiceDetailUiState.Loading)
    val uiState: StateFlow<InvoiceDetailUiState> = _uiState.asStateFlow()

    private val _downloadState = MutableStateFlow<DownloadState>(DownloadState.Idle)
    val downloadState: StateFlow<DownloadState> = _downloadState.asStateFlow()

    init { refresh() }

    fun refresh() {
        viewModelScope.launch {
            if (_uiState.value !is InvoiceDetailUiState.Loaded) {
                _uiState.value = InvoiceDetailUiState.Loading
            }
            when (val result = invoicesRepository.getById(invoiceId)) {
                is ApiResult.Success -> _uiState.value = InvoiceDetailUiState.Loaded(result.data)
                is ApiResult.Error -> {
                    snackbar.showError(errorTranslator.translate(result.error))
                    if (_uiState.value !is InvoiceDetailUiState.Loaded) {
                        _uiState.value = InvoiceDetailUiState.Error
                    }
                }
            }
        }
    }

    /**
     * Streams the PDF to the app's cache dir, then exposes a FileProvider URI
     * the screen hands to `Intent.ACTION_VIEW`. Matches the design decision:
     * no MediaStore.Downloads complexity, no storage permission, system PDF
     * viewer handles save/share from there.
     */
    fun download() {
        viewModelScope.launch {
            _downloadState.value = DownloadState.Downloading
            when (val result = invoicesRepository.downloadPdf(invoiceId)) {
                is ApiResult.Success -> {
                    val uri = withContext(Dispatchers.IO) {
                        val pdfDir = File(appContext.cacheDir, "invoices").apply { mkdirs() }
                        val number = (_uiState.value as? InvoiceDetailUiState.Loaded)
                            ?.invoice?.invoiceNumber ?: invoiceId
                        val file = File(pdfDir, "invoice-$number.pdf")
                        result.data.byteStream().use { input ->
                            file.outputStream().use { output -> input.copyTo(output) }
                        }
                        FileProvider.getUriForFile(
                            appContext,
                            "${appContext.packageName}.fileprovider",
                            file,
                        )
                    }
                    _downloadState.value = DownloadState.Ready(uri)
                }
                is ApiResult.Error -> {
                    snackbar.showError(errorTranslator.translate(result.error))
                    _downloadState.value = DownloadState.Idle
                }
            }
        }
    }

    fun clearDownloadedUri() {
        _downloadState.value = DownloadState.Idle
    }

    fun notifyCopied() = snackbar.showSuccessKey(R.string.invoice_field_copied)

    fun notifyNoPdfViewer() = snackbar.showErrorKey(R.string.no_pdf_viewer)
}
