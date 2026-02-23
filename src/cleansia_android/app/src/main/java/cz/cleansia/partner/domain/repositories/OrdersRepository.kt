package cz.cleansia.partner.domain.repositories

import cz.cleansia.partner.config.Constants
import cz.cleansia.partner.core.database.dao.OrderDao
import cz.cleansia.partner.core.database.entities.CachedOrder
import cz.cleansia.partner.core.network.ApiResult
import cz.cleansia.partner.core.network.ApiService
import cz.cleansia.partner.core.network.NetworkMonitor
import cz.cleansia.partner.core.network.safeApiCall
import cz.cleansia.partner.core.storage.TokenManager
import cz.cleansia.partner.domain.models.orders.CompleteOrderRequest
import cz.cleansia.partner.domain.models.orders.PhotoType
import cz.cleansia.partner.domain.models.orders.UploadOrderPhotoRequest
import cz.cleansia.partner.domain.models.orders.Order
import cz.cleansia.partner.domain.models.orders.OrderDetail
import cz.cleansia.partner.domain.models.orders.OrderFilter
import cz.cleansia.partner.domain.models.orders.OrderStatus
import cz.cleansia.partner.domain.models.orders.PagedOrderResponse
import cz.cleansia.partner.domain.models.orders.StartOrderRequest
import cz.cleansia.partner.domain.models.orders.TakeOrderRequest
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.map
import kotlinx.serialization.json.Json
import java.time.LocalDateTime
import java.time.format.DateTimeFormatter
import javax.inject.Inject
import javax.inject.Singleton

/**
 * Result containing orders and pagination info
 */
data class OrdersResult(
    val orders: List<Order>,
    val hasMore: Boolean
)

interface OrdersRepository {
    suspend fun getOrders(
        page: Int = 1,
        pageSize: Int = Constants.Pagination.DEFAULT_PAGE_SIZE,
        filter: OrderFilter? = null
    ): ApiResult<PagedOrderResponse>

    suspend fun getAvailableOrders(
        page: Int = 1,
        searchTerm: String? = null,
        startDate: String? = null,
        endDate: String? = null,
        sortBy: String? = null,
        sortDescending: Boolean? = null,
        paymentStatuses: List<cz.cleansia.partner.domain.models.orders.PaymentStatus> = emptyList()
    ): ApiResult<OrdersResult>

    suspend fun getMyOrders(
        page: Int = 1,
        statuses: List<OrderStatus>,
        searchTerm: String? = null,
        startDate: String? = null,
        endDate: String? = null,
        sortBy: String? = null,
        sortDescending: Boolean? = null,
        paymentStatuses: List<cz.cleansia.partner.domain.models.orders.PaymentStatus> = emptyList()
    ): ApiResult<OrdersResult>
    suspend fun searchOrdersByNumber(
        page: Int = 1,
        orderNumber: String
    ): ApiResult<OrdersResult>

    suspend fun getOrderById(orderId: String): ApiResult<OrderDetail>
    suspend fun takeOrder(orderId: String): ApiResult<Unit>
    suspend fun startOrder(orderId: String): ApiResult<Unit>
    suspend fun completeOrder(
        orderId: String,
        actualCompletionTimeMinutes: Int,
        completionNotes: String?
    ): ApiResult<Unit>
    suspend fun uploadPhoto(orderId: String, photoData: ByteArray, fileName: String, photoType: String = "Before"): ApiResult<Unit>
    suspend fun addNote(orderId: String, content: String): ApiResult<Unit>
    suspend fun reportIssue(orderId: String, description: String): ApiResult<Unit>

    // Offline support methods
    fun getCachedOrders(): Flow<List<Order>>
    suspend fun getCachedOrderById(orderId: String): Order?
    suspend fun clearCache()
    suspend fun getNext48HoursCachedOrders(): List<Order>
    suspend fun getLastCacheTimestamp(): Long?
}

@Singleton
class OrdersRepositoryImpl @Inject constructor(
    private val apiService: ApiService,
    private val tokenManager: TokenManager,
    private val json: Json,
    private val orderDao: OrderDao,
    private val networkMonitor: NetworkMonitor
) : OrdersRepository {

    companion object {
        // Cache is valid for 30 minutes
        private const val CACHE_VALIDITY_MS = 30 * 60 * 1000L
    }

    override suspend fun getOrders(
        page: Int,
        pageSize: Int,
        filter: OrderFilter?
    ): ApiResult<PagedOrderResponse> {
        val offset = (page - 1) * pageSize
        val result = safeApiCall(json) {
            apiService.getOrders(
                offset = offset,
                limit = pageSize,
                orderStatuses = filter?.status?.let { listOf(it.apiValue) },
                employeeId = filter?.employeeId ?: tokenManager.getUserId(),
                hasAvailableSpots = null,
                excludeEmployeeId = null,
                customerName = filter?.searchTerm,
                customerEmail = null,
                displayOrderNumber = null,
                cleaningDateFrom = filter?.startDate,
                cleaningDateTo = filter?.endDate
            )
        }

        // Cache successful results (only first page without filters)
        if (result is ApiResult.Success && page == 1 && filter == null) {
            cacheOrders(result.data.items)
        }

        return result
    }

    /**
     * Cache orders to local database
     */
    private suspend fun cacheOrders(orders: List<Order>) {
        try {
            val cachedOrders = orders.map { CachedOrder.fromDomainModel(it) }
            orderDao.insertOrders(cachedOrders)
        } catch (e: Exception) {
            // Ignore cache errors
        }
    }

    override suspend fun getAvailableOrders(
        page: Int,
        searchTerm: String?,
        startDate: String?,
        endDate: String?,
        sortBy: String?,
        sortDescending: Boolean?,
        paymentStatuses: List<cz.cleansia.partner.domain.models.orders.PaymentStatus>
    ): ApiResult<OrdersResult> {
        val employeeId = tokenManager.getUserId()
        val pageSize = Constants.Pagination.DEFAULT_PAGE_SIZE
        val offset = (page - 1) * pageSize

        // Available orders: status Pending or Confirmed, hasAvailableSpots=true, exclude current employee
        return when (val result = safeApiCall(json) {
            apiService.getOrders(
                offset = offset,
                limit = pageSize,
                orderStatuses = listOf(OrderStatus.PENDING.apiValue, OrderStatus.CONFIRMED.apiValue),
                paymentStatuses = paymentStatuses.map { it.apiValue }.ifEmpty { null },
                employeeId = null,
                hasAvailableSpots = true,
                excludeEmployeeId = employeeId,
                customerName = searchTerm,
                customerEmail = null,
                displayOrderNumber = null,
                cleaningDateFrom = startDate,
                cleaningDateTo = endDate,
                sortField = sortBy,
                sortDirection = sortDescending?.let { if (it) 1 else 0 }
            )
        }) {
            is ApiResult.Success -> {
                cacheOrders(result.data.items)
                ApiResult.Success(
                    OrdersResult(
                        orders = result.data.items,
                        hasMore = result.data.hasNextPage
                    )
                )
            }
            is ApiResult.Error -> ApiResult.Error(result.error)
        }
    }

    override suspend fun getMyOrders(
        page: Int,
        statuses: List<OrderStatus>,
        searchTerm: String?,
        startDate: String?,
        endDate: String?,
        sortBy: String?,
        sortDescending: Boolean?,
        paymentStatuses: List<cz.cleansia.partner.domain.models.orders.PaymentStatus>
    ): ApiResult<OrdersResult> {
        val employeeId = tokenManager.getUserId()
        val pageSize = Constants.Pagination.DEFAULT_PAGE_SIZE
        val offset = (page - 1) * pageSize

        // My orders: filter by current employee ID and specified statuses
        return when (val result = safeApiCall(json) {
            apiService.getOrders(
                offset = offset,
                limit = pageSize,
                orderStatuses = statuses.map { it.apiValue }.ifEmpty { null },
                paymentStatuses = paymentStatuses.map { it.apiValue }.ifEmpty { null },
                employeeId = employeeId,
                hasAvailableSpots = null,
                excludeEmployeeId = null,
                customerName = searchTerm,
                customerEmail = null,
                displayOrderNumber = null,
                cleaningDateFrom = startDate,
                cleaningDateTo = endDate,
                sortField = sortBy,
                sortDirection = sortDescending?.let { if (it) 1 else 0 }
            )
        }) {
            is ApiResult.Success -> {
                cacheOrders(result.data.items)
                ApiResult.Success(
                    OrdersResult(
                        orders = result.data.items,
                        hasMore = result.data.hasNextPage
                    )
                )
            }
            is ApiResult.Error -> ApiResult.Error(result.error)
        }
    }

    override suspend fun searchOrdersByNumber(
        page: Int,
        orderNumber: String
    ): ApiResult<OrdersResult> {
        val pageSize = Constants.Pagination.DEFAULT_PAGE_SIZE
        val offset = (page - 1) * pageSize

        return when (val result = safeApiCall(json) {
            apiService.getOrders(
                offset = offset,
                limit = pageSize,
                orderStatuses = null,
                paymentStatuses = null,
                employeeId = null,
                hasAvailableSpots = null,
                excludeEmployeeId = null,
                customerName = null,
                customerEmail = null,
                displayOrderNumber = orderNumber,
                cleaningDateFrom = null,
                cleaningDateTo = null
            )
        }) {
            is ApiResult.Success -> ApiResult.Success(
                OrdersResult(
                    orders = result.data.items,
                    hasMore = result.data.hasNextPage
                )
            )
            is ApiResult.Error -> ApiResult.Error(result.error)
        }
    }

    override suspend fun getOrderById(orderId: String): ApiResult<OrderDetail> {
        return safeApiCall(json) {
            apiService.getOrderById(orderId)
        }
    }

    override suspend fun takeOrder(orderId: String): ApiResult<Unit> {
        val employeeId = tokenManager.getUserId()
        if (employeeId.isNullOrBlank()) {
            return ApiResult.Error(cz.cleansia.partner.core.network.ApiError.Unknown("Employee ID not found"))
        }
        return safeApiCall(json) {
            apiService.takeOrder(TakeOrderRequest(orderId, employeeId))
        }.let { result ->
            when (result) {
                is ApiResult.Success -> ApiResult.Success(Unit)
                is ApiResult.Error -> result
            }
        }
    }

    override suspend fun startOrder(orderId: String): ApiResult<Unit> {
        val employeeId = tokenManager.getUserId()
        if (employeeId.isNullOrBlank()) {
            return ApiResult.Error(cz.cleansia.partner.core.network.ApiError.Unknown("Employee ID not found"))
        }
        return safeApiCall(json) {
            apiService.startOrder(StartOrderRequest(orderId, employeeId))
        }.let { result ->
            when (result) {
                is ApiResult.Success -> ApiResult.Success(Unit)
                is ApiResult.Error -> result
            }
        }
    }

    override suspend fun completeOrder(
        orderId: String,
        actualCompletionTimeMinutes: Int,
        completionNotes: String?
    ): ApiResult<Unit> {
        val employeeId = tokenManager.getUserId()
        if (employeeId.isNullOrBlank()) {
            return ApiResult.Error(cz.cleansia.partner.core.network.ApiError.Unknown("Employee ID not found"))
        }
        return safeApiCall(json) {
            apiService.completeOrder(
                CompleteOrderRequest(
                    orderId = orderId,
                    employeeId = employeeId,
                    actualCompletionTimeMinutes = actualCompletionTimeMinutes,
                    completionNotes = completionNotes
                )
            )
        }.let { result ->
            when (result) {
                is ApiResult.Success -> ApiResult.Success(Unit)
                is ApiResult.Error -> result
            }
        }
    }

    override suspend fun uploadPhoto(
        orderId: String,
        photoData: ByteArray,
        fileName: String,
        photoType: String
    ): ApiResult<Unit> {
        val employeeId = tokenManager.getUserId()
        if (employeeId.isNullOrBlank()) {
            return ApiResult.Error(cz.cleansia.partner.core.network.ApiError.Unknown("Employee ID not found"))
        }

        val base64Content = android.util.Base64.encodeToString(photoData, android.util.Base64.NO_WRAP)
        val photoTypeEnum = PhotoType.fromApiValue(photoType)
        val extension = fileName.substringAfterLast('.', "jpg").lowercase()
        val contentType = when (extension) {
            "png" -> "image/png"
            "webp" -> "image/webp"
            else -> "image/jpeg"
        }

        val request = UploadOrderPhotoRequest(
            orderId = orderId,
            employeeId = employeeId,
            photoType = photoTypeEnum.apiNumericValue,
            fileName = fileName,
            contentType = contentType,
            fileData = base64Content
        )

        return safeApiCall(json) {
            apiService.uploadOrderPhoto(request)
        }.let { result ->
            // Map the typed response to Unit for the existing interface
            when (result) {
                is ApiResult.Success -> ApiResult.Success(Unit)
                is ApiResult.Error -> result
            }
        }
    }

    override suspend fun addNote(orderId: String, content: String): ApiResult<Unit> {
        val employeeId = tokenManager.getUserId()
        if (employeeId.isNullOrBlank()) {
            return ApiResult.Error(cz.cleansia.partner.core.network.ApiError.Unknown("Employee ID not found"))
        }

        return safeApiCall(json) {
            apiService.addOrderNote(
                cz.cleansia.partner.domain.models.orders.AddOrderNoteRequest(
                    orderId = orderId,
                    employeeId = employeeId,
                    content = content
                )
            )
        }.let { result ->
            when (result) {
                is ApiResult.Success -> ApiResult.Success(Unit)
                is ApiResult.Error -> result
            }
        }
    }

    override suspend fun reportIssue(orderId: String, description: String): ApiResult<Unit> {
        val employeeId = tokenManager.getUserId()
        if (employeeId.isNullOrBlank()) {
            return ApiResult.Error(cz.cleansia.partner.core.network.ApiError.Unknown("Employee ID not found"))
        }

        return safeApiCall(json) {
            apiService.reportOrderIssue(
                cz.cleansia.partner.domain.models.orders.ReportOrderIssueRequest(
                    orderId = orderId,
                    employeeId = employeeId,
                    description = description
                )
            )
        }.let { result ->
            when (result) {
                is ApiResult.Success -> ApiResult.Success(Unit)
                is ApiResult.Error -> result
            }
        }
    }

    // Offline support methods

    override fun getCachedOrders(): Flow<List<Order>> {
        return orderDao.getAllOrders().map { cachedOrders ->
            cachedOrders.map { it.toDomainModel() }
        }
    }

    override suspend fun getCachedOrderById(orderId: String): Order? {
        return orderDao.getOrderById(orderId)?.toDomainModel()
    }

    override suspend fun clearCache() {
        orderDao.deleteAllOrders()
    }

    override suspend fun getNext48HoursCachedOrders(): List<Order> {
        return try {
            val now = LocalDateTime.now()
            val in48Hours = now.plusHours(48)
            val formatter = DateTimeFormatter.ISO_LOCAL_DATE_TIME
            val fromStr = now.format(formatter)
            val toStr = in48Hours.format(formatter)
            orderDao.getOrdersInDateRange(fromStr, toStr).map { it.toDomainModel() }
        } catch (e: Exception) {
            emptyList()
        }
    }

    override suspend fun getLastCacheTimestamp(): Long? {
        return orderDao.getLastCacheTime()
    }
}
