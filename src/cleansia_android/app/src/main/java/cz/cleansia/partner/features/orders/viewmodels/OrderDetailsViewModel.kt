package cz.cleansia.partner.features.orders.viewmodels

import android.content.Context
import android.util.Log
import androidx.lifecycle.SavedStateHandle
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.partner.core.network.ApiErrorTranslator
import cz.cleansia.partner.core.network.ApiResult
import cz.cleansia.partner.domain.models.orders.CodeValue
import cz.cleansia.partner.domain.models.orders.OrderDetail
import cz.cleansia.partner.domain.models.orders.OrderPhoto
import cz.cleansia.partner.domain.models.orders.OrderStatus
import cz.cleansia.partner.domain.models.orders.PhotoType
import cz.cleansia.partner.core.storage.TokenManager
import cz.cleansia.partner.domain.repositories.OrdersRepository
import cz.cleansia.partner.domain.repositories.ProfileRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import dagger.hilt.android.qualifiers.ApplicationContext
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import java.time.Instant
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
    // Timer state
    val elapsedSeconds: Long = 0,
    val isTimerRunning: Boolean = false,
    val timerStartedAt: Instant? = null,
    // Photo validation state
    val showPhotoValidation: Boolean = false,
    // Current employee assignment state
    val isCurrentEmployeeAssigned: Boolean = false,
    // Whether another order is already in progress (prevents starting this one)
    val hasOtherOrderInProgress: Boolean = false,
    // Optimistic UI syncing state
    val isSyncing: Boolean = false
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
    private val ordersRepository: OrdersRepository,
    private val profileRepository: ProfileRepository,
    private val tokenManager: TokenManager,
    @ApplicationContext private val appContext: Context,
    private val errorTranslator: ApiErrorTranslator
) : ViewModel() {

    private val orderId: String = savedStateHandle.get<String>("orderId") ?: ""

    private val _uiState = MutableStateFlow(OrderDetailsUiState())
    val uiState: StateFlow<OrderDetailsUiState> = _uiState.asStateFlow()

    private val timerManager = OrderTimerManager(
        appContext = appContext,
        scope = viewModelScope,
        onElapsedUpdate = { elapsed ->
            _uiState.update { it.copy(elapsedSeconds = elapsed) }
        },
        onTimerStateChange = { running, startedAt ->
            _uiState.update { it.copy(isTimerRunning = running, timerStartedAt = startedAt) }
        }
    )

    private val photoManager = OrderPhotoManager(
        ordersRepository = ordersRepository,
        errorTranslator = errorTranslator,
        scope = viewModelScope,
        stateUpdater = { update -> _uiState.update(update) }
    )

    init {
        loadOrderDetails()
    }

    override fun onCleared() {
        super.onCleared()
        Log.d("OrderDetailsVM", "onCleared() called - NOT stopping service")
        // Only cancel the local coroutine — do NOT stop the foreground service.
        // Navigation Compose can clear ViewModels during transition animations,
        // and stopping the service here would kill the notification prematurely.
        timerManager.cancel()
    }

    /**
     * Check if the current employee is assigned to the order.
     * Uses tokenManager.getUserId() as primary source (always available after login),
     * falls back to cached profile ID.
     */
    private suspend fun checkEmployeeAssignment(order: OrderDetail): Boolean {
        val employeeId = tokenManager.getUserId()
            ?: profileRepository.getCachedProfileSync()?.id
        if (employeeId.isNullOrBlank()) {
            Log.w("OrderDetailsVM", "checkAssignment: no employeeId available")
            return false
        }
        val assignedIds = order.assignedEmployees?.map { it.employeeId } ?: emptyList()
        val isAssigned = assignedIds.contains(employeeId)
        Log.d("OrderDetailsVM", "checkAssignment: myId=$employeeId assignedIds=$assignedIds isAssigned=$isAssigned")
        return isAssigned
    }

    /**
     * Check if the employee has another order already in progress.
     * Only relevant when viewing a CONFIRMED order (which could be started).
     */
    private suspend fun checkHasOtherOrderInProgress(currentOrder: OrderDetail): Boolean {
        if (currentOrder.status != OrderStatus.CONFIRMED) return false

        return when (val result = ordersRepository.getMyOrders(
            statuses = listOf(OrderStatus.IN_PROGRESS)
        )) {
            is ApiResult.Success -> result.data.orders.any { it.id != orderId }
            is ApiResult.Error -> false
        }
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
                    val startedAt = timerManager.parseStartedAt(order.startedAt)

                    // Check if current employee is assigned
                    val isAssigned = checkEmployeeAssignment(order)

                    // Check if another order is already in progress
                    val hasOtherInProgress = checkHasOtherOrderInProgress(order)

                    _uiState.update {
                        it.copy(
                            isLoading = false,
                            order = order,
                            timerStartedAt = startedAt,
                            isCurrentEmployeeAssigned = isAssigned,
                            hasOtherOrderInProgress = hasOtherInProgress
                        )
                    }

                    // Start timer if order is in progress
                    if (order.status == OrderStatus.IN_PROGRESS && startedAt != null) {
                        timerManager.startTimer(startedAt, order)
                    }
                }
                is ApiResult.Error -> {
                    _uiState.update {
                        it.copy(
                            isLoading = false,
                            error = errorTranslator.translateError(result.error)
                        )
                    }
                }
            }
        }
    }

    fun startTimerManually() {
        timerManager.startTimerManually(_uiState.value.order)
    }

    suspend fun takeOrder(): Boolean {
        val previousState = _uiState.value
        val previousOrder = previousState.order ?: run {
            _uiState.update { it.copy(actionError = "No order loaded") }
            return false
        }

        // Optimistic update: set status to CONFIRMED and mark as assigned
        val newOrderStatus = CodeValue(type = "OrderStatus", name = "Confirmed", value = 2)
        val updatedOrder = previousOrder.copy(orderStatus = newOrderStatus)
        _uiState.update {
            it.copy(
                order = updatedOrder,
                isCurrentEmployeeAssigned = true,
                isSyncing = true,
                actionError = null
            )
        }

        return when (val result = ordersRepository.takeOrder(orderId)) {
            is ApiResult.Success -> {
                _uiState.update { it.copy(isSyncing = false) }
                // Re-fetch order details to get updated state from server
                loadOrderDetailsAfterAction("Order taken successfully")
                true
            }
            is ApiResult.Error -> {
                // Rollback to previous state
                _uiState.update {
                    it.copy(
                        order = previousOrder,
                        isCurrentEmployeeAssigned = previousState.isCurrentEmployeeAssigned,
                        isSyncing = false,
                        actionError = errorTranslator.translateError(result.error)
                    )
                }
                false
            }
        }
    }

    suspend fun startOrder(): Boolean {
        val previousState = _uiState.value
        val previousOrder = previousState.order ?: run {
            _uiState.update { it.copy(actionError = "No order loaded") }
            return false
        }

        // Optimistic update: set status to IN_PROGRESS
        val newOrderStatus = CodeValue(type = "OrderStatus", name = "InProgress", value = 3)
        val updatedOrder = previousOrder.copy(orderStatus = newOrderStatus)
        _uiState.update {
            it.copy(
                order = updatedOrder,
                isSyncing = true,
                actionError = null
            )
        }

        return when (val result = ordersRepository.startOrder(orderId)) {
            is ApiResult.Success -> {
                _uiState.update { it.copy(isSyncing = false) }
                // Re-fetch order details to get updated state with startedAt
                loadOrderDetailsAfterAction("Order started successfully")
                true
            }
            is ApiResult.Error -> {
                // Rollback to previous state
                _uiState.update {
                    it.copy(
                        order = previousOrder,
                        isSyncing = false,
                        actionError = errorTranslator.translateError(result.error)
                    )
                }
                false
            }
        }
    }

    fun showPhotoValidation() {
        _uiState.update { it.copy(showPhotoValidation = true) }
    }

    fun clearPhotoValidation() {
        _uiState.update { it.copy(showPhotoValidation = false) }
    }

    suspend fun completeOrder(): Boolean {
        val previousState = _uiState.value
        val previousOrder = previousState.order ?: run {
            _uiState.update { it.copy(actionError = "No order loaded") }
            return false
        }

        // Auto-calculate actual completion time from the timer
        val elapsedSeconds = previousState.elapsedSeconds
        val actualMinutes = ((elapsedSeconds + 59) / 60).toInt().coerceAtLeast(1) // Round up, minimum 1 minute

        // Optimistic update: set status to COMPLETED
        val newOrderStatus = CodeValue(type = "OrderStatus", name = "Completed", value = 4)
        val updatedOrder = previousOrder.copy(orderStatus = newOrderStatus)
        _uiState.update {
            it.copy(
                order = updatedOrder,
                isSyncing = true,
                actionError = null
            )
        }

        return when (val result = ordersRepository.completeOrder(orderId, actualMinutes, null)) {
            is ApiResult.Success -> {
                // Stop timer when order is completed
                timerManager.stopTimer()

                _uiState.update { it.copy(isSyncing = false) }
                // Re-fetch order details to get updated state
                loadOrderDetailsAfterAction("Order completed successfully")
                true
            }
            is ApiResult.Error -> {
                // Rollback to previous state
                _uiState.update {
                    it.copy(
                        order = previousOrder,
                        isSyncing = false,
                        actionError = errorTranslator.translateError(result.error)
                    )
                }
                false
            }
        }
    }

    /**
     * Re-fetch order details after a successful action (take/start/complete).
     * The action endpoints return simple responses, so we need to fetch the full detail again.
     */
    private suspend fun loadOrderDetailsAfterAction(successMessage: String) {
        when (val result = ordersRepository.getOrderById(orderId)) {
            is ApiResult.Success -> {
                val order = result.data
                val startedAt = timerManager.parseStartedAt(order.startedAt)
                val isAssigned = checkEmployeeAssignment(order)

                _uiState.update {
                    it.copy(
                        isActionLoading = false,
                        order = order,
                        actionSuccess = successMessage,
                        timerStartedAt = startedAt,
                        isCurrentEmployeeAssigned = isAssigned
                    )
                }

                // Start timer if order transitioned to IN_PROGRESS
                if (order.status == OrderStatus.IN_PROGRESS && startedAt != null) {
                    timerManager.startTimer(startedAt, order)
                }
            }
            is ApiResult.Error -> {
                // Action succeeded but re-fetch failed - show success anyway
                _uiState.update {
                    it.copy(
                        isActionLoading = false,
                        actionSuccess = successMessage
                    )
                }
            }
        }
    }

    fun clearError() {
        _uiState.update { it.copy(error = null, actionError = null) }
    }

    fun clearActionSuccess() {
        _uiState.update { it.copy(actionSuccess = null) }
    }

    fun uploadPhoto(photoData: ByteArray, fileName: String, photoType: PhotoType = PhotoType.BEFORE) {
        photoManager.uploadPhoto(orderId, photoData, fileName, photoType)
    }

    fun uploadMultiplePhotos(photosData: List<Pair<ByteArray, String>>, photoType: PhotoType = PhotoType.BEFORE) {
        photoManager.uploadMultiplePhotos(orderId, photosData, photoType)
    }

    fun deletePhoto(photoId: String) {
        photoManager.deletePhoto(orderId, photoId)
    }

    fun clearPhotoSuccess() {
        _uiState.update { it.copy(photoSuccess = null) }
    }
    fun clearPhotoError() {
        _uiState.update { it.copy(photoError = null) }
    }

    fun addNote(content: String) {
        viewModelScope.launch {
            when (val result = ordersRepository.addNote(orderId, content)) {
                is ApiResult.Success -> {
                    // Re-fetch to get the note with proper ID from backend
                    loadOrderDetailsAfterAction("Note added successfully")
                }
                is ApiResult.Error -> {
                    _uiState.update { it.copy(actionError = errorTranslator.translateError(result.error)) }
                }
            }
        }
    }

    fun reportIssue(description: String) {
        viewModelScope.launch {
            when (val result = ordersRepository.reportIssue(orderId, description)) {
                is ApiResult.Success -> {
                    // Re-fetch to get the issue with proper ID from backend
                    loadOrderDetailsAfterAction("Issue reported successfully")
                }
                is ApiResult.Error -> {
                    _uiState.update { it.copy(actionError = errorTranslator.translateError(result.error)) }
                }
            }
        }
    }
}
