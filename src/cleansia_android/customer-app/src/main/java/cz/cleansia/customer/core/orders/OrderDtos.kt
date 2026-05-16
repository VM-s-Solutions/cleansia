package cz.cleansia.customer.core.orders

import cz.cleansia.customer.core.user.CodeDto
import kotlinx.serialization.Serializable

/**
 * Wire DTOs for the customer Order endpoints (`/api/Order/GetMyOrders`,
 * `/api/Order/GetById`). Field casing matches the backend's camelCase JSON
 * policy. Nullability mirrors the OpenAPI schema dump: reference-type fields
 * that the backend marks `nullable: true` are `T? = null` on the Kotlin side.
 *
 * UI/domain types should not consume these directly — a later phase will map
 * them to presentation models so screens don't couple to wire shapes.
 */

/** Paged response wrapper — matches backend `PagedData<T>` (pageNumber/pageSize/total/data). */
@Serializable
data class OrderListResponseDto(
    val pageNumber: Int = 0,
    val pageSize: Int = 0,
    val total: Int = 0,
    val data: List<OrderListItemDto> = emptyList(),
)

/** Mirrors backend `OrderListItem`. */
@Serializable
data class OrderListItemDto(
    val id: String? = null,
    val customerName: String? = null,
    val customerEmail: String? = null,
    val customerPhone: String? = null,
    /** Backend pre-formats this as a single line; use as-is for list display. */
    val customerAddress: String? = null,
    val displayOrderNumber: String? = null,
    val rooms: Int = 0,
    val bathrooms: Int = 0,
    val extras: Map<String, Boolean>? = null,
    /** ISO-8601 date-time; parse at the UI layer. */
    val cleaningDateTime: String? = null,
    val paymentType: CodeDto? = null,
    val paymentStatus: CodeDto? = null,
    val totalPrice: Double = 0.0,
    val originalSubtotal: Double = 0.0,
    /** 0=None, 1=Tier, 2=Membership, 3=Promo. */
    val appliedDiscountSource: Int = 0,
    val tierDiscountAmount: Double? = null,
    val membershipDiscountAmount: Double? = null,
    val promoDiscountAmount: Double? = null,
    val estimatedTime: Int = 0,
    val orderStatus: CodeDto? = null,
    val confirmationCode: String? = null,
    val stripeSessionId: String? = null,
    val selectedPackages: List<OrderPackageSummaryDto>? = null,
    val currencyId: String? = null,
    val currency: OrderCurrencyListItemDto? = null,
    /** List of employee id strings (not full employee objects on the list endpoint). */
    val assignedEmployees: List<String>? = null,
    val selectedServices: List<OrderServiceSummaryDto>? = null,
    val requiredEmployees: Int = 0,
    val maxEmployees: Int = 0,
    val availableSpots: Int = 0,
    val assignedEmployeesCount: Int = 0,
    val hasAvailableSpots: Boolean = false,
)

/** Mirrors backend `OrderItem` (GetById response). */
@Serializable
data class OrderDetailDto(
    val id: String? = null,
    val displayOrderNumber: String? = null,
    val customerName: String? = null,
    val customerEmail: String? = null,
    val customerPhone: String? = null,
    val address: OrderAddressDto? = null,
    val rooms: Int = 0,
    val bathrooms: Int = 0,
    val extras: Map<String, Boolean>? = null,
    val cleaningDateTime: String? = null,
    val paymentType: CodeDto? = null,
    val paymentStatus: CodeDto? = null,
    val totalPrice: Double = 0.0,
    val originalSubtotal: Double = 0.0,
    /** 0=None, 1=Tier, 2=Membership, 3=Promo. */
    val appliedDiscountSource: Int = 0,
    val tierDiscountAmount: Double? = null,
    val membershipDiscountAmount: Double? = null,
    val promoDiscountAmount: Double? = null,
    val estimatedTime: Int = 0,
    val actualCompletionTime: Int? = null,
    val completionNotes: String? = null,
    val orderStatus: CodeDto? = null,
    val confirmationCode: String? = null,
    val stripeSessionId: String? = null,
    val notes: String? = null,
    val specialInstructions: String? = null,
    val accessInstructions: String? = null,
    /**
     * FK back to the recurring booking template that spawned this order.
     * Non-null + Pending payment status means the OrderDetail screen shows
     * the "Confirm and pay" CTA so the customer can take it through Wave 3.3's
     * confirm flow.
     */
    val recurringTemplateId: String? = null,
    val selectedPackages: List<OrderPackageDetailsDto>? = null,
    val currency: OrderCurrencyDetailDto? = null,
    val selectedServices: List<OrderServiceDetailsDto>? = null,
    val statusHistory: List<OrderStatusTrackDto>? = null,
    /** ISO-8601 date-time. */
    val createdOn: String? = null,
    val updatedOn: String? = null,
    val assignedEmployees: List<AssignedEmployeeDto>? = null,
    val receiptNumber: String? = null,
    val orderNotes: List<OrderNoteDto>? = null,
    val orderIssues: List<OrderIssueDto>? = null,
    val review: OrderReviewDto? = null,
)

/** Mirrors backend `OrderAddress`. Backend includes `country` (task spec omitted it). */
@Serializable
data class OrderAddressDto(
    val street: String? = null,
    val city: String? = null,
    val zipCode: String? = null,
    val country: String? = null,
)

/** Mirrors backend `OrderStatusTrackDto`. */
@Serializable
data class OrderStatusTrackDto(
    val status: CodeDto? = null,
    val createdOn: String? = null,
)

/**
 * Mirrors backend `AssignedEmployeeDto`. Backend shape uses `fullName` +
 * `phoneNumber` (not `name`/`phone` as the task spec sketched), and exposes
 * `id` + `employeeId` but not `rating`. Kept aligned with the wire — UI can
 * derive a display string.
 */
@Serializable
data class AssignedEmployeeDto(
    val id: String? = null,
    val employeeId: String? = null,
    val fullName: String? = null,
    val phoneNumber: String? = null,
    val email: String? = null,
)

/** Mirrors backend `OrderReviewDto`. */
@Serializable
data class OrderReviewDto(
    val id: String? = null,
    val orderId: String? = null,
    val userId: String? = null,
    val rating: Int = 0,
    val comment: String? = null,
    val createdOn: String? = null,
    val updatedOn: String? = null,
)

/** Mirrors backend `OrderNoteDto`. */
@Serializable
data class OrderNoteDto(
    val id: String? = null,
    val employeeId: String? = null,
    val content: String? = null,
    val createdOn: String? = null,
)

/** Mirrors backend `OrderIssueDto`. */
@Serializable
data class OrderIssueDto(
    val id: String? = null,
    val reportedByEmployeeId: String? = null,
    val description: String? = null,
    val isResolved: Boolean = false,
    val resolvedAt: String? = null,
    val createdOn: String? = null,
)

// ─── Service / Package / Currency summaries ───
// Shape matches the list-item vs detail distinction on the backend — list
// endpoint returns PackageListItem/ServiceListItem/CurrencyListItem, detail
// returns PackageDetails/ServiceDetails/CurrencyDetailDto.

/** Mirrors backend `ServiceListItem` (used inside OrderListItem). */
@Serializable
data class OrderServiceSummaryDto(
    val id: String? = null,
    val name: String? = null,
    val description: String? = null,
    val basePrice: Double = 0.0,
    val perRoomPrice: Double = 0.0,
)

/** Mirrors backend `ServiceDetails` (used inside OrderItem). */
@Serializable
data class OrderServiceDetailsDto(
    val id: String? = null,
    val name: String? = null,
    val description: String? = null,
    val estimatedTime: Int = 0,
    val currencyCode: String? = null,
)

/** Mirrors backend `PackageListItem` (used inside OrderListItem). */
@Serializable
data class OrderPackageSummaryDto(
    val id: String? = null,
    val name: String? = null,
    val description: String? = null,
    val price: Double = 0.0,
)

/** Mirrors backend `PackageDetails` (used inside OrderItem). */
@Serializable
data class OrderPackageDetailsDto(
    val id: String? = null,
    val name: String? = null,
    val description: String? = null,
    val price: Double = 0.0,
    val estimatedTime: Int = 0,
    val currencyCode: String? = null,
    val includedServices: List<String>? = null,
)

/** Mirrors backend `CurrencyListItem`. */
@Serializable
data class OrderCurrencyListItemDto(
    val id: String? = null,
    val code: String? = null,
    val symbol: String? = null,
    val name: String? = null,
    val exchangeRate: Double = 0.0,
    val isDefault: Boolean = false,
)

/** Mirrors backend `CurrencyDetailDto`. */
@Serializable
data class OrderCurrencyDetailDto(
    val id: String? = null,
    val code: String? = null,
    val name: String? = null,
    val symbol: String? = null,
    val exchangeRate: Double = 0.0,
    val isDefault: Boolean = false,
)

// ─── Cancel / Review / Photos / Receipt (Wave 2 actions) ───

/**
 * Mirrors backend `CancelOrder.Command`. Backend additionally requires a `UserId`
 * on the record, but the controller enriches it from the JWT claims before the
 * handler runs — the client must NOT send it.
 */
@Serializable
data class CancelOrderRequest(
    val orderId: String,
    val reason: String? = null,
)

/** Mirrors backend `ConfirmRecurringOrder.Command`. */
@Serializable
data class ConfirmRecurringOrderRequest(val orderId: String)

/**
 * Mirrors backend `ConfirmRecurringOrder.Response`. Card path returns the
 * three Stripe pieces the PaymentSheet needs (clientSecret + customerId +
 * ephemeralKey); Cash path returns nulls for those and the order is already
 * marked Confirmed + Paid server-side.
 */
@Serializable
data class ConfirmRecurringOrderResponse(
    val orderId: String? = null,
    val clientSecret: String? = null,
    val paymentIntentId: String? = null,
    val stripeCustomerId: String? = null,
    val ephemeralKey: String? = null,
)

/**
 * Mirrors backend `CancelOrder.Response`. Includes `orderId` and `totalPrice`
 * alongside the fee rate / refund details so the UI can render a definitive
 * confirmation without re-fetching the order.
 */
@Serializable
data class CancelOrderResponse(
    val orderId: String? = null,
    /** 0.0 / 0.5 / 1.0 per BookingPolicy's cancellation tiers. */
    val feeRate: Double = 0.0,
    val refundAmount: Double = 0.0,
    val totalPrice: Double = 0.0,
    val refundInitiated: Boolean = false,
)

/**
 * Mirrors backend `SubmitOrderReview.Command`. Like cancel, the backend's
 * `UserId` field is enriched server-side from the JWT; we don't send it.
 */
@Serializable
data class SubmitReviewRequest(
    val orderId: String,
    val rating: Int,
    val comment: String? = null,
)

/** Mirrors backend `GetOrderPhotos.Response`. */
@Serializable
data class OrderPhotosResponse(
    val photos: List<OrderPhotoDto> = emptyList(),
    val beforePhotoCount: Int = 0,
    val afterPhotoCount: Int = 0,
)

/**
 * Mirrors backend `GetOrderPhotos.OrderPhotoDto`. Note:
 *  - `photoType` is serialized as an int (backend `PhotoType` enum is
 *    numeric — `Before = 1`, `After = 2`). No string form.
 *  - `blobUrl` is a fully-signed SAS URL with 1h TTL — pass directly to
 *    Coil / AsyncImage; no auth header needed.
 *  - Both `fileName` and `originalFileName` are present on the backend DTO.
 */
/**
 * Mirrors backend `GetMyServingCleaners.Response`. Returned by
 * `/api/Order/MyServingCleaners` — feeds the favorite-cleaner picker.
 */
@Serializable
data class ServingCleanerDto(
    val employeeId: String,
    val fullName: String,
    /** ISO-8601 date-time of the most recent Completed service for this user. */
    val lastServedOn: String,
)

@Serializable
data class OrderPhotoDto(
    val id: String? = null,
    val photoType: Int? = null,
    val blobUrl: String? = null,
    val fileName: String? = null,
    val originalFileName: String? = null,
    val fileSizeBytes: Long = 0L,
    val contentType: String? = null,
    val capturedAt: String? = null,
    val capturedByEmployeeId: String? = null,
    val capturedByEmployeeName: String? = null,
    val width: Int? = null,
    val height: Int? = null,
    val notes: String? = null,
)
