package cz.cleansia.customer.core.orders

import cz.cleansia.customer.core.catalog.TranslationDto
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
    /**
     * Rows the server SENT, which is not [data].size once the mapper drops an unidentifiable one.
     * Pagination is offset-based against the server's [total], so both the offset and the stop
     * condition have to count what the server counted or neither ever reaches it.
     */
    val receivedCount: Int = data.size,
)

/** Mirrors backend `OrderListItem`. */
@Serializable
data class OrderListItemDto(
    val id: String,
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
    val totalPrice: Double,
    val originalSubtotal: Double,
    /** 0=None, 1=Tier, 2=Membership, 3=Promo. */
    val appliedDiscountSource: Int,
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
    /**
     * Whether this order already carries the customer's review. Projected server-side precisely so the
     * completion prompt can be decided from the WARM list cache — the alternative is a detail fetch per
     * candidate order every time the app opens.
     */
    val hasReview: Boolean = false,
)

/** Mirrors backend `OrderItem` (GetById response). */
@Serializable
data class OrderDetailDto(
    val id: String,
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
    val totalPrice: Double,
    val originalSubtotal: Double,
    /** 0=None, 1=Tier, 2=Membership, 3=Promo. */
    val appliedDiscountSource: Int,
    val tierDiscountAmount: Double? = null,
    val membershipDiscountAmount: Double? = null,
    val promoDiscountAmount: Double? = null,
    val estimatedTime: Int = 0,
    val actualCompletionTime: Int? = null,
    /**
     * ISO-8601 UTC timestamp of when the order was actually marked
     * Completed (authoritative completion column on the backend,
     * mirrors the existing CancelledAt pattern). Null while the order
     * is still open. Previously the customer UI inferred completion
     * time from `statusHistory` entries; now there's a dedicated
     * field. Parse at the UI layer.
     */
    val completedAt: String? = null,
    val completionNotes: String? = null,
    val orderStatus: CodeDto? = null,
    val confirmationCode: String? = null,
    val stripeSessionId: String? = null,
    val notes: String? = null,
    val specialInstructions: String? = null,
    val accessInstructions: String? = null,
    /**
     * Why the PLATFORM cancelled this order, as a key the UI localises — null for every order the
     * platform did not cancel itself.
     *
     * The backend populates it only when `CancelledBy` is `System`, because the same column also
     * carries an admin's free-text note written for other staff. That gating is server-side, so
     * nothing here has to decide whether the value is safe to show.
     */
    val systemCancellationReason: String? = null,
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

/**
 * Mirrors backend `OrderAddress`. Backend includes `country` (task spec omitted it).
 *
 * Latitude/longitude are the geocode captured when the order was placed; they
 * back the order detail's map. Nullable because orders booked before geocoding
 * existed, or whose address never resolved, carry neither.
 */
@Serializable
data class OrderAddressDto(
    val street: String? = null,
    val city: String? = null,
    val zipCode: String? = null,
    val country: String? = null,
    val latitude: Double? = null,
    val longitude: Double? = null,
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

/**
 * The chips a customer may attach to a review. Mirrors the backend `ReviewTag` enum, whose INTEGER is
 * the wire contract — positive tags occupy 1..10 and negative 11..20, banded so a later insert never
 * renumbers a shipped value.
 *
 * **Hand-written rather than the generated enum**, for the reason every other enum here is: the
 * generator emits members named `_1`.._18`, which read as nothing at a call site. [ReviewTagCodes]
 * maps to the generated type at the adapter boundary, exactly as disputes do.
 *
 * There is deliberately no damage tag — damage is a dispute reason and belongs on the money path.
 */
enum class ReviewTag(val code: Int, val isPositive: Boolean) {
    OnTime(1, true),
    Thorough(2, true),
    Friendly(3, true),
    CarefulWithBelongings(4, true),
    ExtrasDoneWell(5, true),
    FollowedInstructions(6, true),
    GreatPhotos(7, true),
    ArrivedLate(11, false),
    MissedAreas(12, false),
    FeltRushed(13, false),
    ExtraNotDone(14, false),
    DidNotFollowInstructions(15, false),
    Unprofessional(16, false),
    SmellOrProducts(17, false),
    CrewSmallerThanBooked(18, false),
    ;

    companion object {
        /** The lowest rating that offers the positive set — mirrors `ReviewTagPolarity`. */
        const val POSITIVE_RATING_FLOOR = 4

        /** The server refuses more than this, so the sheet stops offering at it. */
        const val MAX_TAGS = 4

        fun fromCode(code: Int): ReviewTag? = entries.firstOrNull { it.code == code }

        /** The set to offer for [rating]; empty outside 1..5. */
        fun forRating(rating: Int): List<ReviewTag> = when (rating) {
            in 1..5 -> entries.filter { it.isPositive == (rating >= POSITIVE_RATING_FLOOR) }
            else -> emptyList()
        }
    }
}

/** Mirrors backend `OrderReviewDto`. */
@Serializable
data class OrderReviewDto(
    val id: String? = null,
    val orderId: String? = null,
    val userId: String? = null,
    val rating: Int = 0,
    val comment: String? = null,
    // Wire codes, not [ReviewTag]: an unknown code from a newer server must not crash an older app, so
    // the raw list is carried and [tags] drops what it cannot name.
    val tagCodes: List<Int> = emptyList(),
    val createdOn: String? = null,
    val updatedOn: String? = null,
) {
    val tags: List<ReviewTag> get() = tagCodes.mapNotNull(ReviewTag::fromCode)
}

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
    /** Per-language name/description overrides keyed by 2-letter code (T-0395). */
    val translations: Map<String, TranslationDto>? = null,
)

/** Mirrors backend `ServiceDetails` (used inside OrderItem). */
@Serializable
data class OrderServiceDetailsDto(
    val id: String? = null,
    val name: String? = null,
    val description: String? = null,
    val estimatedTime: Int = 0,
    val currencyCode: String? = null,
    /** Per-language name/description overrides keyed by 2-letter code (T-0395). */
    val translations: Map<String, TranslationDto>? = null,
)

/** Mirrors backend `PackageListItem` (used inside OrderListItem). */
@Serializable
data class OrderPackageSummaryDto(
    val id: String? = null,
    val name: String? = null,
    val description: String? = null,
    val price: Double = 0.0,
    /** Per-language name/description overrides keyed by 2-letter code (T-0395). */
    val translations: Map<String, TranslationDto>? = null,
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
    /** Per-language name/description overrides keyed by 2-letter code (T-0395). */
    val translations: Map<String, TranslationDto>? = null,
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
    val feeRate: Double,
    val refundAmount: Double,
    val totalPrice: Double,
    val refundInitiated: Boolean,
)

/**
 * Mirrors backend `GetCancellationFeePreview.Response` — what cancelling this
 * order would cost, asked before the customer commits.
 *
 * [tier] is the wire ordinal of `CancellationFeeTier` and is the only thing
 * that decides what the sheet says; `null` means the server sent a tier this
 * build has no copy for, which the sheet renders as "we could not check"
 * rather than as any particular outcome. The two facts behind the number — the
 * caller's own free-cancellation window and whether a cleaner has been pulled
 * onto the job — exist only server-side.
 */
@Serializable
data class CancellationFeePreviewDto(
    val orderId: String? = null,
    val tier: Int? = null,
    val feeRate: Double,
    val feeAmount: Double,
    val refundAmount: Double,
    val totalPrice: Double,
    val currencyCode: String? = null,
    val expressWaiverForfeitedOnCancel: Boolean,
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
    val tags: List<ReviewTag> = emptyList(),
)

/** Mirrors backend `GetOrderPhotos.Response`. */
@Serializable
data class OrderPhotosResponse(
    val photos: List<OrderPhotoDto> = emptyList(),
    val beforePhotoCount: Int = 0,
    val afterPhotoCount: Int = 0,
)

/**
 * Mirrors the backend order-photo DTO. The photo type is an INT on the wire, never a string, and the
 * blob URL is **an already-signed SAS with a short TTL** — pass it straight to the image loader and add
 * no auth header. -> /flows/execution-and-completion
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
