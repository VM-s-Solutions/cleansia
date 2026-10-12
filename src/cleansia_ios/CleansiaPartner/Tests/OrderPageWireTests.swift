import CleansiaCore
import CleansiaPartnerApi
import XCTest
@testable import CleansiaPartner

/// The Orders list's sort choice reaches the server only as `Sort[0].Field` and `Sort[0].Direction`, the one form
/// it binds. → /decisions/adr-0075
@MainActor
final class OrderPageWireTests: XCTestCase {
    private var bodies: WireBodies!

    override func setUp() {
        super.setUp()
        bodies = WireBodies()
        GeneratedWireSpine.install(recording: bodies) { _ in (200, Data(#"{"data":[],"total":0}"#.utf8)) }
    }

    override func tearDown() {
        GenMockURLProtocol.handler = nil
        bodies = nil
        super.tearDown()
    }

    func testTheChosenFieldAndDirectionTravelInTheFormTheServerBinds() async throws {
        for (field, ascending, direction) in [("cleaningDateTime", true, "0"), ("totalPrice", false, "1")] {
            let page = OrderPageQuery(statuses: [._2], sortField: field, sortAscending: ascending)
            _ = try await LivePartnerOrderClient().getPaged(page).get()

            let query = try XCTUnwrap(bodies.query(ofPath: "/api/Order/GetPaged"), "no order page request")
            XCTAssertTrue(query.contains(URLQueryItem(name: "Sort[0].Field", value: field)), "\(query)")
            XCTAssertTrue(query.contains(URLQueryItem(name: "Sort[0].Direction", value: direction)), "\(query)")
            XCTAssertFalse(query.contains { $0.name == "Sort" }, "a sort the server does not bind: \(query)")
        }
    }
}
