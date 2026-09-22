import CleansiaCore
import CleansiaPartnerApi
import XCTest
@testable import CleansiaPartner

/// The bytes the production client puts on the wire for the contract for work, and what it refuses to
/// read back. Every field on a generated command is optional with a nil default, so a take that
/// dropped the echo would still compile and still pass a view-model test against the fake — only the
/// recorded body can tell an omitted member from a carried one.
@MainActor
final class WorkContractWireTests: XCTestCase {
    private var bodies: WireBodies!
    private var answer: [String: (Int, Data)] = [:]

    private static let takePath = "/api/Order/TakeOrder"
    private static let acceptPath = "/api/Order/AcceptWorkContract"
    private static let previewPath = "/api/Order/GetWorkContractPreview"
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
        bodies = WireBodies()
        answer = [
            Self.takePath: (200, Data(#"{"orderId":"order-1"}"#.utf8)),
            Self.acceptPath: (200, Data(#"{"orderId":"order-1","acceptanceId":"acc-1"}"#.utf8)),
            Self.previewPath: (200, Self.contractJson),
            Self.readPath: (200, Self.contractJson)
        ]
        GeneratedWireSpine.install(recording: bodies) { [answer] request in
            answer[request.url?.path ?? ""] ?? (404, Data())
        }
        CodableHelper.jsonDecoder = ApiDateDecoding.decoder(primary: { CodableHelper.dateFormatter.date(from: $0) })
    }

    override func tearDown() {
        GenMockURLProtocol.handler = nil
        bodies = nil
        super.tearDown()
    }

    // MARK: the echo on the take

    func testTheTakeCarriesTheAcceptedTextIdBesideTheOrderId() async throws {
        _ = await LivePartnerOrderClient().takeOrder(orderId: "order-1", acceptedWorkContractTextId: "text-cs-1")

        let body = try XCTUnwrap(bodies.json(ofPath: Self.takePath))
        XCTAssertEqual(body["orderId"] as? String, "order-1")
        XCTAssertEqual(body["acceptedWorkContractTextId"] as? String, "text-cs-1")
        XCTAssertEqual(Set(body.keys), ["orderId", "acceptedWorkContractTextId"])
        XCTAssertEqual(bodies.method(ofPath: Self.takePath), "POST")
    }

    func testTheStandaloneAcceptanceCarriesTheSameEchoToItsOwnRoute() async throws {
        _ = await LivePartnerOrderClient()
            .acceptWorkContract(orderId: "order-1", acceptedWorkContractTextId: "text-cs-1")

        let body = try XCTUnwrap(bodies.json(ofPath: Self.acceptPath))
        XCTAssertEqual(body["orderId"] as? String, "order-1")
        XCTAssertEqual(body["acceptedWorkContractTextId"] as? String, "text-cs-1")
        XCTAssertEqual(bodies.method(ofPath: Self.acceptPath), "POST")
        XCTAssertFalse(bodies.paths.contains(Self.takePath), "an acceptance must not take a seat")
    }

    /// The generated command's member set is the wire contract the backend regenerated; a re-dump that
    /// renamed the echo would fail here rather than decode away to null on the server.
    func testTheGeneratedCommandsCarryExactlyTheTwoMembers() {
        XCTAssertEqual(
            Set(TakeOrderCommand.CodingKeys.allCases.map(\.rawValue)),
            ["orderId", "acceptedWorkContractTextId"]
        )
        XCTAssertEqual(
            Set(AcceptWorkContractCommand.CodingKeys.allCases.map(\.rawValue)),
            ["orderId", "acceptedWorkContractTextId"]
        )
    }

    // MARK: the reads

    func testThePreviewIsAGetKeyedOnTheOrderAndTheLanguage() async throws {
        let contract = try await LivePartnerOrderClient()
            .getWorkContractPreview(orderId: "order-1", language: "cs")
            .get()

        XCTAssertEqual(bodies.method(ofPath: Self.previewPath), "GET")
        XCTAssertEqual(contract.legalDocumentTextId, "text-cs-1")
        XCTAssertEqual(contract.facts.services, ["Standard cleaning"])
        XCTAssertEqual(contract.facts.extraSlugs, ["inside-oven"])
        XCTAssertEqual(contract.acceptance?.acceptedLanguage, "cs")
    }

    func testTheReadIsAGetKeyedOnTheAcceptance() async throws {
        let contract = try await LivePartnerOrderClient()
            .getWorkContract(acceptanceId: "acc-1", language: "en")
            .get()

        XCTAssertEqual(bodies.method(ofPath: Self.readPath), "GET")
        XCTAssertEqual(contract.version, "2026-09-20")
        XCTAssertEqual(contract.acceptance?.documentVersion, "2026-09-20")
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
    }

    func testEveryMemberTheAcceptanceBindsIsRefusedRatherThanDefaulted() throws {
        let cases: [(String, (inout WorkContractDto) -> Void)] = [
            ("legalDocumentTextId", { $0.legalDocumentTextId = nil }),
            ("legalDocumentTextId", { $0.legalDocumentTextId = " " }),
            ("version", { $0.version = nil }),
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
}
