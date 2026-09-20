import CleansiaCore
import CleansiaPartnerApi
import Foundation
@testable import CleansiaPartner

@MainActor
final class FakePartnerOrderClient: PartnerOrderClient {
    var employeeIdResult: ApiResult<String> = .success("emp-self")
    var pagedResult: ApiResult<[OrderListItem]> = .success([])
    var byIdResult: ApiResult<OrderItem> = .success(.wireComplete())
    var commandResult: ApiResult<Void> = .success(())

    private(set) var queries: [OrderPageQuery] = []
    private(set) var employeeIdCallCount = 0
    private(set) var getPagedCallCount = 0
    private(set) var getByIdCallCount = 0

    /// Each lifecycle command appends `(command, orderId)` here — the test
    /// asserts the carried id is the acted-on id and nothing else (O1/O2).
    private(set) var commands: [(name: String, orderId: String)] = []

    /// Note/issue mutations appended here for the notes-section tests.
    private(set) var noteCommands: [(name: String, id: String?, content: String?)] = []

    var getPhotosResult: ApiResult<[OrderPhoto]> = .success([])
    private(set) var getPhotosCallCount = 0

    /// Each photo mutation appends `(name, orderId, photoId, photoType, hasBase64)`
    /// — the ownership test asserts the carried ids + that no employeeId leaks.
    private(set) var photoCommands: [(
        name: String,
        orderId: String?,
        photoId: String?,
        photoType: PhotoType?,
        hasBase64: Bool
    )] = []

    /// When set, the next command suspends until `resumeCommand()` so a test can
    /// hold one mutation mid-flight and fire a second (re-entry guard).
    var suspendCommands = false
    private var commandGate: CheckedContinuation<Void, Never>?

    /// Invoked at the top of every `getPaged` — lets a test observe VM state
    /// mid-refresh (e.g. the in-flight hold across the post-success refetch).
    var onGetPaged: (() -> Void)?

    /// Same observation hook for `getById` (the detail VM's refetch).
    var onGetById: (() -> Void)?

    var pendingOffersResult: ApiResult<[PendingOfferItem]> = .success([])
    var declineResult: ApiResult<Void> = .success(())
    private(set) var pendingOffersCallCount = 0

    /// The reservation writes, recorded separately from the lifecycle commands so a test can assert
    /// that a confirm went to `takeOrder` and nowhere near the decline endpoint.
    private(set) var pendingOfferCommands: [(name: String, orderId: String)] = []

    /// Fired before the decline returns — lets a test move the server's rows on so the refetch the
    /// view model always issues sees the write it just made.
    var onDeclinePreferredOffer: ((String) -> Void)?

    var previewResult: ApiResult<WorkContract> = .success(.sample())
    var contractResult: ApiResult<WorkContract> = .success(.sample(acceptance: .sample()))
    /// Every contract read, by the id it was keyed on and the language asked for.
    private(set) var previewRequests: [(orderId: String, language: String)] = []
    private(set) var contractRequests: [(acceptanceId: String, language: String)] = []
    /// The text id each take or standalone acceptance echoed, in call order — the pin that a take
    /// carries the previewed text and nothing else.
    private(set) var echoedTextIds: [String] = []

    func currentEmployeeId() async -> ApiResult<String> {
        employeeIdCallCount += 1
        return employeeIdResult
    }

    func getPaged(_ query: OrderPageQuery) async -> ApiResult<[OrderListItem]> {
        onGetPaged?()
        getPagedCallCount += 1
        queries.append(query)
        return pagedResult
    }

    /// Holds the WIRE shape and maps it through the real initializer, so a fixture that breaks the
    /// contract produces exactly the refusal production would.
    func getById(orderId _: String) async -> ApiResult<OrderDetail> {
        onGetById?()
        getByIdCallCount += 1
        switch byIdResult {
        case let .success(item):
            return await apiResult { try OrderDetail(item) }
        case let .failure(error):
            return .failure(error)
        }
    }

    func resumeCommand() {
        commandGate?.resume()
        commandGate = nil
    }

    private func record(_ name: String, _ orderId: String) async -> ApiResult<Void> {
        commands.append((name, orderId))
        return await gated()
    }

    private func recordNote(_ name: String, id: String?, content: String?) async -> ApiResult<Void> {
        noteCommands.append((name: name, id: id, content: content))
        return await gated()
    }

    private func gated() async -> ApiResult<Void> {
        if suspendCommands {
            await withCheckedContinuation { commandGate = $0 }
        }
        return commandResult
    }

    func myPendingOffers() async -> ApiResult<[PendingOfferItem]> {
        pendingOffersCallCount += 1
        return pendingOffersResult
    }

    /// Gated like the lifecycle commands, so a test can hold a release mid-flight and try a confirm
    /// against it; the confirm itself never reaches the gate, because it only opens the sheet.
    func declinePreferredOffer(orderId: String) async -> ApiResult<Void> {
        pendingOfferCommands.append((name: "declinePreferredOffer", orderId: orderId))
        onDeclinePreferredOffer?(orderId)
        if suspendCommands {
            await withCheckedContinuation { commandGate = $0 }
        }
        return declineResult
    }

    func takeOrder(orderId: String, acceptedWorkContractTextId: String) async -> ApiResult<Void> {
        echoedTextIds.append(acceptedWorkContractTextId)
        return await record("take", orderId)
    }

    func acceptWorkContract(orderId: String, acceptedWorkContractTextId: String) async -> ApiResult<Void> {
        echoedTextIds.append(acceptedWorkContractTextId)
        return await record("acceptWorkContract", orderId)
    }

    func getWorkContractPreview(orderId: String, language: String) async -> ApiResult<WorkContract> {
        previewRequests.append((orderId: orderId, language: language))
        return previewResult
    }

    func getWorkContract(acceptanceId: String, language: String) async -> ApiResult<WorkContract> {
        contractRequests.append((acceptanceId: acceptanceId, language: language))
        return contractResult
    }

    func notifyOnTheWay(orderId: String) async -> ApiResult<Void> {
        await record("notifyOnTheWay", orderId)
    }

    func startOrder(orderId: String) async -> ApiResult<Void> {
        await record("start", orderId)
    }

    func markCashCollected(orderId: String) async -> ApiResult<Void> {
        await record("markCashCollected", orderId)
    }

    func completeOrder(orderId: String, actualMinutes _: Int?, notes _: String?) async -> ApiResult<Void> {
        await record("complete", orderId)
    }

    func addNote(orderId _: String, content: String) async -> ApiResult<Void> {
        await recordNote("addNote", id: nil, content: content)
    }

    func updateNote(orderId _: String, noteId: String, content: String) async -> ApiResult<Void> {
        await recordNote("updateNote", id: noteId, content: content)
    }

    func deleteNote(orderId _: String, noteId: String) async -> ApiResult<Void> {
        await recordNote("deleteNote", id: noteId, content: nil)
    }

    func reportIssue(orderId _: String, description: String) async -> ApiResult<Void> {
        await recordNote("reportIssue", id: nil, content: description)
    }

    func updateIssue(orderId _: String, issueId: String, description: String) async -> ApiResult<Void> {
        await recordNote("updateIssue", id: issueId, content: description)
    }

    func deleteIssue(orderId _: String, issueId: String) async -> ApiResult<Void> {
        await recordNote("deleteIssue", id: issueId, content: nil)
    }

    func getPhotos(orderId _: String) async -> ApiResult<[OrderPhoto]> {
        getPhotosCallCount += 1
        return getPhotosResult
    }

    func savePhoto(
        orderId: String,
        photoType: PhotoType,
        base64Content: String,
        fileName _: String,
        contentType _: String
    ) async -> ApiResult<Void> {
        photoCommands.append((
            name: "savePhoto",
            orderId: orderId,
            photoId: nil,
            photoType: photoType,
            hasBase64: !base64Content.isEmpty
        ))
        return await gated()
    }

    func deletePhoto(photoId: String) async -> ApiResult<Void> {
        photoCommands.append((
            name: "deletePhoto",
            orderId: nil,
            photoId: photoId,
            photoType: nil,
            hasBase64: false
        ))
        return await gated()
    }
}

extension WorkContract {
    static func sample(
        textId: String = "text-1",
        version: String = "2026-09-20",
        language: String? = "en",
        acceptance: WorkContractAcceptanceFacts? = nil
    ) -> WorkContract {
        WorkContract(
            legalDocumentTextId: textId,
            version: version,
            language: language,
            title: "Contract for work",
            contentHtml: "<p>The contract.</p>",
            facts: WorkContractJobFacts(
                orderNumber: "CL-2026-0042",
                cleaningDateTimeUtc: Date(timeIntervalSince1970: 1_786_200_000),
                estimatedMinutes: 180,
                totalPrice: 1850,
                currencyCode: "CZK",
                locationApproximate: "Praha 4 · 14000",
                rooms: 3,
                bathrooms: 1,
                services: ["Standard cleaning"],
                packages: [],
                extraSlugs: ["inside-oven"]
            ),
            acceptance: acceptance
        )
    }
}

extension WorkContractAcceptanceFacts {
    static func sample(acceptedLanguage: String? = "cs") -> WorkContractAcceptanceFacts {
        WorkContractAcceptanceFacts(
            acceptedOn: Date(timeIntervalSince1970: 1_786_000_000),
            documentVersion: "2026-09-20",
            acceptedLanguage: acceptedLanguage
        )
    }
}

extension PendingOfferItem {
    static func sample(
        id: String,
        respondByUtc: Date? = Date(timeIntervalSince1970: 1_786_000_000)
    ) -> PendingOfferItem {
        PendingOfferItem(
            id: id,
            displayOrderNumber: "CL-\(id)",
            cleaningDateTime: Date(timeIntervalSince1970: 1_786_200_000),
            estimatedTime: 120,
            respondByUtc: respondByUtc,
            customerAddressApproximate: "Praha 4 · 14000",
            rooms: 2,
            bathrooms: 1,
            totalPrice: 1200,
            currencyCode: "CZK"
        )
    }
}

extension OrderListItem {
    static func sample(
        id: String,
        status: OrderStatus = ._2,
        pay: Double = 500,
        cleaningDateTime: Date? = nil,
        customerName: String? = nil,
        customerAddress: String? = nil,
        displayOrderNumber: String? = nil,
        latitude: Double? = nil,
        longitude: Double? = nil
    ) -> OrderListItem {
        var item = OrderListItem()
        item.id = id
        item.orderStatus = Code(value: status.rawValue)
        item.estimatedCleanerPay = pay
        item.cleaningDateTime = cleaningDateTime
        item.customerName = customerName
        item.customerAddress = customerAddress
        item.displayOrderNumber = displayOrderNumber
        item.customerAddressLatitude = latitude
        item.customerAddressLongitude = longitude
        return item
    }
}
