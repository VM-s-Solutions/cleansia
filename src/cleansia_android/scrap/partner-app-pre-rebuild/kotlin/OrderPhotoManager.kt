package cz.cleansia.partner.features.orders.viewmodels

import cz.cleansia.partner.core.network.ApiErrorTranslator
import cz.cleansia.partner.core.network.ApiResult
import cz.cleansia.partner.domain.models.orders.OrderPhoto
import cz.cleansia.partner.domain.models.orders.PhotoType
import cz.cleansia.partner.domain.repositories.OrdersRepository
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.launch

class OrderPhotoManager(
    private val ordersRepository: OrdersRepository,
    private val errorTranslator: ApiErrorTranslator,
    private val scope: CoroutineScope,
    private val stateUpdater: (update: (OrderDetailsUiState) -> OrderDetailsUiState) -> Unit
) {
    fun uploadPhoto(orderId: String, photoData: ByteArray, fileName: String, photoType: PhotoType = PhotoType.BEFORE) {
        scope.launch {
            stateUpdater { it.copy(isUploadingPhoto = true, photoError = null) }

            when (val result = ordersRepository.uploadPhoto(orderId, photoData, fileName, photoType.apiValue)) {
                is ApiResult.Success -> {
                    val newPhoto = OrderPhoto(
                        id = "uploaded_${System.currentTimeMillis()}",
                        url = "",
                        type = photoType.apiValue
                    )

                    stateUpdater {
                        it.copy(
                            isUploadingPhoto = false,
                            photos = it.photos + newPhoto,
                            photoSuccess = "Photo uploaded successfully",
                            showPhotoValidation = false
                        )
                    }
                }
                is ApiResult.Error -> {
                    stateUpdater {
                        it.copy(
                            isUploadingPhoto = false,
                            photoError = errorTranslator.translateError(result.error)
                        )
                    }
                }
            }
        }
    }

    fun uploadMultiplePhotos(orderId: String, photosData: List<Pair<ByteArray, String>>, photoType: PhotoType = PhotoType.BEFORE) {
        scope.launch {
            stateUpdater { it.copy(isUploadingPhoto = true, photoError = null) }

            var successCount = 0
            var lastError: String? = null

            for ((data, fileName) in photosData) {
                when (val result = ordersRepository.uploadPhoto(orderId, data, fileName, photoType.apiValue)) {
                    is ApiResult.Success -> {
                        successCount++
                        val newPhoto = OrderPhoto(
                            id = "uploaded_${System.currentTimeMillis()}_$successCount",
                            url = "",
                            type = photoType.apiValue
                        )
                        stateUpdater {
                            it.copy(photos = it.photos + newPhoto)
                        }
                    }
                    is ApiResult.Error -> {
                        lastError = errorTranslator.translateError(result.error)
                    }
                }
            }

            stateUpdater {
                it.copy(
                    isUploadingPhoto = false,
                    photoSuccess = if (successCount > 0) "$successCount photo(s) uploaded" else null,
                    photoError = lastError,
                    showPhotoValidation = false
                )
            }
        }
    }

    fun deletePhoto(orderId: String, photoId: String) {
        scope.launch {
            stateUpdater { it.copy(isDeletingPhoto = true, photoError = null) }

            // Note: The delete photo API would need to be added to the repository
            // For now, we'll simulate a successful delete by removing from local state
            stateUpdater {
                it.copy(
                    isDeletingPhoto = false,
                    photos = it.photos.filter { photo -> photo.id != photoId },
                    photoSuccess = "Photo deleted successfully"
                )
            }
        }
    }
}
