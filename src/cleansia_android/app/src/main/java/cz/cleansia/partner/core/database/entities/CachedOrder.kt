package cz.cleansia.partner.core.database.entities

import androidx.room.Entity
import androidx.room.PrimaryKey
import cz.cleansia.partner.domain.models.orders.Order
import cz.cleansia.partner.domain.models.orders.CodeValue
import cz.cleansia.partner.domain.models.orders.CurrencyInfo
import cz.cleansia.partner.domain.models.orders.ServiceInfo

/**
 * Room entity for caching orders locally.
 * This allows the app to display orders when offline.
 */
@Entity(tableName = "cached_orders")
data class CachedOrder(
    @PrimaryKey
    val id: String,
    val displayOrderNumber: String?,
    val orderStatusValue: Int?,
    val orderStatusName: String?,
    val paymentStatusValue: Int?,
    val paymentStatusName: String?,
    val cleaningDateTime: String?,
    val customerName: String?,
    val customerEmail: String?,
    val customerPhone: String?,
    val customerAddress: String?,
    val totalPrice: Double?,
    val currencyCode: String?,
    val currencySymbol: String?,
    val servicesJson: String?, // JSON array of service names
    val estimatedTime: Int?,
    val rooms: Int?,
    val bathrooms: Int?,
    val hasAvailableSpots: Boolean?,
    val assignedEmployeesCount: Int?,
    val requiredEmployees: Int?,
    val cachedAt: Long = System.currentTimeMillis()
) {
    /**
     * Convert cached entity back to domain model
     */
    fun toDomainModel(): Order {
        return Order(
            id = id,
            displayOrderNumber = displayOrderNumber,
            orderStatus = if (orderStatusValue != null || orderStatusName != null) {
                CodeValue(
                    type = "OrderStatus",
                    name = orderStatusName,
                    value = orderStatusValue
                )
            } else null,
            paymentStatus = if (paymentStatusValue != null || paymentStatusName != null) {
                CodeValue(
                    type = "PaymentStatus",
                    name = paymentStatusName,
                    value = paymentStatusValue
                )
            } else null,
            cleaningDateTime = cleaningDateTime,
            customerName = customerName,
            customerEmail = customerEmail,
            customerPhone = customerPhone,
            customerAddress = customerAddress,
            totalPrice = totalPrice,
            currency = if (currencyCode != null) {
                CurrencyInfo(
                    code = currencyCode,
                    symbol = currencySymbol
                )
            } else null,
            selectedServices = parseServicesFromJson(servicesJson),
            estimatedTime = estimatedTime,
            rooms = rooms,
            bathrooms = bathrooms,
            hasAvailableSpots = hasAvailableSpots,
            assignedEmployeesCount = assignedEmployeesCount,
            requiredEmployees = requiredEmployees
        )
    }

    private fun parseServicesFromJson(json: String?): List<ServiceInfo>? {
        if (json.isNullOrBlank()) return null
        return try {
            // Simple parsing - just extract service names from JSON array
            json.trim('[', ']')
                .split(",")
                .map { it.trim().trim('"') }
                .filter { it.isNotBlank() }
                .map { ServiceInfo(name = it) }
        } catch (e: Exception) {
            null
        }
    }

    companion object {
        /**
         * Create a cached entity from domain model
         */
        fun fromDomainModel(order: Order): CachedOrder {
            return CachedOrder(
                id = order.id,
                displayOrderNumber = order.displayOrderNumber,
                orderStatusValue = order.orderStatus?.value,
                orderStatusName = order.orderStatus?.name,
                paymentStatusValue = order.paymentStatus?.value,
                paymentStatusName = order.paymentStatus?.name,
                cleaningDateTime = order.cleaningDateTime,
                customerName = order.customerName,
                customerEmail = order.customerEmail,
                customerPhone = order.customerPhone,
                customerAddress = order.customerAddress,
                totalPrice = order.totalPrice,
                currencyCode = order.currency?.code,
                currencySymbol = order.currency?.symbol,
                servicesJson = order.selectedServices?.mapNotNull { it.name }
                    ?.let { "[\"${it.joinToString("\",\"")}\"]" },
                estimatedTime = order.estimatedTime,
                rooms = order.rooms,
                bathrooms = order.bathrooms,
                hasAvailableSpots = order.hasAvailableSpots,
                assignedEmployeesCount = order.assignedEmployeesCount,
                requiredEmployees = order.requiredEmployees
            )
        }
    }
}
