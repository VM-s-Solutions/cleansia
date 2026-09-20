import CleansiaCore
import CleansiaCustomerApi
import XCTest
@testable import CleansiaCustomer

/// The customer's read of an accepted contract for work over the app's REAL generated
/// `CustomerOrderAPI` riding the Core spine: the route, the two query members, the session Bearer, the
/// generated DTO surface the regenerated spec carries, and what the mapper refuses to read back.
@MainActor
final class WorkContractWireTests: XCTestCase {
    private static let readPath = "/api/Order/GetWorkContract"

    private static let contractJson = Data("""
    {
      "legalDocumentTextId": "text-cs-1",
      "legalDocumentId": "doc-1",
      "version": "2026-09-20",
      "effectiveFrom": "2026-09-20",
      "language": "cs",
      "title": "Smlouva o dílo",
      "contentHtml": "<p>Smlouva.</p>",
      "facts": {
        "orderNumber": "CL-2026-0042",
        "cleaningDateTimeUtc": "2026-08-12T09:00:00Z",
        "estimatedMinutes": 180,
        "totalPrice": 1850,
        "currencyCode": "CZK",
        "locationApproximate": "Praha 4 · 14000",
        "countryId": "CZE",
        "rooms": 3,
        "bathrooms": 1,
        "services": [{ "id": "svc-1", "name": "Standard cleaning" }, { "id": "svc-2", "name": "" }],
        "packages": [],
        "extraSlugs": ["inside-oven"]
      },
      "acceptance": {
        "acceptedOn": "2026-08-10T08:00:00Z",
        "documentVersion": "2026-09-20",
        "acceptedLanguage": "cs",
        "orderEmployeeId": "seat-1",
        "employeeId": "emp-1"
      }
    }
    """.utf8)

    override func setUp() {
        super.setUp()
        WorkContractWireRecorder.reset()
        WorkContractWireRecorder.answer = (200, Self.contractJson)
        let config = URLSessionConfiguration.ephemeral
        config.protocolClasses = [WorkContractWireRecorder.self]
        let session = URLSession(configuration: config)
        let tokenStore = SignedInTokenStore()
        CustomerGeneratedAuth.install(
            bridge: GeneratedClientAuthBridge(
                headerAdapter: HeaderAdapter(deviceIdProvider: WireDeviceId(), anonymousAllowList: .customer),
                tokenStore: tokenStore,
                sessionRefresher: SessionRefresher(
                    tokenStore: tokenStore,
                    refreshClient: NeverRefreshing(),
                    sessionManager: SessionManager(),
                    sessionScopedCaches: SessionScopedCacheRegistry()
                ),
                session: session
            ),
            basePath: "https://api.test"
        )
        CodableHelper.jsonDecoder = ApiDateDecoding.decoder(primary: { CodableHelper.dateFormatter.date(from: $0) })
    }

    override func tearDown() {
        WorkContractWireRecorder.reset()
        super.tearDown()
    }

    // MARK: the read

    func testTheReadIsAnAuthedGetKeyedOnTheAcceptanceAndTheLanguage() async throws {
        let contract = try await LiveOrderClient().getWorkContract(acceptanceId: "acc-1", language: "cs").get()

        let request = try XCTUnwrap(WorkContractWireRecorder.request(ofPath: Self.readPath))
        XCTAssertEqual(request.httpMethod, "GET")
        XCTAssertEqual(request.value(forHTTPHeaderField: "Authorization"), "Bearer access-1")
        let query = try XCTUnwrap(URLComponents(url: XCTUnwrap(request.url), resolvingAgainstBaseURL: false)?
            .queryItems)
        XCTAssertEqual(Set(query.map(\.name)), ["AcceptanceId", "Language"])
        XCTAssertEqual(query.first { $0.name == "AcceptanceId" }?.value, "acc-1")
        XCTAssertEqual(query.first { $0.name == "Language" }?.value, "cs")
        XCTAssertEqual(contract.legalDocumentTextId, "text-cs-1")
        XCTAssertEqual(contract.version, "2026-09-20")
        XCTAssertEqual(contract.facts.services, ["Standard cleaning"])
        XCTAssertEqual(contract.facts.extraSlugs, ["inside-oven"])
        XCTAssertEqual(contract.acceptance?.documentVersion, "2026-09-20")
        XCTAssertEqual(contract.acceptance?.acceptedLanguage, "cs")
    }

    /// Another customer's acceptance id is `order.not_found` on the server; the client hands the key
    /// through so the screen shows its error state and nothing of the order.
    func testARefusedReadCarriesTheServersKey() async {
        WorkContractWireRecorder.answer = (400, Data(#"{"type":"order.not_found","detail":"Booking not found."}"#.utf8))

        let result = await LiveOrderClient().getWorkContract(acceptanceId: "acc-of-someone-else", language: "cs")

        guard case let .failure(error) = result else { return XCTFail("expected the refusal") }
        XCTAssertEqual(error.code, "order.not_found")
        XCTAssertEqual(error.httpStatus, 400)
    }

    func testTheGeneratedContractModelCarriesTheWholeDtoSurface() {
        XCTAssertEqual(
            Set(WorkContractDto.CodingKeys.allCases.map(\.rawValue)),
            [
                "legalDocumentTextId", "legalDocumentId", "version", "effectiveFrom", "language", "title",
                "contentHtml", "facts", "acceptance"
            ]
        )
        XCTAssertEqual(
            Set(WorkContractFacts.CodingKeys.allCases.map(\.rawValue)),
            [
                "orderNumber", "cleaningDateTimeUtc", "estimatedMinutes", "totalPrice", "currencyCode",
                "locationApproximate", "countryId", "rooms", "bathrooms", "services", "packages", "extraSlugs"
            ]
        )
        XCTAssertEqual(
            Set(WorkContractAcceptanceDetails.CodingKeys.allCases.map(\.rawValue)),
            ["acceptedOn", "documentVersion", "acceptedLanguage", "orderEmployeeId", "employeeId"]
        )
        XCTAssertEqual(
            Set(WorkContractAcceptanceDto.CodingKeys.allCases.map(\.rawValue)),
            ["id", "orderEmployeeId", "employeeId", "acceptedOn", "documentVersion", "language"]
        )
    }

    // MARK: the mapper refuses rather than defaults

    private func fullPayload() throws -> WorkContractDto {
        try CodableHelper.jsonDecoder.decode(WorkContractDto.self, from: Self.contractJson)
    }

    func testAFullyPopulatedContractMaps() throws {
        let contract = try WorkContract(fullPayload())

        XCTAssertEqual(contract.title, "Smlouva o dílo")
        XCTAssertEqual(contract.facts.totalPrice, 1850)
        XCTAssertEqual(contract.facts.estimatedMinutes, 180)
        XCTAssertEqual(contract.facts.rooms, 3)
        XCTAssertEqual(contract.facts.bathrooms, 1)
        XCTAssertEqual(contract.facts.orderNumber, "CL-2026-0042")
    }

    func testEveryMemberTheAcceptanceBindsIsRefusedRatherThanDefaulted() throws {
        let cases: [(String, (inout WorkContractDto) -> Void)] = [
            ("legalDocumentTextId", { $0.legalDocumentTextId = nil }),
            ("version", { $0.version = nil }),
            ("version", { $0.version = " " }),
            ("contentHtml", { $0.contentHtml = nil }),
            ("facts", { $0.facts = nil }),
            ("facts.cleaningDateTimeUtc", { $0.facts?.cleaningDateTimeUtc = nil }),
            ("facts.estimatedMinutes", { $0.facts?.estimatedMinutes = nil }),
            ("facts.totalPrice", { $0.facts?.totalPrice = nil }),
            ("facts.rooms", { $0.facts?.rooms = nil }),
            ("facts.bathrooms", { $0.facts?.bathrooms = nil }),
            ("acceptance.acceptedOn", { $0.acceptance?.acceptedOn = nil }),
            ("acceptance.documentVersion", { $0.acceptance?.documentVersion = nil })
        ]
        for (field, break_) in cases {
            var payload = try fullPayload()
            break_(&payload)
            XCTAssertThrowsError(try WorkContract(payload), "\(field) was defaulted instead of refused") {
                XCTAssertEqual($0 as? WireContractViolation, WireContractViolation(field: field))
            }
        }
    }

    func testTheLabelsAndListsAreNotRefusals() throws {
        var payload = try fullPayload()
        payload.title = nil
        payload.language = nil
        payload.facts?.orderNumber = nil
        payload.facts?.currencyCode = nil
        payload.facts?.locationApproximate = nil
        payload.facts?.services = nil
        payload.facts?.packages = nil
        payload.facts?.extraSlugs = nil
        payload.acceptance = nil

        let contract = try WorkContract(payload)

        XCTAssertNil(contract.title)
        XCTAssertNil(contract.facts.orderNumber)
        XCTAssertTrue(contract.facts.services.isEmpty)
        XCTAssertTrue(contract.facts.extraSlugs.isEmpty)
        XCTAssertNil(contract.acceptance)
    }

    /// The detail's rows: a row with no id or no instant is dropped, a missing version reads as absent.
    func testAnAcceptanceRowOnTheDetailKeepsWhatTheLineReads() {
        let full = WorkContractAcceptanceDto(
            id: "acc-1",
            orderEmployeeId: "seat-1",
            employeeId: "emp-1",
            acceptedOn: Date(timeIntervalSince1970: 1_786_000_000),
            documentVersion: "2026-09-20",
            language: "cs"
        )
        XCTAssertEqual(WorkContractAcceptance(full)?.documentVersion, "2026-09-20")

        var noVersion = full
        noVersion.documentVersion = nil
        XCTAssertEqual(WorkContractAcceptance(noVersion)?.documentVersion, "")

        var noId = full
        noId.id = " "
        XCTAssertNil(WorkContractAcceptance(noId))

        var noSeat = full
        noSeat.orderEmployeeId = nil
        XCTAssertNil(WorkContractAcceptance(noSeat))

        var noInstant = full
        noInstant.acceptedOn = nil
        XCTAssertNil(WorkContractAcceptance(noInstant))
    }
}

private final class WorkContractWireRecorder: URLProtocol {
    private nonisolated(unsafe) static let lock = NSLock()
    private nonisolated(unsafe) static var recorded: [URLRequest] = []
    nonisolated(unsafe) static var answer: (status: Int, body: Data) = (200, Data())

    static func reset() {
        lock.withLock { recorded.removeAll() }
    }

    static func request(ofPath path: String) -> URLRequest? {
        lock.withLock { recorded.last { $0.url?.path == path } }
    }

    override static func canInit(with _: URLRequest) -> Bool {
        true
    }

    override static func canonicalRequest(for request: URLRequest) -> URLRequest {
        request
    }

    override func startLoading() {
        Self.lock.withLock { Self.recorded.append(request) }
        let answer = Self.answer
        guard let url = request.url,
              let response = HTTPURLResponse(
                  url: url,
                  statusCode: answer.status,
                  httpVersion: nil,
                  headerFields: ["Content-Type": "application/json"]
              )
        else {
            client?.urlProtocol(self, didFailWithError: URLError(.badServerResponse))
            return
        }
        client?.urlProtocol(self, didReceive: response, cacheStoragePolicy: .notAllowed)
        client?.urlProtocol(self, didLoad: answer.body)
        client?.urlProtocolDidFinishLoading(self)
    }

    override func stopLoading() {}
}

private struct WireDeviceId: DeviceIdProviding {
    var deviceId: String {
        "device-wire"
    }
}

private struct NeverRefreshing: AuthRefreshing {
    func refresh(refreshToken _: String) async -> RefreshCallResult {
        .retryable
    }
}

/// A session IS on the device, and the read is not on the anonymous allow-list, so the Bearer rides.
private final class SignedInTokenStore: TokenStore, @unchecked Sendable {
    func current() -> AuthTokens? {
        AuthTokens(
            accessToken: "access-1",
            accessTokenExpiresAt: Date(timeIntervalSinceNow: 900),
            refreshToken: "refresh-1",
            refreshTokenExpiresAt: Date(timeIntervalSinceNow: 9999)
        )
    }

    func save(_: AuthTokens) {}

    func clear() {}
}
