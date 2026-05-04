package cz.cleansia.customer.core.disputes

import cz.cleansia.customer.core.user.CodeDto
import kotlinx.serialization.Serializable

/**
 * Wire DTOs for the customer Dispute endpoints — the customer-facing
 * complaint channel, distinct from `OrderIssue` (which is cleaner-authored).
 *
 * Routes mirror [Cleansia.Web.Customer.Controllers.DisputeController]:
 *  - `POST /api/Dispute/Create`             → `string` (the new dispute id)
 *  - `GET  /api/Dispute/GetPaged`           → `PagedData<DisputeListItem>`
 *  - `GET  /api/Dispute/GetById/{disputeId}` → `DisputeDetails` (PATH param!)
 *  - `POST /api/Dispute/AddMessage`         → 200 OK, no body
 *
 * Field shapes match the backend records verbatim — see
 * `src/Cleansia.Core.AppServices/Features/Disputes/DTOs/` and
 * `src/Cleansia.Core.AppServices/Mappers/DisputeMappers.cs`.
 *
 * Note that list-item and details wrap the `Reason` / `Status` enum values in
 * the shared `Code` DTO shape (`{ type, name, value }`) — we reuse the
 * existing [CodeDto] from the user module. The outbound `CreateDisputeRequest`
 * takes the raw `Int` value of [cz.cleansia.customer.core.disputes.DisputeReason]
 * because the backend validator expects an enum, not the wrapper.
 */

/** Paged response wrapper — matches backend `PagedData<DisputeListItem>`. */
@Serializable
data class DisputeListResponseDto(
    val pageNumber: Int = 0,
    val pageSize: Int = 0,
    val total: Int = 0,
    val data: List<DisputeListItemDto> = emptyList(),
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
