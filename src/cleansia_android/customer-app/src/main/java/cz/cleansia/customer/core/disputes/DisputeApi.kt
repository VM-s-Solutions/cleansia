package cz.cleansia.customer.core.disputes

import cz.cleansia.customer.api.client.DisputeApi as GenDisputeApi
import cz.cleansia.customer.api.model.AddDisputeMessageCommand as GenAddDisputeMessageCommand
import cz.cleansia.customer.api.model.CreateDisputeCommand as GenCreateDisputeCommand
import cz.cleansia.customer.api.model.DisputeDetails as GenDisputeDetails
import cz.cleansia.customer.api.model.DisputeEvidenceDto as GenDisputeEvidenceDto
import cz.cleansia.customer.api.model.DisputeListItem as GenDisputeListItem
import cz.cleansia.customer.api.model.DisputeMessageDto as GenDisputeMessageDto
import cz.cleansia.customer.api.model.DisputeReason as GenDisputeReason
import cz.cleansia.customer.api.model.PagedDataOfDisputeListItem as GenPagedDisputes
import cz.cleansia.customer.api.model.UploadDisputeEvidenceResponse as GenUploadDisputeEvidenceResponse
import cz.cleansia.customer.core.user.toAppDto
import okhttp3.MultipartBody
import okhttp3.RequestBody
import cz.cleansia.core.network.mapWire
import cz.cleansia.core.network.required
import retrofit2.Response

/**
 * Adapter over the OpenAPI-generated [GenDisputeApi]. Maps wire DTOs into the
 * hand-written shapes the dispute screens consume (DisputeListItemDto /
 * DisputeDetailsDto). The hand-written details DTO carries a few extra fields
 * (`userId`, `resolvedBy`, `stripeDisputeId`, `createdBy`, `updatedBy`) that
 * the generated `DisputeDetails` no longer exposes — we set those to null
 * since none of the customer screens read them.
 */
class DisputeApi(
    private val disputeApi: GenDisputeApi,
    private val evidenceUploadApi: DisputeEvidenceUploadApi,
) {
    suspend fun getPaged(offset: Int = 0, limit: Int = 20): Response<DisputeListResponseDto> {
        val raw = disputeApi.disputeGetPagedDisputes(offset = offset, limit = limit)
        return raw.mapWire { it.toAppDto() }
    }

    suspend fun getById(id: String): Response<DisputeDetailsDto> {
        val raw = disputeApi.disputeGetDisputeById(disputeId = id)
        return raw.mapWire { it.toAppDto() }
    }

    suspend fun create(body: CreateDisputeRequest): Response<String> =
        disputeApi.disputeCreateDispute(
            createDisputeCommand = GenCreateDisputeCommand(
                orderId = body.orderId,
                reason = body.reason.toWireReason(),
                description = body.description,
            ),
        )

    suspend fun addMessage(body: AddDisputeMessageRequest): Response<Unit> =
        disputeApi.disputeAddMessage(
            addDisputeMessageCommand = GenAddDisputeMessageCommand(
                disputeId = body.disputeId,
                message = body.message,
                isStaffMessage = body.isStaffMessage,
            ),
        )

    suspend fun uploadEvidence(
        disputeId: RequestBody,
        file: MultipartBody.Part,
    ): Response<UploadDisputeEvidenceResponse> {
        // Routed around the generated client on purpose — see DisputeEvidenceUploadApi. Its signature
        // takes the id as a String, which this app's only converter factory serialises as JSON, so the
        // part arrived quoted and no upload ever matched a dispute.
        val raw = evidenceUploadApi.uploadEvidence(disputeId = disputeId, file = file)
        return raw.mapWire { it.toAppDto() }
    }
}

// ─── Generated → app DTO mappers ───

/**
 * `total` is both the count `DisputesListScreen` renders and the stop condition
 * [DisputeRepository.loadNextPage] pages against, so a defaulted zero ends pagination while claiming
 * the customer has no disputes.
 *
 * The two rulings compose as they do on the orders list, and for the same reason: nothing here sums
 * the rows — `total` is the server's own count and `refundAmount` is `nullable: true`, since an
 * unresolved dispute has no refund — so an unidentifiable row is dropped rather than costing the
 * customer every other dispute the server answered, while a surviving row whose own content is broken
 * refuses, and because the row is an element of the page that refuses the page.
 */
private fun GenPagedDisputes?.toAppDto(): DisputeListResponseDto {
    val page = required("PagedDataOfDisputeListItem")
    return DisputeListResponseDto(
        pageNumber = page.pageNumber.required("pageNumber"),
        pageSize = page.pageSize.required("pageSize"),
        total = page.total.required("total"),
        // `.orEmpty()` on the page's own member, never on the body above it: an absent `data` array
        // beside a present `total` is an empty page, where an absent BODY is no answer at all.
        receivedCount = page.`data`.orEmpty().size,
        data = page.`data`.orEmpty()
            .filter { it.id != null }
            .map { it.toAppDtoOrRefuse() },
    )
}

private fun GenDisputeListItem.toAppDtoOrRefuse(): DisputeListItemDto {
    return DisputeListItemDto(
        id = id,
        orderId = orderId,
        displayOrderNumber = displayOrderNumber,
        customerName = customerName,
        customerEmail = customerEmail,
        reason = reason?.toAppDto().required("reason"),
        status = status?.toAppDto().required("status"),
        createdOn = createdOn?.toString(),
        resolvedOn = resolvedOn?.toString(),
        refundAmount = refundAmount,
    )
}

/**
 * One dispute, so there is no rest of the page to keep: the detail refuses outright. The thread is
 * the content rather than decoration over it, so a message that cannot be attributed refuses the
 * dispute with it.
 */
private fun GenDisputeDetails?.toAppDto(): DisputeDetailsDto {
    val details = required("DisputeDetails")
    return with(details) { DisputeDetailsDto(
        id = id,
        orderId = orderId,
        displayOrderNumber = displayOrderNumber,
        userId = null, // not on generated DTO
        customerName = customerName,
        customerEmail = customerEmail,
        reason = reason?.toAppDto().required("reason"),
        description = description,
        status = status?.toAppDto().required("status"),
        resolutionNotes = resolutionNotes,
        refundAmount = refundAmount,
        resolvedBy = null, // not on generated DTO
        resolvedOn = resolvedOn?.toString(),
        stripeDisputeId = null, // not on generated DTO
        messages = messages?.map { it.toAppDto() },
        evidence = evidence?.map { it.toAppDto() },
        createdOn = createdOn?.toString(),
        createdBy = null,
        updatedOn = updatedOn?.toString(),
        updatedBy = null,
    ) }
}

/**
 * `isStaffMessage` decides which side of the thread a message is drawn on, so `false` renders
 * Cleansia's reply as something the customer wrote themselves — on the one screen where they are
 * waiting to hear whether their money is coming back, the answer arrives and reads as their own
 * words.
 */
private fun GenDisputeMessageDto.toAppDto(): DisputeMessageDto {
    return DisputeMessageDto(
        id = id,
        message = message,
        authorId = authorId,
        authorName = authorName,
        isStaffMessage = isStaffMessage.required("isStaffMessage"),
        createdOn = createdOn?.toString(),
    )
}

private fun GenDisputeEvidenceDto.toAppDto(): DisputeEvidenceDto = DisputeEvidenceDto(
    id = id,
    fileName = fileName,
    filePath = filePath,
    blobUrl = blobUrl,
    uploadedBy = uploadedBy,
    uploadedOn = uploadedOn?.toString(),
)

private fun GenUploadDisputeEvidenceResponse?.toAppDto(): UploadDisputeEvidenceResponse =
    with(required("UploadDisputeEvidenceResponse")) {
        UploadDisputeEvidenceResponse(
            evidenceId = evidenceId,
            fileName = fileName,
            blobUrl = blobUrl,
            uploadedOn = uploadedOn?.toString(),
        )
    }

private fun Int.toWireReason(): GenDisputeReason? = when (this) {
    1 -> GenDisputeReason._1
    2 -> GenDisputeReason._2
    3 -> GenDisputeReason._3
    4 -> GenDisputeReason._4
    5 -> GenDisputeReason._5
    6 -> GenDisputeReason._6
    7 -> GenDisputeReason._7
    else -> null
}
