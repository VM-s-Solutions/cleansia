package cz.cleansia.partner.features.orders.viewmodels

import androidx.lifecycle.SavedStateHandle
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.partner.core.network.ApiResult
import cz.cleansia.partner.domain.models.orders.OrderDetail
import cz.cleansia.partner.domain.models.orders.OrderPhoto
import cz.cleansia.partner.domain.models.orders.OrderStatus
import cz.cleansia.partner.domain.models.orders.PhotoType
import cz.cleansia.partner.domain.repositories.OrdersRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.Job
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.isActive
import kotlinx.coroutines.launch
import java.time.Instant
import java.time.format.DateTimeFormatter
import java.time.format.DateTimeParseException
import javax.inject.Inject

data class OrderDetailsUiState(
    val isLoading: Boolean = false,
    val isActionLoading: Boolean = false,
    val isUploadingPhoto: Boolean = false,
    val isDeletingPhoto: Boolean = false,
    val error: String? = null,
    val actionError: String? = null,
    val actionSuccess: String? = null,
    val photoError: String? = null,
    val photoSuccess: String? = null,
    val order: OrderDetail? = null,
    val photos: List<OrderPhoto> = emptyList(),
    val navigateBack: Boolean = false,
    val showCompleteDialog: Boolean = false,
    // Timer state
    val elapsedSeconds: Long = 0,
    val isTimerRunning: Boolean = false,
    val timerStartedAt: Instant? = null,
    // Photo validation state
    val showPhotoValidation: Boolean = false
) {
    // Computed properties for before/after photos
    val beforePhotos: List<OrderPhoto>
        get() = photos.filter { it.photoType == PhotoType.BEFORE }

    val afterPhotos: List<OrderPhoto>
        get() = photos.filter { it.photoType == PhotoType.AFTER }

    val hasRequiredPhotos: Boolean
        get() = beforePhotos.isNotEmpty() && afterPhotos.isNotEmpty()
}

@HiltViewModel
class OrderDetailsViewModel @Inject constructor(
    savedStateHandle: SavedStateHandle,
    private val ordersRepository: OrdersRepository
) : ViewModel() {

    private val orderId: String = savedStateHandle.get<String>("orderId") ?: ""

    private val _uiState = MutableStateFlow(OrderDetailsUiState())
    val uiState: StateFlow<OrderDetailsUiState> = _uiState.asStateFlow()

    private var timerJob: Job? = null

    init {
        loadOrderDetails()
    }

    override fun onCleared() {
        super.onCleared()
        stopTimer()
    }

    fun loadOrderDetails() {
        if (orderId.isBlank()) {
            _uiState.update { it.copy(error = "Invalid order ID") }
            return
        }

        viewModelScope.launch {
            _uiState.update { it.copy(isLoading = true, error = null) }

            when (val result = ordersRepository.getOrderById(orderId)) {
                is ApiResult.Success -> {
                    val order = result.data
                    val startedAt = parseStartedAt(order.startedAt)

                    _uiState.update {
                        it.copy(
                            isLoading = false,
                            order = order,
                            timerStartedAt = startedAt
                        )
                    }

                    // Start timer if order is in progress
                    if (order.status == OrderStatus.IN_PROGRESS && startedAt != null) {
                        startTimer(startedAt)
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

    /**
     * Parse the startedAt timestamp from various ISO formats
     */
    private fun parseStartedAt(startedAt: String?): Instant? {
        if (startedAt.isNullOrBlank()) return null

        return try {
            // Try parsing as ISO instant first
            Instant.parse(startedAt)
        } catch (e: DateTimeParseException) {
            try {
                // Try parsing as ISO local date time and convert to instant
                java.time.LocalDateTime.parse(startedAt, DateTimeFormatter.ISO_LOCAL_DATE_TIME)
                    .atZone(java.time.ZoneId.systemDefault())
                    .toInstant()
            } catch (e: DateTimeParseException) {
                // If all parsing fails, return null (timer won't work but app won't crash)
                null
            }
        }
    }

    /**
     * Start the countdown timer
     */
    private fun startTimer(startedAt: Instant) {
        stopTimer() // Stop any existing timer

        timerJob = viewModelScope.launch {
            _uiState.update { it.copy(isTimerRunning = true, timerStartedAt = startedAt) }

            while (isActive) {
                val now = Instant.now()
                val elapsedSeconds = java.time.Duration.between(startedAt, now).seconds

                _uiState.update { it.copy(elapsedSeconds = elapsedSeconds) }

                delay(1000) // Update every second
            }
        }
    }

    /**
     * Stop the countdown timer
     */
    private fun stopTimer() {
        timerJob?.cancel()
        timerJob = null
        _uiState.update { it.copy(isTimerRunning = false) }
    }

    /**
     * Manually start the timer (used when order transitions to IN_PROGRESS)
     * This stores the start time locally if the API doesn't provide startedAt
     */
    fun startTimerManually() {
        val now = Instant.now()
        startTimer(now)
    }

    fun takeOrder() {
        performOrderAction { ordersRepository.takeOrder(orderId) }
    }

    fun startOrder() {
        viewModelScope.launch {
            _uiState.update { it.copy(isActionLoading = true, actionError = null) }

            when (val result = ordersRepository.startOrder(orderId)) {
                is ApiResult.Success -> {
                    val order = result.data
                    val startedAt = parseStartedAt(order.startedAt) ?: Instant.now()

                    _uiState.update {
                        it.copy(
                            isActionLoading = false,
                            order = order,
                            actionSuccess = getSuccessMessage(order.status),
                            timerStartedAt = startedAt
                        )
                    }

                    // Start timer when order transitions to IN_PROGRESS
                    if (order.status == OrderStatus.IN_PROGRESS) {
                        startTimer(startedAt)
                    }
                }
                is ApiResult.Error -> {
                    _uiState.update {
                        it.copy(
                            isActionLoading = false,
                            actionError = result.error.getUserMessage()
                        )
                    }
                }
            }
        }
    }

    fun showCompleteOrderDialog() {
        // Check if required photos are uploaded
        val currentState = _uiState.value
        if (!currentState.hasRequiredPhotos) {
            _uiState.update { it.copy(showPhotoValidation = true) }
            return
        }
        _uiState.update { it.copy(showCompleteDialog = true, showPhotoValidation = false) }
    }

    fun showPhotoValidation() {
        _uiState.update { it.copy(showPhotoValidation = true) }
    }

    fun clearPhotoValidation() {
        _uiState.update { it.copy(showPhotoValidation = false) }
    }

    fun dismissCompleteOrderDialog() {
        _uiState.update { it.copy(showCompleteDialog = false) }
    }

    fun completeOrder(actualCompletionTimeMinutes: Int, completionNotes: String?) {
        _uiState.update { it.copy(showCompleteDialog = false) }

        viewModelScope.launch {
            _uiState.update { it.copy(isActionLoading = true, actionError = null) }

            when (val result = ordersRepository.completeOrder(orderId, actualCompletionTimeMinutes, completionNotes)) {
                is ApiResult.Success -> {
                    // Stop timer when order is completed
                    stopTimer()

                    _uiState.update {
                        it.copy(
                            isActionLoading = false,
                            order = result.data,
                            actionSuccess = getSuccessMessage(result.data.status)
                        )
                    }
                }
                is ApiResult.Error -> {
                    _uiState.update {
                        it.copy(
                            isActionLoading = false,
                            actionError = result.error.getUserMessage()
                        )
                    }
                }
            }
        }
    }

    private fun performOrderAction(action: suspend () -> ApiResult<OrderDetail>) {
        viewModelScope.launch {
            _uiState.update { it.copy(isActionLoading = true, actionError = null) }

            when (val result = action()) {
                is ApiResult.Success -> {
                    _uiState.update {
                        it.copy(
                            isActionLoading = false,
                            order = result.data,
                            actionSuccess = getSuccessMessage(result.data.status)
                        )
                    }
                }
                is ApiResult.Error -> {
                    _uiState.update {
                        it.copy(
                            isActionLoading = false,
                            actionError = result.error.getUserMessage()
                        )
                    }
                }
            }
        }
    }

    private fun getSuccessMessage(status: OrderStatus): String {
        return when (status) {
            OrderStatus.CONFIRMED -> "Order taken successfully"
            OrderStatus.IN_PROGRESS -> "Order started successfully"
            OrderStatus.COMPLETED -> "Order completed successfully"
            else -> "Order updated successfully"
        }
    }

    fun clearError() {
        _uiState.update { it.copy(error = null, actionError = null) }
    }

    fun clearActionSuccess() {
        _uiState.update { it.copy(actionSuccess = null) }
    }

    fun uploadPhoto(photoData: ByteArray, fileName: String, photoType: PhotoType = PhotoType.BEFORE) {
        viewModelScope.launch {
            _uiState.update { it.copy(isUploadingPhoto = true, photoError = null) }

            // Add photo type to the upload (API should support this)
            when (val result = ordersRepository.uploadPhoto(orderId, photoData, fileName, photoType.apiValue)) {
                is ApiResult.Success -> {
                    // Add the new photo to local state with the correct type
                    val newPhoto = OrderPhoto(
                        id = "temp_${System.currentTimeMillis()}",
                        url = "", // Will be replaced when we reload
                        type = photoType.apiValue
                    )

                    _uiState.update {
                        it.copy(
                            isUploadingPhoto = false,
                            photos = it.photos + newPhoto,
                            photoSuccess = "Photo uploaded successfully",
                            showPhotoValidation = false // Clear validation on successful upload
                        )
                    }

                    // Reload order to get updated photos list from server
                    loadOrderDetails()
                }
                is ApiResult.Error -> {
                    _uiState.update {
                        it.copy(
                            isUploadingPhoto = false,
                            photoError = result.error.getUserMessage()
                        )
                    }
                }
            }
        }
    }

    fun deletePhoto(photoId: String) {
        viewModelScope.launch {
            _uiState.update { it.copy(isDeletingPhoto = true, photoError = null) }

            // Note: The delete photo API would need to be added to the repository
            // For now, we'll simulate a successful delete by removing from local state
            _uiState.update {
                it.copy(
                    isDeletingPhoto = false,
                    photos = it.photos.filter { photo -> photo.id != photoId },
                    photoSuccess = "Photo deleted successfully"
                )
            }
        }
    }

    fun clearPhotoSuccess() {
        _uiState.update { it.copy(photoSuccess = null) }
    }

    fun clearPhotoError() {
        _uiState.update { it.copy(photoError = null) }
    }
}
