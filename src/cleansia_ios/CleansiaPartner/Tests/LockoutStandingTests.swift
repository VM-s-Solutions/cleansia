import CleansiaCore
import CleansiaPartnerApi
import XCTest
@testable import CleansiaPartner

/// `ReportOrderLockout` answers only the crew of an order that is Confirmed, on the way or in progress,
/// and refuses it before the booked start plus `BookingPolicy.LockoutWaitMinutes`.
final class LockoutStandingTests: XCTestCase {
    private static let start = Date(timeIntervalSince1970: 1_790_676_000)
    private static let calls = "Called at 10:05 and 10:15, no answer"

    private var late: Date {
        Self.start.addingTimeInterval(3600)
    }

    private func order(
        status: Int = 4,
        mine: Bool = true,
        startsAt: Date? = LockoutStandingTests.start,
        reportedAt: Date? = nil,
        callAttempts: String? = nil
    ) throws -> OrderDetail {
        var item = OrderItem.wireComplete()
        item.orderStatus = Code(value: status)
        item.isAssignedToCurrentUser = mine
        item.cleaningDateTime = startsAt
        item.lockoutReportedAt = reportedAt
        item.lockoutCallAttempts = callAttempts
        return try OrderDetail(item)
    }

    func testTheReportOpensTheWaitAfterTheBookedStartAndNotAMomentSooner() throws {
        let opensAt = Self.start.addingTimeInterval(TimeInterval(LockoutStanding.waitMinutes * 60))

        XCTAssertEqual(try order().lockoutStanding(now: opensAt.addingTimeInterval(-1)), .notYet(opensAt: opensAt))
        XCTAssertEqual(try order().lockoutStanding(now: opensAt), .open)
    }

    func testACleanerWaitingAtTheDoorHasTheWallClockReReadEveryThirtySecondsNotOnceAtTheEnd() {
        let opensAt = Self.start.addingTimeInterval(TimeInterval(LockoutStanding.waitMinutes * 60))
        let step = { (now: Date) in LockoutStanding.clockStep(now: now, opensAt: opensAt) }

        XCTAssertEqual(step(Self.start), 30, accuracy: 0.001)
        XCTAssertEqual(step(opensAt.addingTimeInterval(-5)), 5, accuracy: 0.001)
        XCTAssertEqual(step(opensAt.addingTimeInterval(300)), 0, accuracy: 0.001)
    }

    func testTheWaitIsTheOneTheServerHoldsTheCleanerTo() throws {
        XCTAssertEqual(LockoutStanding.waitMinutes, try Self.serverWaitMinutes())
    }

    func testACrewMemberMayReportFromConfirmedThroughInProgress() throws {
        let open = try (0 ... 6).filter { try order(status: $0).lockoutStanding(now: late) == .open }

        XCTAssertEqual(open, [2, 3, 4])
    }

    func testACleanerWhoIsNotOnTheCrewIsOfferedNothing() throws {
        XCTAssertEqual(try order(mine: false).lockoutStanding(now: late), .hidden)
    }

    func testAJobWithNoStartIsOfferedNothingRatherThanAnUngatedReport() throws {
        XCTAssertEqual(try order(startsAt: nil).lockoutStanding(now: late), .hidden)
    }

    func testAReportAlreadyMadeIsShownAsMadeWithTheCallsItNames() throws {
        let reportedAt = Self.start.addingTimeInterval(20 * 60)

        let standing = try order(status: 3, reportedAt: reportedAt, callAttempts: Self.calls).lockoutStanding(now: late)

        XCTAssertEqual(standing, .reported(reportedAt: reportedAt, callAttempts: Self.calls))
    }

    func testAReportOnAJobThatHasSinceClosedIsNotOfferedAgain() throws {
        let reported = try order(status: 6, reportedAt: Self.start.addingTimeInterval(20 * 60))

        XCTAssertEqual(reported.lockoutStanding(now: late), .hidden)
    }

    func testTheTooEarlyRefusalStatesTheWaitInAllFiveLocales() {
        defer { CoreL10n.apply(languageTag: "en") }
        let refusal = ApiError(code: "order.lockout.too_early", message: "raw server text", httpStatus: 400)
        for locale in ["en", "cs", "sk", "uk", "ru"] {
            CoreL10n.apply(languageTag: locale)
            let message = ApiErrorLocalizer().message(for: refusal)
            XCTAssertTrue(
                message.contains("\(LockoutStanding.waitMinutes)"),
                "\(locale) does not state the wait: \(message)"
            )
        }
    }

    private static func serverWaitMinutes() throws -> Int {
        let policy = URL(fileURLWithPath: #filePath)
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .appendingPathComponent("src/Cleansia.Core.AppServices/Features/Orders/BookingPolicy.cs")
        let source = try String(contentsOf: policy, encoding: .utf8)
        let regex = try NSRegularExpression(pattern: #"public\s+const\s+int\s+LockoutWaitMinutes\s*=\s*(\d+)\s*;"#)
        let match = try XCTUnwrap(
            regex.firstMatch(in: source, range: NSRange(source.startIndex..., in: source)),
            "BookingPolicy.LockoutWaitMinutes not found — the parser needs updating"
        )
        let digits = try XCTUnwrap(Range(match.range(at: 1), in: source))
        return try XCTUnwrap(Int(source[digits]))
    }
}
