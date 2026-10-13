import CleansiaCore
import CleansiaPartnerApi
import XCTest
@testable import CleansiaPartner

/// The dashboard's upcoming page starts at the beginning of the cleaner's own day, so today's earlier jobs stay on it
/// for the today count. Without a from-date the page could be filled with past assignments, leaving no next job.
/// The day is Kiritimati's, 14 hours ahead of UTC all year, so a UTC midnight cannot pass for it on a UTC runner.
@MainActor
final class UpcomingOrdersWireTests: XCTestCase {
    override func tearDown() {
        GenMockURLProtocol.handler = nil
        super.tearDown()
    }

    func testTheUpcomingPageStartsAtTheBeginningOfTheLocalDay() async throws {
        var calendar = Calendar(identifier: .gregorian)
        calendar.timeZone = try XCTUnwrap(TimeZone(identifier: "Pacific/Kiritimati"))
        let savedZone = NSTimeZone.default
        NSTimeZone.default = calendar.timeZone
        defer { NSTimeZone.default = savedZone }
        let lock = NSLock()
        var sent: [URLQueryItem]?
        GeneratedWireSpine.install(recording: WireBodies()) { request in
            if request.url?.path == "/api/Dashboard/GetUpcomingOrders" {
                let query = request.url.flatMap { URLComponents(url: $0, resolvingAgainstBaseURL: false) }?.queryItems
                lock.withLock { sent = query ?? [] }
            }
            return (200, Data(#"{"data":[],"total":0}"#.utf8))
        }

        let before = calendar.startOfDay(for: Date())
        _ = try await LivePartnerDashboardClient().getUpcomingOrders(employeeId: "emp-1", limit: 10).get()
        let after = calendar.startOfDay(for: Date())

        let query = try XCTUnwrap(lock.withLock { sent }, "no upcoming request was sent")
        let from = try XCTUnwrap(query.first { $0.name == "Filter.CleaningDateFrom" }?.value, "\(query)")
        let day = try XCTUnwrap(CodableHelper.dateFormatter.date(from: from), from)
        XCTAssertTrue(day == before || day == after, "\(from) is not the start of the local day")
        XCTAssertTrue(query.contains(URLQueryItem(name: "Filter.EmployeeId", value: "emp-1")), "\(query)")
    }

    /// The server binds a sort only as `Sort[0].Field` and `Sort[0].Direction`; any other form is dropped and
    /// the page comes back newest-created first. → /decisions/adr-0075
    func testTheUpcomingPageAsksForTheSoonestCleaningFirst() async throws {
        let bodies = WireBodies()
        GeneratedWireSpine.install(recording: bodies) { _ in (200, Data(#"{"data":[],"total":0}"#.utf8)) }

        _ = try await LivePartnerDashboardClient().getUpcomingOrders(employeeId: "emp-1", limit: 10).get()

        let query = try XCTUnwrap(bodies.query(ofPath: "/api/Dashboard/GetUpcomingOrders"), "no upcoming request")
        XCTAssertTrue(query.contains(URLQueryItem(name: "Sort[0].Field", value: "cleaningDateTime")), "\(query)")
        XCTAssertTrue(query.contains(URLQueryItem(name: "Sort[0].Direction", value: "0")), "\(query)")
        XCTAssertFalse(query.contains { $0.name == "Sort" }, "a sort the server does not bind: \(query)")
    }
}
