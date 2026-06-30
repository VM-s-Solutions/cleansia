import CleansiaCore
import CleansiaCustomerApi
import Foundation

protocol DisputeClient: Sendable {
    func getPaged(offset: Int, limit: Int) async -> ApiResult<DisputesPage>
    func getById(disputeId: String) async -> ApiResult<DisputeDetail>
    func create(orderId: String, reason: Int, description: String) async -> ApiResult<String>
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
        let result = await apiResult(mapError: ApiError.fromGenerated) {
            try await CustomerDisputeAPI.disputeGetDisputeById(disputeId: disputeId)
        }
        return result.flatMap { details in
            guard let detail = details.toDetail() else {
                return .failure(ApiError(code: "dispute.malformed"))
            }
            return .success(detail)
        }
    }

    func create(orderId: String, reason: Int, description: String) async -> ApiResult<String> {
        let command = CreateDisputeCommand(
            orderId: orderId,
            reason: DisputeReason(rawValue: reason),
            description: description
        )
        return await apiResult(mapError: ApiError.fromGenerated) {
            try await CustomerDisputeAPI.disputeCreateDispute(createDisputeCommand: command)
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
