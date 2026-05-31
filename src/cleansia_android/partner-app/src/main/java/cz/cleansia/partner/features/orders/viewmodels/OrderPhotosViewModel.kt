package cz.cleansia.partner.features.orders.viewmodels

import androidx.lifecycle.SavedStateHandle
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.partner.api.model.GetOrderPhotosOrderPhotoDto
import cz.cleansia.partner.api.model.PhotoType
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.partner.core.network.ApiErrorTranslator
import cz.cleansia.partner.core.network.ApiResult
import cz.cleansia.partner.data.orders.OrdersRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import javax.inject.Inject

data class OrderPhotosUiState(
    val isLoading: Boolean = false,
    val photos: List<GetOrderPhotosOrderPhotoDto> = emptyList(),
    val isUploading: Boolean = false,
    val deletingId: String? = null,
    val error: String? = null,
    /**
     * Monotonic counter that bumps once whenever the server state
     * changed (successful upload or delete). The parent screen reads
     * it via `LaunchedEffect` to trigger a refresh of the surrounding
     * OrderItem — needed so `hasAfterPhotos` stays live as photos
     * are added/removed (drives the Complete-slide gate).
     */
    val mutationVersion: Int = 0,
)

@HiltViewModel
class OrderPhotosViewModel @Inject constructor(
    savedStateHandle: SavedStateHandle,
    private val ordersRepository: OrdersRepository,
    private val errorTranslator: ApiErrorTranslator,
    private val snackbar: SnackbarController,
) : ViewModel() {

    private val orderId: String = savedStateHandle.get<String>("orderId")
        ?: error("orderId required for OrderPhotos VM")

    private val _uiState = MutableStateFlow(OrderPhotosUiState())
    val uiState: StateFlow<OrderPhotosUiState> = _uiState.asStateFlow()

    init { refresh() }

    fun refresh() {
        viewModelScope.launch {
            _uiState.update { it.copy(isLoading = true, error = null) }
            when (val result = ordersRepository.getPhotos(orderId)) {
                is ApiResult.Success -> _uiState.update {
                    it.copy(isLoading = false, photos = result.data.photos.orEmpty())
                }
                is ApiResult.Error -> {
                    snackbar.showError(errorTranslator.translate(result.error))
                    _uiState.update { it.copy(isLoading = false) }
                }
            }
        }
    }

    fun upload(
        type: PhotoType,
        fileName: String,
        contentType: String,
        base64Content: String,
    ) {
        viewModelScope.launch {
            _uiState.update { it.copy(isUploading = true, error = null) }
            val result = ordersRepository.uploadPhoto(
                orderId = orderId,
                photoType = type,
                fileName = fileName,
                contentType = contentType,
                base64Content = base64Content,
            )
            when (result) {
                is ApiResult.Success -> {
                    _uiState.update {
                        it.copy(isUploading = false, mutationVersion = it.mutationVersion + 1)
                    }
                    refresh()
                }
                is ApiResult.Error -> {
                    snackbar.showError(errorTranslator.translate(result.error))
                    _uiState.update { it.copy(isUploading = false) }
                }
            }
        }
    }

    fun delete(photoId: String) {
        viewModelScope.launch {
            _uiState.update { it.copy(deletingId = photoId, error = null) }
            val result = ordersRepository.deletePhoto(photoId)
            when (result) {
                is ApiResult.Success -> {
                    _uiState.update {
                        it.copy(deletingId = null, mutationVersion = it.mutationVersion + 1)
                    }
                    refresh()
                }
                is ApiResult.Error -> {
                    snackbar.showError(errorTranslator.translate(result.error))
                    _uiState.update { it.copy(deletingId = null) }
                }
            }
        }
    }

    fun clearError() = _uiState.update { it.copy(error = null) }
}
