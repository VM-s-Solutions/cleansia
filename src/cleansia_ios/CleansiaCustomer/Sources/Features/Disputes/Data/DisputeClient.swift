import CleansiaCore
import CleansiaCustomerApi
import Foundation

protocol DisputeClient: Sendable {
    func getPaged(offset: Int, limit: Int) async -> ApiResult<DisputesPage>
    func getById(disputeId: String) async -> ApiResult<DisputeDetail>
    func create(
        orderId: String,
        reason: Int,
        description: String,
        lines: [OrderItemLine]
    ) async -> ApiResult<String>
    func addMessage(disputeId: String, message: String) async -> ApiResult<Void>
    func uploadEvidence(disputeId: String, file: URL) async -> ApiResult<DisputeEvidence>
}

struct LiveDisputeClient: DisputeClient {
    func getPaged(offset: Int, limit: Int) async -> ApiResult<DisputesPage> {
        let result = await apiResult(mapError: ApiError.fromGenerated) {
            try await CustomerDisputeAPI.disputeGetPagedDisputes(offset: offset, limit: limit)
        }
        return result.map { $0.toDisputesPage() }
    }

    func getById(disputeId: String) async -> ApiResult<DisputeDetail> {
        await apiResult(mapError: ApiError.fromGenerated) {
            let details = try await CustomerDisputeAPI.disputeGetDisputeById(disputeId: disputeId)
            guard let detail = try details.toDetail() else {
                throw ApiError(code: "dispute.malformed")
            }
            return detail
        }
    }

    func create(
        orderId: String,
        reason: Int,
        description: String,
        lines: [OrderItemLine]
    ) async -> ApiResult<String> {
        let command = CreateDisputeCommand(
            orderId: orderId,
            reason: DisputeReason(rawValue: reason),
            description: description,
            // nil, not [], when the customer ticked nothing. The backend treats the two the same, but
            // every other client omits the field entirely and the wire shapes should not diverge.
            lines: lines.isEmpty ? nil : lines.map {
                CreateDisputeDisputeLineSelection(serviceId: $0.serviceId, packageId: $0.packageId)
            }
        )
        return await apiResult(mapError: ApiError.fromGenerated) {
            // The endpoint answers `{ "disputeId": "..." }`, never a bare string. The id is not
            // cosmetic: the evidence upload that follows is addressed to it, so substituting an
            // empty stand-in would strand the customer's photo instead of failing visibly.
            // Android met the same wire shape — see `DisputeApi.create`.
            let response = try await CustomerDisputeAPI.disputeCreateDispute(createDisputeCommand: command)
            guard let disputeId = response.disputeId, !disputeId.isEmpty else {
                throw ApiError(code: "dispute.malformed")
            }
            return disputeId
        }
    }

    func addMessage(disputeId: String, message: String) async -> ApiResult<Void> {
        let command = AddDisputeMessageCommand(
            disputeId: disputeId,
            message: message,
            isStaffMessage: false
        )
        return await apiResult(mapError: ApiError.fromGenerated) {
            try await CustomerDisputeAPI.disputeAddMessage(addDisputeMessageCommand: command)
        }
    }

    /// One multipart call per file. The generated `disputeUploadEvidence` takes a
    /// `file: URL` whose extension drives the multipart filename + MIME (the
    /// generated `URLSessionImplementations` does real multipart on the
    /// `multipart/form-data` content type). The blob name is SERVER-controlled
    /// (`{disputeId}/{Guid}{ext}`) — the client filename contributes only its
    /// extension, so there is no path-traversal surface (Gate-SEC R12).
    func uploadEvidence(disputeId: String, file: URL) async -> ApiResult<DisputeEvidence> {
        let result = await apiResult(mapError: ApiError.fromGenerated) {
            try await CustomerDisputeAPI.disputeUploadEvidence(disputeId: disputeId, file: file)
        }
        return result.map { response in
            DisputeEvidence(
                id: response.evidenceId ?? "",
                fileName: response.fileName,
                blobURL: response.blobUrl,
                uploadedOn: response.uploadedOn
            )
        }
    }
}
