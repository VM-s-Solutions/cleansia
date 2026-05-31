package cz.cleansia.partner.features.invoices.viewmodels

import android.content.Context
import androidx.lifecycle.SavedStateHandle
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import androidx.core.content.FileProvider
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.partner.R
import cz.cleansia.partner.api.model.EmployeeInvoiceDetailDto
import cz.cleansia.partner.core.network.ApiErrorTranslator
import cz.cleansia.partner.core.network.ApiResult
import cz.cleansia.partner.data.invoices.InvoicesRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import dagger.hilt.android.qualifiers.ApplicationContext
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import java.io.File
import javax.inject.Inject

data class InvoiceDetailsUiState(
    val isLoading: Boolean = false,
    val invoice: EmployeeInvoiceDetailDto? = null,
    val isDownloading: Boolean = false,
    val downloadedFileUri: android.net.Uri? = null,
    val error: String? = null,
)

@HiltViewModel
class InvoiceDetailsViewModel @Inject constructor(
    savedStateHandle: SavedStateHandle,
    private val invoicesRepository: InvoicesRepository,
    private val errorTranslator: ApiErrorTranslator,
    private val snackbar: SnackbarController,
    @ApplicationContext private val appContext: Context,
) : ViewModel() {

    private val invoiceId: String = savedStateHandle.get<String>("invoiceId")
        ?: error("invoiceId required for InvoiceDetails")

    private val _uiState = MutableStateFlow(InvoiceDetailsUiState())
    val uiState: StateFlow<InvoiceDetailsUiState> = _uiState.asStateFlow()

    init { refresh() }

    fun refresh() {
        viewModelScope.launch {
            _uiState.update { it.copy(isLoading = true, error = null) }
            when (val result = invoicesRepository.getById(invoiceId)) {
                is ApiResult.Success -> _uiState.update { it.copy(isLoading = false, invoice = result.data) }
                is ApiResult.Error -> {
                    snackbar.showError(errorTranslator.translate(result.error))
                    _uiState.update { it.copy(isLoading = false) }
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
            _uiState.update { it.copy(isDownloading = true, error = null) }
            when (val result = invoicesRepository.downloadPdf(invoiceId)) {
                is ApiResult.Success -> {
                    val uri = withContext(Dispatchers.IO) {
                        val pdfDir = File(appContext.cacheDir, "invoices").apply { mkdirs() }
                        val number = _uiState.value.invoice?.invoiceNumber ?: invoiceId
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
                    _uiState.update { it.copy(isDownloading = false, downloadedFileUri = uri) }
                }
                is ApiResult.Error -> {
                    snackbar.showError(errorTranslator.translate(result.error))
                    _uiState.update { it.copy(isDownloading = false) }
                }
            }
        }
    }

    fun clearDownloadedUri() = _uiState.update { it.copy(downloadedFileUri = null) }
    fun clearError() = _uiState.update { it.copy(error = null) }

    /** Fired by the screen when a reference field is copied to the clipboard. */
    fun notifyCopied() = snackbar.showSuccessKey(R.string.invoice_field_copied)

    /** Fired by the screen when no PDF viewer activity is available. */
    fun notifyNoPdfViewer() = snackbar.showErrorKey(R.string.no_pdf_viewer)
}
