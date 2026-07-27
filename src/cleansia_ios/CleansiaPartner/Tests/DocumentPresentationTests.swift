import CleansiaPartnerApi
import XCTest
@testable import CleansiaPartner

final class DocumentPresentationTests: XCTestCase {
    /// The picker is spelled out rather than derived from `allCases`, so a
    /// regenerated client that gains an eleventh `DocumentType` would silently
    /// render "—" for it and never offer it on upload. This is the tripwire.
    func testEveryDocumentTypeHasARow() {
        XCTAssertEqual(DocumentPresentation.types.count, 10)
        XCTAssertEqual(DocumentPresentation.types.map(\.type), DocumentType.allCases)
    }

    func testUnknownTypeAndNilStatusRenderEmDash() {
        XCTAssertEqual(DocumentPresentation.typeLabel(nil), "—")
        XCTAssertEqual(DocumentPresentation.statusLabel(nil), "—")
    }

    func testEveryTypeAndStatusResolvesToATranslatedLabel() {
        for option in DocumentPresentation.types {
            let label = DocumentPresentation.typeLabel(option.type)
            XCTAssertFalse(label.isEmpty)
            XCTAssertNotEqual(label, "—", "type \(option.type) has no label")
        }
        for status in DocumentStatus.allCases {
            XCTAssertNotEqual(DocumentPresentation.statusLabel(status), "—")
        }
    }

    /// The dropdown speaks `String` ids; a mismatch here would make the confirm
    /// button enable and then quietly do nothing.
    func testOptionIdRoundTripsToTheWireValue() {
        for option in DocumentPresentation.types {
            let id = DocumentPresentation.optionId(option.type)
            XCTAssertEqual(id, String(option.type.rawValue))
            XCTAssertEqual(DocumentPresentation.type(forOptionId: id), option.type)
        }
    }

    func testTypeForOptionIdRejectsNilAndUnknownIds() {
        XCTAssertNil(DocumentPresentation.type(forOptionId: nil))
        XCTAssertNil(DocumentPresentation.type(forOptionId: ""))
        XCTAssertNil(DocumentPresentation.type(forOptionId: "11"))
    }

    func testMaxDocumentBytesMatchesTheTenMegabyteCopy() {
        XCTAssertEqual(DocumentPresentation.maxDocumentBytes, 10 * 1024 * 1024)
    }
}
