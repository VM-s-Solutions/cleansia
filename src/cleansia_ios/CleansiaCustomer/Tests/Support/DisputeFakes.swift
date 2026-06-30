import CleansiaCore
import Foundation
@testable import CleansiaCustomer

final class FakeDisputeClient: DisputeClient, @unchecked Sendable {
    var pages: [DisputesPage] = []
    var pageError: ApiError?
    private(set) var pageRequests: [(offset: Int, limit: Int)] = []

    var detailResults: [ApiResult<DisputeDetail>] = []
    private(set) var detailCallCount = 0

    var createResult: ApiResult<String> = .success("dispute-new")
    private(set) var createCallCount = 0
    private(set) var lastCreate: (orderId: String, reason: Int, description: String)?

    var addMessageResult: ApiResult<Void> = .success(())
    private(set) var addMessageCallCount = 0
    private(set) var lastMessage: String?

    var uploadResult: ApiResult<DisputeEvidence> = .success(
        DisputeEvidence(id: "ev", fileName: "f.jpg", blobURL: nil, uploadedOn: nil)
    )
    private(set) var uploadCallCount = 0
    private(set) var uploadedFiles: [URL] = []

    func getPaged(offset: Int, limit: Int) async -> ApiResult<DisputesPage> {
        pageRequests.append((offset, limit))
        if let pageError { return .failure(pageError) }
        let index = min(pageRequests.count - 1, pages.count - 1)
        guard index >= 0 else { return .success(DisputesPage(items: [], total: 0)) }
        return .success(pages[index])
    }

    func getById(disputeId _: String) async -> ApiResult<DisputeDetail> {
        defer { detailCallCount += 1 }
        let index = min(detailCallCount, detailResults.count - 1)
        guard index >= 0 else { return .failure(ApiError(httpStatus: 500)) }
        return detailResults[index]
    }

    func create(orderId: String, reason: Int, description: String) async -> ApiResult<String> {
        createCallCount += 1
        lastCreate = (orderId, reason, description)
        return createResult
    }

    func addMessage(disputeId _: String, message: String) async -> ApiResult<Void> {
        addMessageCallCount += 1
        lastMessage = message
        return addMessageResult
    }

    func uploadEvidence(disputeId _: String, file: URL) async -> ApiResult<DisputeEvidence> {
        uploadCallCount += 1
        uploadedFiles.append(file)
        return uploadResult
    }
}

enum DisputeFixtures {
    static func entry(id: String, statusValue: Int = 1) -> DisputeListEntry {
        DisputeListEntry(
            id: id,
            displayOrderNumber: "1042",
            reasonName: "Quality issue",
            statusName: "Pending",
            statusValue: statusValue,
            createdOn: Date(timeIntervalSince1970: 0)
        )
    }

    static func detail(id: String = "dispute-1", statusValue: Int = 1) -> DisputeDetail {
        DisputeDetail(
            id: id,
            displayOrderNumber: "1042",
            reasonName: "Quality issue",
            description: "Cleaner skipped the kitchen entirely",
            statusName: "Pending",
            statusValue: statusValue,
            createdOn: Date(timeIntervalSince1970: 0),
            messages: [],
            evidence: []
        )
    }
}
