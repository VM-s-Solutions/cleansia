package cz.cleansia.partner.domain.models.orders

import kotlinx.serialization.Serializable

/**
 * Order status enum - matches API integer values
 * Pending = 1, Confirmed = 2, InProgress = 3, Completed = 4, Cancelled = 5
 */
enum class OrderStatus(val apiValue: Int, val apiName: String) {
    PENDING(1, "Pending"),
    CONFIRMED(2, "Confirmed"),
    IN_PROGRESS(3, "InProgress"),
    COMPLETED(4, "Completed"),
    CANCELLED(5, "Cancelled");

    companion object {
        fun fromApiValue(value: Int): OrderStatus =
            entries.find { it.apiValue == value } ?: PENDING

        fun fromApiName(name: String): OrderStatus =
            entries.find { it.apiName.equals(name, ignoreCase = true) } ?: PENDING
    }
}

/**
 * Payment status enum - matches API integer values
 * Pending = 1, Paid = 2, Failed = 3, Refunded = 4
 */
enum class PaymentStatus(val apiValue: Int, val apiName: String) {
    PENDING(1, "Pending"),
    PAID(2, "Paid"),
    FAILED(3, "Failed"),
    REFUNDED(4, "Refunded");

    companion object {
        fun fromApiValue(value: Int): PaymentStatus =
            entries.find { it.apiValue == value } ?: PENDING

        fun fromApiName(name: String): PaymentStatus =
            entries.find { it.apiName.equals(name, ignoreCase = true) } ?: PENDING
    }
}

/**
 * Code object representing enum values from the API
 */
@Serializable
data class CodeValue(
    val type: String? = null,
    val name: String? = null,
    val value: Int? = null
)

/**
 * Currency information from the API
 */
@Serializable
data class CurrencyInfo(
    val id: String? = null,
    val code: String? = null,
    val name: String? = null,
    val symbol: String? = null
)

/**
 * Service item from the API
 */
@Serializable
data class ServiceInfo(
    val id: String? = null,
    val name: String? = null,
    val description: String? = null,
    val price: Double? = null
)

@Serializable
data class Order(
    val id: String,
    val displayOrderNumber: String? = null,
    val orderStatus: CodeValue? = null,
    val paymentStatus: CodeValue? = null,
    val cleaningDateTime: String? = null,
    val customerName: String? = null,
    val customerEmail: String? = null,
    val customerPhone: String? = null,
    val customerAddress: String? = null,
    val totalPrice: Double? = null,
    val currency: CurrencyInfo? = null,
    val selectedServices: List<ServiceInfo>? = null,
    val estimatedTime: Int? = null,
    val rooms: Int? = null,
    val bathrooms: Int? = null,
    val hasAvailableSpots: Boolean? = null,
    val assignedEmployeesCount: Int? = null,
    val requiredEmployees: Int? = null
) {
    // Helper properties for UI compatibility
    val orderNumber: String get() = displayOrderNumber ?: ""
    val status: OrderStatus get() = orderStatus?.value?.let { OrderStatus.fromApiValue(it) }
        ?: orderStatus?.name?.let { OrderStatus.fromApiName(it) }
        ?: OrderStatus.PENDING
    val paymentStatusEnum: PaymentStatus get() = paymentStatus?.value?.let { PaymentStatus.fromApiValue(it) }
        ?: paymentStatus?.name?.let { PaymentStatus.fromApiName(it) }
        ?: PaymentStatus.PENDING

    val scheduledDate: String get() = cleaningDateTime ?: ""
    val address: String get() = customerAddress ?: ""
    val totalAmount: Double get() = totalPrice ?: 0.0
    val currencyCode: String get() = currency?.code ?: "CZK"

    val services: List<String> get() = selectedServices?.mapNotNull { it.name } ?: emptyList()
}

/**
 * Address object for order details
 */
@Serializable
data class OrderAddressInfo(
    val street: String? = null,
    val city: String? = null,
    val zipCode: String? = null,
    val country: String? = null
) {
    val formatted: String
        get() = listOfNotNull(street, city, zipCode, country)
            .filter { it.isNotBlank() }
            .joinToString(", ")
}

/**
 * Currency detail object for order details
 */
@Serializable
data class CurrencyDetail(
    val id: String? = null,
    val code: String? = null,
    val name: String? = null,
    val symbol: String? = null,
    val exchangeRate: Double? = null,
    val isDefault: Boolean? = null
)

/**
 * Service detail for order details
 */
@Serializable
data class ServiceDetail(
    val id: String? = null,
    val name: String? = null,
    val description: String? = null,
    val price: Double? = null,
    val estimatedTime: Int? = null
)

@Serializable
data class OrderDetail(
    val id: String,
    val displayOrderNumber: String? = null,
    val orderStatus: CodeValue? = null,
    val paymentStatus: CodeValue? = null,
    val paymentType: CodeValue? = null,
    val cleaningDateTime: String? = null,
    val customerName: String? = null,
    val customerEmail: String? = null,
    val customerPhone: String? = null,
    val address: OrderAddressInfo? = null,
    val totalPrice: Double? = null,
    val currency: CurrencyDetail? = null,
    val selectedServices: List<ServiceDetail>? = null,
    val estimatedTime: Int? = null,
    val actualCompletionTime: Int? = null,
    val completionNotes: String? = null,
    val notes: String? = null,
    val specialInstructions: String? = null,
    val accessInstructions: String? = null,
    val rooms: Int? = null,
    val bathrooms: Int? = null,
    val createdOn: String? = null,
    val updatedOn: String? = null,
    val receiptNumber: String? = null,
    val startedAt: String? = null,      // ISO timestamp when order was started (for timer)
    val completedAt: String? = null     // ISO timestamp when order was completed
) {
    // Helper properties for UI compatibility
    val orderNumber: String get() = displayOrderNumber ?: ""
    val status: OrderStatus get() = orderStatus?.value?.let { OrderStatus.fromApiValue(it) }
        ?: orderStatus?.name?.let { OrderStatus.fromApiName(it) }
        ?: OrderStatus.PENDING
    val paymentStatusEnum: PaymentStatus get() = paymentStatus?.value?.let { PaymentStatus.fromApiValue(it) }
        ?: paymentStatus?.name?.let { PaymentStatus.fromApiName(it) }
        ?: PaymentStatus.PENDING
    val scheduledDate: String get() = cleaningDateTime ?: ""
    val totalAmount: Double get() = totalPrice ?: 0.0
    val currencyCode: String get() = currency?.code ?: "CZK"
    val fullAddress: String get() = address?.formatted ?: ""
    val services: List<String> get() = selectedServices?.mapNotNull { it.name } ?: emptyList()
}

@Serializable
data class OrderService(
    val id: String,
    val name: String,
    val description: String = "",
    val quantity: Int = 1,
    val price: Double = 0.0,
    val currency: String = "CZK"
)

@Serializable
data class ServiceItem(
    val id: String,
    val name: String,
    val description: String? = null,
    val quantity: Int = 1,
    val unitPrice: Double = 0.0,
    val totalPrice: Double = 0.0,
    val currency: String = "CZK"
)

/**
 * Photo type enum for before/after cleaning photos
 */
enum class PhotoType(val apiValue: String) {
    BEFORE("Before"),
    AFTER("After");

    companion object {
        fun fromApiValue(value: String?): PhotoType =
            entries.find { it.apiValue.equals(value, ignoreCase = true) } ?: BEFORE
    }
}

@Serializable
data class OrderPhoto(
    val id: String,
    val url: String,
    val thumbnailUrl: String? = null,
    val caption: String? = null,
    val uploadedAt: String? = null,
    val type: String? = null  // "Before" or "After"
) {
    val photoType: PhotoType get() = PhotoType.fromApiValue(type)
}

@Serializable
data class PagedOrderResponse(
    val data: List<Order> = emptyList(),
    val pageNumber: Int = 1,
    val pageSize: Int = 20,
    val total: Int = 0
) {
    // Compatibility helpers
    val items: List<Order> get() = data
    val totalCount: Int get() = total
    val totalPages: Int get() = if (pageSize > 0) (total + pageSize - 1) / pageSize else 0
    val hasNextPage: Boolean get() = pageNumber < totalPages
    val hasPreviousPage: Boolean get() = pageNumber > 1
}

/**
 * Filter parameters for orders
 */
data class OrderFilter(
    val status: OrderStatus? = null,
    val searchTerm: String? = null,
    val startDate: String? = null,
    val endDate: String? = null,
    val employeeId: String? = null
)

/**
 * Request to take an order
 */
@Serializable
data class TakeOrderRequest(
    val orderId: String,
    val employeeId: String
)

/**
 * Request to start an order
 */
@Serializable
data class StartOrderRequest(
    val orderId: String,
    val employeeId: String
)

/**
 * Request to complete an order
 */
@Serializable
data class CompleteOrderRequest(
    val orderId: String,
    val employeeId: String,
    val actualCompletionTimeMinutes: Int,
    val completionNotes: String? = null
)
