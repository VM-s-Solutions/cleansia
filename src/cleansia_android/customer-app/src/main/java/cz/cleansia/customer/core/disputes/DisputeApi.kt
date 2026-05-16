package cz.cleansia.customer.core.disputes

import cz.cleansia.customer.api.client.DisputeApi as GenDisputeApi
import cz.cleansia.customer.api.model.AddDisputeMessageCommand as GenAddDisputeMessageCommand
import cz.cleansia.customer.api.model.Code as GenCode
import cz.cleansia.customer.api.model.CreateDisputeCommand as GenCreateDisputeCommand
import cz.cleansia.customer.api.model.DisputeDetails as GenDisputeDetails
import cz.cleansia.customer.api.model.DisputeEvidenceDto as GenDisputeEvidenceDto
import cz.cleansia.customer.api.model.DisputeListItem as GenDisputeListItem
import cz.cleansia.customer.api.model.DisputeMessageDto as GenDisputeMessageDto
import cz.cleansia.customer.api.model.DisputeReason as GenDisputeReason
import cz.cleansia.customer.api.model.PagedDataOfDisputeListItem as GenPagedDisputes
import cz.cleansia.customer.api.model.UploadDisputeEvidenceResponse as GenUploadDisputeEvidenceResponse
import cz.cleansia.customer.core.user.CodeDto
import okhttp3.MultipartBody
import okhttp3.RequestBody
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
) {
    suspend fun getPaged(offset: Int = 0, limit: Int = 20): Response<DisputeListResponseDto> {
        val raw = disputeApi.disputeGetPagedDisputes(offset = offset, limit = limit)
        return raw.mapBody { it.toAppDto() }
    }

    suspend fun getById(id: String): Response<DisputeDetailsDto> {
        val raw = disputeApi.disputeGetDisputeById(disputeId = id)
        return raw.mapBody { it?.toAppDto() }
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
        // Generated client takes `disputeId` as a `String?`, so we extract the
        // single text part the hand-written API was sending via @Part("disputeId")
        // RequestBody. Trade-off: small, but it keeps the adapter API unchanged
        // for the existing `DisputeRepository` call site.
        val disputeIdString = disputeId.asString()
        val raw = disputeApi.disputeUploadEvidence(disputeId = disputeIdString, file = file)
        return raw.mapBody { it?.toAppDto() }
    }
}

private fun RequestBody.asString(): String? = try {
    val buffer = okio.Buffer()
    writeTo(buffer)
    buffer.readUtf8()
} catch (_: Throwable) {
    null
}

private inline fun <T, R : Any> Response<T>.mapBody(transform: (T?) -> R?): Response<R> =
    if (isSuccessful) Response.success(transform(body()), raw())
    else @Suppress("UNCHECKED_CAST") (this as Response<R>)

// ─── Generated → app DTO mappers ───

private fun GenPagedDisputes?.toAppDto(): DisputeListResponseDto = DisputeListResponseDto(
    pageNumber = this?.pageNumber ?: 0,
    pageSize = this?.pageSize ?: 0,
    total = this?.total ?: 0,
    data = this?.`data`?.map { it.toAppDto() }.orEmpty(),
)

private fun GenDisputeListItem.toAppDto(): DisputeListItemDto = DisputeListItemDto(
    id = id,
    orderId = orderId,
    displayOrderNumber = displayOrderNumber,
    customerName = customerName,
    customerEmail = customerEmail,
    reason = reason?.toAppDto(),
    status = status?.toAppDto(),
    createdOn = createdOn?.toString(),
    resolvedOn = resolvedOn?.toString(),
    refundAmount = refundAmount,
)

private fun GenDisputeDetails.toAppDto(): DisputeDetailsDto = DisputeDetailsDto(
    id = id,
    orderId = orderId,
    displayOrderNumber = displayOrderNumber,
    userId = null, // not on generated DTO
    customerName = customerName,
    customerEmail = customerEmail,
    reason = reason?.toAppDto(),
    description = description,
    status = status?.toAppDto(),
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
)

private fun GenCode.toAppDto(): CodeDto = CodeDto(
    type = type.orEmpty(),
    name = name.orEmpty(),
    value = `value` ?: 0,
)

private fun GenDisputeMessageDto.toAppDto(): DisputeMessageDto = DisputeMessageDto(
    id = id,
    message = message,
    authorId = authorId,
    authorName = authorName,
    isStaffMessage = isStaffMessage ?: false,
    createdOn = createdOn?.toString(),
)

private fun GenDisputeEvidenceDto.toAppDto(): DisputeEvidenceDto = DisputeEvidenceDto(
    id = id,
    fileName = fileName,
    filePath = filePath,
    blobUrl = blobUrl,
    uploadedBy = uploadedBy,
    uploadedOn = uploadedOn?.toString(),
)

private fun GenUploadDisputeEvidenceResponse.toAppDto(): UploadDisputeEvidenceResponse =
    UploadDisputeEvidenceResponse(
        evidenceId = evidenceId,
        fileName = fileName,
        blobUrl = blobUrl,
        uploadedOn = uploadedOn?.toString(),
    )

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
