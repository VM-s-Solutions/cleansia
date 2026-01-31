package cz.cleansia.partner.mock

import cz.cleansia.partner.core.network.ApiResult
import cz.cleansia.partner.domain.models.orders.CodeValue
import cz.cleansia.partner.domain.models.orders.Order
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

    override suspend fun getOrderById(orderId: String): ApiResult<OrderDetail> {
        delay(300)
        return ApiResult.Success(MockDataProvider.getOrderDetail(orderId))
    }

    override suspend fun takeOrder(orderId: String): ApiResult<OrderDetail> {
        delay(600)
        val detail = MockDataProvider.getOrderDetail(orderId)
        return ApiResult.Success(
            detail.copy(orderStatus = CodeValue("OrderStatus", "Confirmed", OrderStatus.CONFIRMED.apiValue))
        )
    }

    override suspend fun startOrder(orderId: String): ApiResult<OrderDetail> {
        delay(600)
        val detail = MockDataProvider.getOrderDetail(orderId)
        return ApiResult.Success(
            detail.copy(orderStatus = CodeValue("OrderStatus", "InProgress", OrderStatus.IN_PROGRESS.apiValue))
        )
    }

    override suspend fun completeOrder(
        orderId: String,
        actualCompletionTimeMinutes: Int,
        completionNotes: String?
    ): ApiResult<OrderDetail> {
        delay(800)
        val detail = MockDataProvider.getOrderDetail(orderId)
        return ApiResult.Success(
            detail.copy(
                orderStatus = CodeValue("OrderStatus", "Completed", OrderStatus.COMPLETED.apiValue),
                actualCompletionTime = actualCompletionTimeMinutes,
                completionNotes = completionNotes
            )
        )
    }

    override suspend fun uploadPhoto(orderId: String, photoData: ByteArray, fileName: String, photoType: String): ApiResult<Unit> {
        delay(1000)
        return ApiResult.Success(Unit)
    }

    override fun getCachedOrders(): Flow<List<Order>> = flowOf(emptyList())

    override suspend fun getCachedOrderById(orderId: String): Order? = null

    override suspend fun clearCache() { /* no-op */ }
}
