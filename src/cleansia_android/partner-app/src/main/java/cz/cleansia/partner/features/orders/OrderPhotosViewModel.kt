package cz.cleansia.partner.features.orders

import android.content.Context
import android.net.Uri
import androidx.lifecycle.SavedStateHandle
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.partner.api.model.GetOrderPhotosOrderPhotoDto
import cz.cleansia.partner.api.model.PhotoType
import cz.cleansia.core.media.ImageCompressor
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.partner.R
import cz.cleansia.partner.core.network.ApiErrorTranslator
import cz.cleansia.core.network.ApiResult
import cz.cleansia.partner.data.orders.OrdersRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import dagger.hilt.android.qualifiers.ApplicationContext
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import javax.inject.Inject

sealed interface OrderPhotosUiState {
    data object Loading : OrderPhotosUiState
    data object Error : OrderPhotosUiState
    data class Loaded(val photos: List<GetOrderPhotosOrderPhotoDto>) : OrderPhotosUiState
}

/**
 * Per-rail mutation substate kept alongside the loaded photo list:
 * [isUploading] drives the add-tile spinner, [deletingId] drives the
 * spinner on the specific tile being removed.
 */
data class PhotoMutationState(
    val isUploading: Boolean = false,
    val deletingId: String? = null,
)

@HiltViewModel
class OrderPhotosViewModel @Inject constructor(
    savedStateHandle: SavedStateHandle,
    private val ordersRepository: OrdersRepository,
    private val errorTranslator: ApiErrorTranslator,
    private val snackbar: SnackbarController,
    @ApplicationContext private val appContext: Context,
) : ViewModel() {

    private val orderId: String = savedStateHandle.get<String>("orderId")
        ?: error("orderId required for OrderPhotos VM")

    private val _uiState = MutableStateFlow<OrderPhotosUiState>(OrderPhotosUiState.Loading)
    val uiState: StateFlow<OrderPhotosUiState> = _uiState.asStateFlow()

    private val _mutation = MutableStateFlow(PhotoMutationState())
    val mutation: StateFlow<PhotoMutationState> = _mutation.asStateFlow()

    /**
     * Monotonic counter that bumps once whenever the server state changed
     * (successful upload or delete). The parent screen reads it via
     * `LaunchedEffect` to refresh the surrounding OrderItem — needed so
     * `hasAfterPhotos` stays live as photos are added/removed (drives the
     * Complete-slide gate).
     */
    private val _mutationVersion = MutableStateFlow(0)
    val mutationVersion: StateFlow<Int> = _mutationVersion.asStateFlow()

    init { refresh() }

    fun refresh() {
        viewModelScope.launch {
            if (_uiState.value !is OrderPhotosUiState.Loaded) {
                _uiState.value = OrderPhotosUiState.Loading
            }
            when (val result = ordersRepository.getPhotos(orderId)) {
                is ApiResult.Success ->
                    _uiState.value = OrderPhotosUiState.Loaded(result.data.photos.orEmpty())
                is ApiResult.Error -> {
                    snackbar.showError(errorTranslator.translate(result.error))
                    if (_uiState.value !is OrderPhotosUiState.Loaded) {
                        _uiState.value = OrderPhotosUiState.Error
                    }
                }
            }
        }
    }

    /**
     * Downscale, strip the metadata from and upload the picked photo.
     *
     * The picker callback hands over a [Uri] and nothing else: reading and
     * encoding the file used to happen inline in that callback, which the
     * `ActivityResultRegistry` dispatches on the main thread, so a multi-MB
     * photo froze the UI. All of it now happens inside [ImageCompressor],
     * which hops to `Dispatchers.Default` and base64-encodes in the same hop.
     *
     * [PhotoMutationState.isUploading] is set *before* the compression rather
     * than at the network call, so the add-tile spinner covers the whole
     * operation. Left at the network call the cleaner gets a second of dead tap
     * — the same freeze, just without the frame drops.
     *
     * Single-flight guarded to match iOS (`OrderPhotosViewModel.swift:53`):
     * the spinner replaces the add tile, but a fast double-tap can still land
     * two picks, and the second would overwrite the first's spinner state. Both
     * the check and the flag flip happen *before* the launch, synchronously on
     * the main thread — two picker callbacks are both delivered before either
     * coroutine body starts, so a guard inside the launch would let both past.
     */
    fun compressAndUpload(type: PhotoType, uri: Uri) {
        if (_mutation.value.isUploading) return
        _mutation.update { it.copy(isUploading = true) }
        viewModelScope.launch {
            val encoded = ImageCompressor.compressToBase64(appContext.contentResolver, uri)
            if (encoded == null) {
                // Unreadable, corrupt, or a format this API level can't decode
                // (HEIC below API 28). Must be visible: the tile has been
                // spinning, and a silent reset reads as the app ignoring the tap.
                snackbar.showError(appContext.getString(R.string.photo_encode_failed))
                _mutation.update { it.copy(isUploading = false) }
                return@launch
            }
            val result = ordersRepository.uploadPhoto(
                orderId = orderId,
                photoType = type,
                fileName = encoded.fileName,
                contentType = encoded.contentType,
                base64Content = encoded.base64,
            )
            when (result) {
                is ApiResult.Success -> {
                    _mutation.update { it.copy(isUploading = false) }
                    _mutationVersion.update { it + 1 }
                    refresh()
                }
                is ApiResult.Error -> {
                    snackbar.showError(errorTranslator.translate(result.error))
                    _mutation.update { it.copy(isUploading = false) }
                }
            }
        }
    }

    fun delete(photoId: String) {
        viewModelScope.launch {
            _mutation.update { it.copy(deletingId = photoId) }
            val result = ordersRepository.deletePhoto(photoId)
            when (result) {
                is ApiResult.Success -> {
                    _mutation.update { it.copy(deletingId = null) }
                    _mutationVersion.update { it + 1 }
                    refresh()
                }
                is ApiResult.Error -> {
                    snackbar.showError(errorTranslator.translate(result.error))
                    _mutation.update { it.copy(deletingId = null) }
                }
            }
        }
    }
}
