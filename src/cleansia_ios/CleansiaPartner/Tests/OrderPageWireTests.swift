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
            _ = try await LivePartnerOrderClient(staleness: OrdersStaleness()).getPaged(page).get()

            let query = try XCTUnwrap(bodies.query(ofPath: "/api/Order/GetPaged"), "no order page request")
            XCTAssertTrue(query.contains(URLQueryItem(name: "Sort[0].Field", value: field)), "\(query)")
            XCTAssertTrue(query.contains(URLQueryItem(name: "Sort[0].Direction", value: direction)), "\(query)")
            XCTAssertFalse(query.contains { $0.name == "Sort" }, "a sort the server does not bind: \(query)")
        }
    }

    func testAvailableForwardsFreeSeatAndCurrentEmployeeExclusion() async throws {
        GeneratedWireSpine.install(recording: bodies) { request in
            if request.url?.path == "/api/Employee/GetCurrentEmployee" {
                return (200, Data(#"{"id":"emp-self"}"#.utf8))
            }
            return (200, Data(#"{"data":[],"total":0}"#.utf8))
        }
        let client = LivePartnerOrderClient(staleness: OrdersStaleness())
        let vm = OrdersListViewModel(client: client, staleness: OrdersStaleness(), snackbar: SnackbarController())
        await vm.onAppear()
        let query = try XCTUnwrap(bodies.query(ofPath: "/api/Order/GetPaged"))
        XCTAssertTrue(bodies.paths.contains("/api/Employee/GetCurrentEmployee"))
        XCTAssertTrue(query.contains(URLQueryItem(name: "Filter.HasAvailableSpots", value: "true")))
        XCTAssertTrue(query.contains(URLQueryItem(name: "Filter.ExcludeEmployeeId", value: "emp-self")))
        XCTAssertFalse(query.contains { ["Filter.IsUnassigned", "Filter.EmployeeId"].contains($0.name) })
        XCTAssertEqual(query.filter { $0.name == "Filter.OrderStatuses" }.map(\.value), ["0", "2", "3", "4"])
        XCTAssertTrue(query.contains(URLQueryItem(name: "Sort[0].Field", value: "estimatedCleanerPay")))
        XCTAssertTrue(query.contains(URLQueryItem(name: "Sort[0].Direction", value: "1")))
        XCTAssertTrue(query.contains(URLQueryItem(name: "Offset", value: "0")))
        XCTAssertTrue(query.contains(URLQueryItem(name: "Limit", value: "50")))
    }

    func testAvailableRefusesMissingNullAndBlankCurrentEmployeeWireIdentity() async {
        for body in [#"{}"#, #"{"id":null}"#, #"{"id":""}"#, #"{"id":" \t"}"#] {
            let recorded = WireBodies()
            GeneratedWireSpine.install(recording: recorded) { request in
                if request.url?.path == "/api/Employee/GetCurrentEmployee" { return (200, Data(body.utf8)) }
                return (200, Data(#"{"data":[],"total":0}"#.utf8))
            }
            let client = LivePartnerOrderClient(staleness: OrdersStaleness())
            let vm = OrdersListViewModel(client: client, staleness: OrdersStaleness(), snackbar: SnackbarController())
            await vm.onAppear()
            XCTAssertEqual(recorded.paths, ["/api/Employee/GetCurrentEmployee"], "wire body: \(body)")
            guard case .error = vm.currentState else { return XCTFail("expected identity refusal: \(body)") }
        }
    }

    func testActiveAndHistoryRetainTheirOwnEmployeeAndExcludeAvailableFilters() async throws {
        let calendar = Calendar(identifier: .gregorian)
        for tab in [OrdersTab.active, .history] {
            let page = OrdersQueryBuilder.query(
                tab: tab, ownEmployeeId: "emp-self", sort: .priceHighToLow, period: .thisMonth,
                calendar: calendar, now: Date(timeIntervalSince1970: 1_780_000_000)
            )
            _ = try await LivePartnerOrderClient(staleness: OrdersStaleness()).getPaged(page).get()
            let query = try XCTUnwrap(bodies.query(ofPath: "/api/Order/GetPaged"))
            XCTAssertTrue(query.contains(URLQueryItem(name: "Filter.EmployeeId", value: "emp-self")))
            XCTAssertFalse(query.contains { [
                "Filter.HasAvailableSpots",
                "Filter.ExcludeEmployeeId",
                "Filter.IsUnassigned"
            ].contains($0.name) })
            XCTAssertEqual(
                query.filter { $0.name == "Filter.OrderStatuses" }.map(\.value),
                tab == .active ? ["2", "3", "4"] : ["5"]
            )
            XCTAssertTrue(query.contains(URLQueryItem(name: "Sort[0].Field", value: "cleaningDateTime")))
            XCTAssertTrue(query.contains(URLQueryItem(name: "Sort[0].Direction", value: tab == .active ? "0" : "1")))
            XCTAssertTrue(query.contains(URLQueryItem(name: "Offset", value: "0")))
            XCTAssertTrue(query.contains(URLQueryItem(name: "Limit", value: "50")))
            XCTAssertEqual(query.contains { $0.name == "Filter.CleaningDateFrom" }, tab == .history)
            XCTAssertEqual(query.contains { $0.name == "Filter.CleaningDateTo" }, tab == .history)
        }
    }
}
