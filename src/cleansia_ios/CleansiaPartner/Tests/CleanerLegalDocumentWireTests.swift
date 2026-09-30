import CleansiaCore
import CleansiaPartnerApi
import XCTest
@testable import CleansiaPartner

private enum LegalDocumentWire {
    static let documentsPath = "/api/Employee/GetMyLegalDocuments"
    static let acceptPath = "/api/Employee/AcceptLegalDocument"
    static let removalPath = "/api/Order/GetMyAssignmentRemoval"

    static let documentJson = """
    {
      "type": 3,
      "legalDocumentId": "doc-3",
      "legalDocumentTextId": "text-cs-3",
      "version": "2026-12-01",
      "effectiveFrom": "2026-12-01",
      "language": "cs",
      "title": "Rámcová smlouva",
      "contentHtml": "<p>Smlouva.</p>",
      "contentHash": "abc",
      "isAccepted": false,
      "acceptedVersion": "2026-10-01",
      "acceptedAt": "2026-10-02T08:30:00Z"
    }
    """

    static let removalJson = """
    { "orderId": "order-1", "reason": "Customer request.", "removedOn": "2026-10-02T08:30:00Z" }
    """

    /// Answers only the query the client is meant to send, so a dropped parameter fails the read.
    static func answer(_ request: URLRequest) -> (Int, Data) {
        let query = request.url.flatMap { URLComponents(url: $0, resolvingAgainstBaseURL: false) }?.queryItems ?? []
        switch request.url?.path ?? "" {
        case documentsPath:
            guard query.contains(URLQueryItem(name: "Language", value: "cs")) else { return (400, Data()) }
            return (200, Data("[\(documentJson)]".utf8))
        case acceptPath:
            return (200, Data(#"{"type":3,"version":"2026-12-01"}"#.utf8))
        case removalPath:
            guard query.contains(URLQueryItem(name: "OrderId", value: "order-1")) else { return (400, Data()) }
            return (200, Data(removalJson.utf8))
        default:
            return (404, Data())
        }
    }
}

/// The contract documents and the removal reason as the production clients read and write them: the
/// language the documents are asked in, the text id an acceptance echoes, and what the mapper refuses to
/// read back.
@MainActor
final class CleanerLegalDocumentWireTests: XCTestCase {
    private var bodies: WireBodies!

    override func setUp() {
        super.setUp()
        bodies = WireBodies()
        GeneratedWireSpine.install(recording: bodies) { LegalDocumentWire.answer($0) }
        CodableHelper.jsonDecoder = ApiDateDecoding.decoder(primary: { CodableHelper.dateFormatter.date(from: $0) })
    }

    override func tearDown() {
        GenMockURLProtocol.handler = nil
        bodies = nil
        super.tearDown()
    }

    func testTheDocumentsAreAGetInTheReadersLanguage() async throws {
        let documents = try await LivePartnerProfileClient().getLegalDocuments(language: "cs").get()

        XCTAssertEqual(bodies.method(ofPath: LegalDocumentWire.documentsPath), "GET")
        XCTAssertEqual(documents.map(\.legalDocumentTextId), ["text-cs-3"])
        XCTAssertEqual(documents.first?.type, ._3)
        XCTAssertEqual(documents.first?.isAccepted, false)
        XCTAssertEqual(documents.first?.acceptedVersion, "2026-10-01")
        XCTAssertNotNil(documents.first?.acceptedAt)
    }

    func testTheAcceptanceEchoesTheTextIdItWasShownAndNothingElse() async throws {
        _ = try await LivePartnerProfileClient().acceptLegalDocument(legalDocumentTextId: "text-cs-3").get()

        let body = try XCTUnwrap(bodies.json(ofPath: LegalDocumentWire.acceptPath))
        XCTAssertEqual(body["acceptedTextId"] as? String, "text-cs-3")
        XCTAssertEqual(Set(body.keys), ["acceptedTextId"])
        XCTAssertEqual(bodies.method(ofPath: LegalDocumentWire.acceptPath), "POST")
    }

    func testTheRemovalReasonIsAGetKeyedOnTheOrder() async throws {
        let reason = try await LivePartnerOrderClient().getMyAssignmentRemovalReason(orderId: "order-1").get()

        XCTAssertEqual(bodies.method(ofPath: LegalDocumentWire.removalPath), "GET")
        XCTAssertEqual(reason, "Customer request.")
    }

    // MARK: the mapper refuses rather than defaults

    private func fullPayload() throws -> CleanerLegalDocumentDto {
        try CodableHelper.jsonDecoder.decode(
            CleanerLegalDocumentDto.self,
            from: Data(LegalDocumentWire.documentJson.utf8)
        )
    }

    func testEveryMemberTheAcceptanceHangsOnIsRefusedRatherThanDefaulted() throws {
        let cases: [(String, (inout CleanerLegalDocumentDto) -> Void)] = [
            ("type", { $0.type = nil }),
            ("legalDocumentTextId", { $0.legalDocumentTextId = nil }),
            ("legalDocumentTextId", { $0.legalDocumentTextId = " " }),
            ("version", { $0.version = nil }),
            ("title", { $0.title = nil }),
            ("contentHtml", { $0.contentHtml = nil }),
            ("isAccepted", { $0.isAccepted = nil })
        ]
        for (field, break_) in cases {
            var payload = try fullPayload()
            break_(&payload)
            XCTAssertThrowsError(try CleanerLegalDocument(payload), "\(field) was defaulted instead of refused") {
                XCTAssertEqual($0 as? WireContractViolation, WireContractViolation(field: field))
            }
        }
    }

    func testANeverAcceptedDocumentIsNotARefusal() throws {
        var payload = try fullPayload()
        payload.acceptedVersion = nil
        payload.acceptedAt = nil

        let document = try CleanerLegalDocument(payload)

        XCTAssertNil(document.acceptedVersion)
        XCTAssertNil(document.acceptedAt)
    }

    func testTheGeneratedCommandCarriesExactlyTheEcho() {
        XCTAssertEqual(Set(AcceptLegalDocumentCommand.CodingKeys.allCases.map(\.rawValue)), ["acceptedTextId"])
    }
}
