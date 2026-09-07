package cz.cleansia.customer.core.disputes

import cz.cleansia.customer.core.user.CodeDto
import kotlinx.serialization.Serializable

/**
 * Wire DTOs for the customer Dispute endpoints — the customer-facing complaint channel, distinct from
 * the cleaner-authored order issue.
 *
 * Field shapes match the backend records verbatim. Enum values arrive wrapped in the shared code shape,
 * not as bare values. -> /flows/cancellation-refund-dispute
 */

/** Paged response wrapper — matches backend `PagedData<DisputeListItem>`. */
@Serializable
data class DisputeListResponseDto(
    val pageNumber: Int = 0,
    val pageSize: Int = 0,
    val total: Int = 0,
    val data: List<DisputeListItemDto> = emptyList(),
    /**
     * Rows the server SENT, which is not [data].size once the mapper drops an unidentifiable one.
     * Pagination is offset-based against the server's [total], so both the offset and the stop
     * condition have to count what the server counted or neither ever reaches it.
     */
    val receivedCount: Int = data.size,
)

/**
 * Mirrors backend `DisputeListItem`.
 *
 * `customerName` is pre-composed on the server as "first last" and
 * `refundAmount` is populated only after the dispute is resolved.
 */
@Serializable
data class DisputeListItemDto(
    val id: String? = null,
    val orderId: String? = null,
    val displayOrderNumber: String? = null,
    val customerName: String? = null,
    val customerEmail: String? = null,
    val reason: CodeDto? = null,
    val status: CodeDto? = null,
    val createdOn: String? = null,
    val resolvedOn: String? = null,
    val refundAmount: Double? = null,
    /** The order's currency code, so a refund figure can be printed with its unit. */
    val currency: String? = null,
    /**
     * Whether the customer filed inside the 24-hour deadline. False does not mean rejected — a late
     * dispute is still accepted and investigated if it is serious, per the owner's 2026-09-05 ruling.
     */
    val filedWithinWindow: Boolean? = null,
    /** The items the customer named as not done properly. Empty on a whole-job dispute. */
    val lines: List<DisputeLineDto>? = null,
)

/** Mirrors backend `DisputeDetails`. */
@Serializable
data class DisputeDetailsDto(
    val id: String? = null,
    val orderId: String? = null,
    val displayOrderNumber: String? = null,
    val userId: String? = null,
    val customerName: String? = null,
    val customerEmail: String? = null,
    val reason: CodeDto? = null,
    val description: String? = null,
    val status: CodeDto? = null,
    val resolutionNotes: String? = null,
    val refundAmount: Double? = null,
    val resolvedBy: String? = null,
    val resolvedOn: String? = null,
    val stripeDisputeId: String? = null,
    val messages: List<DisputeMessageDto>? = null,
    val evidence: List<DisputeEvidenceDto>? = null,
    val createdOn: String? = null,
    val createdBy: String? = null,
    val updatedOn: String? = null,
    val updatedBy: String? = null,
)

/**
 * Mirrors backend `DisputeMessageDto`. Backend uses `message` (not `content`)
 * as the body field name and `isStaffMessage: Boolean` instead of a string
 * role. `authorName` is now populated by the backend mapper as
 * `firstName + " " + lastName` (Wave 3 backend foundations).
 */
@Serializable
data class DisputeMessageDto(
    val id: String? = null,
    val message: String? = null,
    val authorId: String? = null,
    val authorName: String? = null,
    val isStaffMessage: Boolean = false,
    val createdOn: String? = null,
)

/**
 * Mirrors backend `DisputeEvidenceDto`. `blobUrl` is a fully-signed Azure SAS
 * URL with 1h TTL — pass directly to Coil / FileProvider. `filePath` is the
 * raw blob name (kept for debugging; not used at the UI layer).
 */
@Serializable
data class DisputeEvidenceDto(
    val id: String? = null,
    val fileName: String? = null,
    val filePath: String? = null,
    val blobUrl: String? = null,
    val uploadedBy: String? = null,
    val uploadedOn: String? = null,
)

/** Mirrors backend `UploadDisputeEvidence.Response`. */
@Serializable
data class UploadDisputeEvidenceResponse(
    val evidenceId: String? = null,
    val fileName: String? = null,
    val blobUrl: String? = null,
    val uploadedOn: String? = null,
)

/**
 * Mirrors backend `CreateDispute.Command`. `UserId` is enriched server-side
 * from the JWT — we don't send it. `Reason` is the raw int value from
 * the backend `DisputeReason` enum (1..7). `Description` is validated at
 * 10..2000 chars; do the same check client-side before sending.
 */
@Serializable
data class CreateDisputeRequest(
    val orderId: String,
    val reason: Int,
    val description: String,
    /**
     * Which items of the order were not done properly. Optional and usually absent — a dispute about
     * the whole job, or about a charge, names none. Sent as null rather than an empty list so the
     * wire shape matches what the web client sends; the backend treats the two the same.
     */
    val lines: List<DisputeLineRequest>? = null,
)

/**
 * Mirrors backend `DisputeLineDto` — an item the customer named, as the server reports it back.
 *
 * Carries the NAMES as well as the ids, deliberately: the detail screen renders these long after the
 * order that produced them may have been reshaped, and resolving a name from an id at read time would
 * make the dispute's own record depend on the catalogue not having changed.
 */
@Serializable
data class DisputeLineDto(
    val serviceId: String? = null,
    val serviceName: String? = null,
    val packageId: String? = null,
    val packageName: String? = null,
)

/**
 * Mirrors backend `CreateDispute.DisputeLineSelection`.
 *
 * The identity of an order item is the PAIR. A service bought on its own leaves [packageId] null; a
 * service that came inside a package carries both, because the same service can appear twice on one
 * order and an admin refunding one must not refund the other. The backend validates every line is
 * actually on the order and answers `dispute.line_not_on_order` if it is not.
 */
@Serializable
data class DisputeLineRequest(
    val serviceId: String,
    val packageId: String? = null,
)

/**
 * Mirrors backend `AddDisputeMessage.Command`. Backend uses `message` for the
 * body field and expects an `isStaffMessage` flag — we always send `false`
 * from the customer app.
 */
@Serializable
data class AddDisputeMessageRequest(
    val disputeId: String,
    val message: String,
    val isStaffMessage: Boolean = false,
)
