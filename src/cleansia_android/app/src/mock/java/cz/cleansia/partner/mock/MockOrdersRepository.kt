package cz.cleansia.partner.mock

import cz.cleansia.partner.core.network.ApiResult
import cz.cleansia.partner.domain.models.orders.CodeValue
import cz.cleansia.partner.domain.models.orders.Order
import cz.cleansia.partner.domain.models.orders.AssignedEmployee
import cz.cleansia.partner.domain.models.orders.OrderDetail
import cz.cleansia.partner.domain.models.orders.OrderFilter
import cz.cleansia.partner.domain.models.orders.OrderStatus
import cz.cleansia.partner.domain.models.orders.PagedOrderResponse
import cz.cleansia.partner.domain.models.orders.PaymentStatus
import cz.cleansia.partner.domain.repositories.OrdersRepository
import cz.cleansia.partner.domain.repositories.OrdersResult
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.flowOf
import java.time.Instant

class MockOrdersRepository : OrdersRepository {

    override suspend fun getOrders(
        page: Int,
        pageSize: Int,
        filter: OrderFilter?
    ): ApiResult<PagedOrderResponse> {
        delay(500)
        return ApiResult.Success(MockDataProvider.getPagedOrders(page, pageSize, filter))
    }

    override suspend fun getAvailableOrders(
        page: Int,
        searchTerm: String?,
        startDate: String?,
        endDate: String?,
        sortBy: String?,
        sortDescending: Boolean?,
        paymentStatuses: List<PaymentStatus>
    ): ApiResult<OrdersResult> {
        delay(500)
        val (orders, hasMore) = MockDataProvider.getAvailableOrders(
            page = page,
            searchTerm = searchTerm,
            sortBy = sortBy,
            sortDescending = sortDescending
        )
        return ApiResult.Success(OrdersResult(orders = orders, hasMore = hasMore))
    }

    override suspend fun getMyOrders(
        page: Int,
        statuses: List<OrderStatus>,
        searchTerm: String?,
        startDate: String?,
        endDate: String?,
        sortBy: String?,
        sortDescending: Boolean?,
        paymentStatuses: List<PaymentStatus>
    ): ApiResult<OrdersResult> {
        delay(500)
        val (orders, hasMore) = MockDataProvider.getMyOrders(
            page = page,
            statuses = statuses,
            searchTerm = searchTerm,
            sortBy = sortBy,
            sortDescending = sortDescending
        )
        return ApiResult.Success(OrdersResult(orders = orders, hasMore = hasMore))
    }

    override suspend fun searchOrdersByNumber(
        page: Int,
        orderNumber: String
    ): ApiResult<OrdersResult> {
        delay(500)
        return ApiResult.Success(OrdersResult(orders = emptyList(), hasMore = false))
    }

    override suspend fun getOrderById(orderId: String): ApiResult<OrderDetail> {
        delay(300)
        return ApiResult.Success(MockDataProvider.getOrderDetail(orderId))
    }

    override suspend fun takeOrder(orderId: String): ApiResult<Unit> {
        delay(600)
        return ApiResult.Success(Unit)
    }

    override suspend fun startOrder(orderId: String): ApiResult<Unit> {
        delay(600)
        return ApiResult.Success(Unit)
    }

    override suspend fun completeOrder(
        orderId: String,
        actualCompletionTimeMinutes: Int,
        completionNotes: String?
    ): ApiResult<Unit> {
        delay(800)
        return ApiResult.Success(Unit)
    }

    override suspend fun uploadPhoto(orderId: String, photoData: ByteArray, fileName: String, photoType: String): ApiResult<Unit> {
        delay(1000)
        return ApiResult.Success(Unit)
    }

    override suspend fun addNote(orderId: String, content: String): ApiResult<Unit> {
        delay(500)
        return ApiResult.Success(Unit)
    }

    override suspend fun reportIssue(orderId: String, description: String): ApiResult<Unit> {
        delay(500)
        return ApiResult.Success(Unit)
    }

    override fun getCachedOrders(): Flow<List<Order>> = flowOf(emptyList())

    override suspend fun getCachedOrderById(orderId: String): Order? = null

    override suspend fun clearCache() { /* no-op */ }

    override suspend fun getNext48HoursCachedOrders(): List<Order> =
        MockDataProvider.getAvailableOrders(page = 1, pageSize = 5).first

    override suspend fun getLastCacheTimestamp(): Long? =
        System.currentTimeMillis() - 300_000L
}
