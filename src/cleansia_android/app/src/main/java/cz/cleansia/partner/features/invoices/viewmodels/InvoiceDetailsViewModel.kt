package cz.cleansia.partner.features.invoices.viewmodels

import android.content.ContentValues
import android.content.Context
import android.os.Build
import android.os.Environment
import android.provider.MediaStore
import androidx.lifecycle.SavedStateHandle
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.partner.core.network.ApiResult
import cz.cleansia.partner.domain.models.invoices.InvoiceDetail
import cz.cleansia.partner.domain.repositories.InvoicesRepository
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
import java.io.FileOutputStream
import javax.inject.Inject

data class InvoiceDetailsUiState(
    val isLoading: Boolean = false,
    val isDownloading: Boolean = false,
    val error: String? = null,
    val downloadError: String? = null,
    val invoice: InvoiceDetail? = null,
    val downloadSuccess: Boolean = false,
    val downloadedFilePath: String? = null
)

@HiltViewModel
class InvoiceDetailsViewModel @Inject constructor(
    savedStateHandle: SavedStateHandle,
    private val invoicesRepository: InvoicesRepository,
    @ApplicationContext private val context: Context
) : ViewModel() {

    private val invoiceId: String = savedStateHandle.get<String>("invoiceId") ?: ""

    private val _uiState = MutableStateFlow(InvoiceDetailsUiState())
    val uiState: StateFlow<InvoiceDetailsUiState> = _uiState.asStateFlow()

    init {
        loadInvoiceDetails()
    }

    fun loadInvoiceDetails() {
        if (invoiceId.isBlank()) {
            _uiState.update { it.copy(error = "Invalid invoice ID") }
            return
        }

        viewModelScope.launch {
            _uiState.update { it.copy(isLoading = true, error = null) }

            when (val result = invoicesRepository.getInvoiceById(invoiceId)) {
                is ApiResult.Success -> {
                    _uiState.update {
                        it.copy(
                            isLoading = false,
                            invoice = result.data
                        )
                    }
                }
                is ApiResult.Error -> {
                    _uiState.update {
                        it.copy(
                            isLoading = false,
                            error = result.error.getUserMessage()
                        )
                    }
                }
            }
        }
    }

    fun downloadInvoice() {
        val invoice = _uiState.value.invoice ?: return

        viewModelScope.launch {
            _uiState.update { it.copy(isDownloading = true, downloadError = null) }

            when (val result = invoicesRepository.downloadInvoicePdf(invoiceId)) {
                is ApiResult.Success -> {
                    try {
                        val fileName = "Invoice_${invoice.invoiceNumber}.pdf"
                        val filePath = withContext(Dispatchers.IO) {
                            savePdfToDownloads(result.data.bytes(), fileName)
                        }
                        _uiState.update {
                            it.copy(
                                isDownloading = false,
                                downloadSuccess = true,
                                downloadedFilePath = filePath
                            )
                        }
                    } catch (e: Exception) {
                        _uiState.update {
                            it.copy(
                                isDownloading = false,
                                downloadError = "Failed to save file: ${e.message}"
                            )
                        }
                    }
                }
                is ApiResult.Error -> {
                    _uiState.update {
                        it.copy(
                            isDownloading = false,
                            downloadError = result.error.getUserMessage()
                        )
                    }
                }
            }
        }
    }

    private fun savePdfToDownloads(bytes: ByteArray, fileName: String): String {
        return if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
            // For Android 10 and above, use MediaStore
            val contentValues = ContentValues().apply {
                put(MediaStore.Downloads.DISPLAY_NAME, fileName)
                put(MediaStore.Downloads.MIME_TYPE, "application/pdf")
                put(MediaStore.Downloads.IS_PENDING, 1)
            }

            val resolver = context.contentResolver
            val uri = resolver.insert(MediaStore.Downloads.EXTERNAL_CONTENT_URI, contentValues)
                ?: throw Exception("Failed to create file")

            resolver.openOutputStream(uri)?.use { outputStream ->
                outputStream.write(bytes)
            } ?: throw Exception("Failed to open output stream")

            contentValues.clear()
            contentValues.put(MediaStore.Downloads.IS_PENDING, 0)
            resolver.update(uri, contentValues, null, null)

            uri.toString()
        } else {
            // For older Android versions
            val downloadsDir = Environment.getExternalStoragePublicDirectory(Environment.DIRECTORY_DOWNLOADS)
            val file = File(downloadsDir, fileName)
            FileOutputStream(file).use { outputStream ->
                outputStream.write(bytes)
            }
            file.absolutePath
        }
    }

    fun clearError() {
        _uiState.update { it.copy(error = null, downloadError = null) }
    }

    fun clearDownloadSuccess() {
        _uiState.update { it.copy(downloadSuccess = false) }
    }
}
