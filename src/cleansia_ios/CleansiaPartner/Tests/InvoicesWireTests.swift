import CleansiaCore
import CleansiaPartnerApi
import XCTest
@testable import CleansiaPartner

/// The invoice list asks for the newest first in the one sort form the server binds. → /decisions/adr-0075
@MainActor
final class InvoicesWireTests: XCTestCase {
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

    func testTheInvoiceListSortTravelsInTheFormTheServerBinds() async throws {
        _ = try await LivePartnerPayrollClient().getPagedInvoices(employeeId: "emp-1").get()

        let query = try XCTUnwrap(bodies.query(ofPath: "/api/EmployeePayroll/GetPagedInvoices"), "no invoice request")
        XCTAssertTrue(query.contains(URLQueryItem(name: "Sort[0].Field", value: "createdOn")), "\(query)")
        XCTAssertTrue(query.contains(URLQueryItem(name: "Sort[0].Direction", value: "1")), "\(query)")
        XCTAssertFalse(query.contains { $0.name == "Sort" }, "a sort the server does not bind: \(query)")
    }
}
