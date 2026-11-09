import CleansiaCore
import CleansiaPartnerApi
import XCTest
@testable import CleansiaPartner

final class OrdersListLogicTests: XCTestCase {
    private var calendar: Calendar = {
        var calendar = Calendar(identifier: .gregorian)
        calendar.timeZone = TimeZone(identifier: "UTC") ?? .current
        calendar.firstWeekday = 2 // Monday
        return calendar
    }()

    // MARK: tab → query (+ O3)

    func testAvailableQueryRequiresAFreeSeatExcludesOwnEmployeeAndSortsBySoonest() {
        let query = OrdersQueryBuilder.query(tab: .available, ownEmployeeId: "emp-self", period: .thisMonth)
        XCTAssertEqual(query.statuses, [._0, ._2, ._3, ._4])
        XCTAssertNil(query.isUnassigned)
        XCTAssertEqual(query.hasAvailableSpots, true)
        XCTAssertEqual(query.excludeEmployeeId, "emp-self")
        XCTAssertNil(query.employeeId)
        XCTAssertEqual(query.sortField, "cleaningDateTime")
        XCTAssertTrue(query.sortAscending)
        XCTAssertEqual(query.limit, 100)
        XCTAssertNil(query.id)
    }

    func testActiveQueryScopesToOwnEmployeeSortDateAsc() {
        let query = OrdersQueryBuilder.query(tab: .active, ownEmployeeId: "emp-self", period: .thisMonth)
        XCTAssertEqual(query.statuses, [._2, ._3, ._4])
        XCTAssertNil(query.isUnassigned)
        XCTAssertEqual(query.employeeId, "emp-self")
        XCTAssertNil(query.hasAvailableSpots)
        XCTAssertNil(query.excludeEmployeeId)
        XCTAssertEqual(query.sortField, "cleaningDateTime")
        XCTAssertTrue(query.sortAscending)
    }

    func testHistoryQueryScopesToOwnEmployeeSortDateDesc() {
        let query = OrdersQueryBuilder.query(tab: .history, ownEmployeeId: "emp-self", period: .thisMonth)
        XCTAssertEqual(query.statuses, [._5])
        XCTAssertEqual(query.employeeId, "emp-self")
        XCTAssertNil(query.hasAvailableSpots)
        XCTAssertNil(query.excludeEmployeeId)
        XCTAssertFalse(query.sortAscending)
    }

    /// D13 and D11: the taken row and an off-page reservation are each one row by id, scoped as their pane is.
    func testTheTakenRowAndAReservedRowAreReadByIdInTheirPanesScope() {
        let taken = OrdersQueryBuilder.takenRow(orderId: "o1", ownEmployeeId: "emp-self")
        XCTAssertEqual(taken.id, "o1")
        XCTAssertEqual(taken.employeeId, "emp-self")
        XCTAssertEqual(taken.statuses, [._2, ._3, ._4])
        XCTAssertEqual(taken.limit, 1)
        let reserved = OrdersQueryBuilder.reservedRow(orderId: "o2", ownEmployeeId: "emp-self")
        XCTAssertEqual(reserved.id, "o2")
        XCTAssertEqual(reserved.excludeEmployeeId, "emp-self")
        XCTAssertEqual(reserved.hasAvailableSpots, true)
        XCTAssertEqual(reserved.limit, 1)
    }

    // MARK: periodToDateRange (half-open)

    func testThisMonthIsHalfOpenMonthRange() {
        let now = makeDate(2026, 3, 17)
        let range = OrdersQueryBuilder.periodToDateRange(.thisMonth, calendar: calendar, now: now)
        XCTAssertEqual(range.from, makeDate(2026, 3, 1))
        XCTAssertEqual(range.to, makeDate(2026, 4, 1))
    }

    func testLastMonthIsHalfOpenPreviousMonthRange() {
        let now = makeDate(2026, 3, 17)
        let range = OrdersQueryBuilder.periodToDateRange(.lastMonth, calendar: calendar, now: now)
        XCTAssertEqual(range.from, makeDate(2026, 2, 1))
        XCTAssertEqual(range.to, makeDate(2026, 3, 1))
    }

    /// The week starts on Monday whatever the phone's calendar says: an en-US phone starts its weeks on Sunday.
    func testThisWeekIsSevenDayHalfOpenRangeFromMondayOnASundayFirstPhone() {
        var sundayFirst = Calendar(identifier: .gregorian)
        sundayFirst.timeZone = TimeZone(identifier: "UTC") ?? .current
        sundayFirst.locale = Locale(identifier: "en_US")
        sundayFirst.firstWeekday = 1
        for now in [makeDate(2026, 3, 18), makeDate(2026, 3, 22)] {
            let range = OrdersQueryBuilder.periodToDateRange(.thisWeek, calendar: sundayFirst, now: now)
            XCTAssertEqual(range.from, makeDate(2026, 3, 16), "Monday, for \(now)")
            XCTAssertEqual(range.to, makeDate(2026, 3, 23), "the next Monday, for \(now)")
        }
        let monday = OrdersQueryBuilder.periodToDateRange(.thisWeek, calendar: calendar, now: makeDate(2026, 3, 16))
        XCTAssertEqual(monday.from, makeDate(2026, 3, 16))
    }

    /// D10: "All time" is gone; the months are named, standalone and capitalised in the locale.
    func testThePeriodsAreThisWeekAndTheTwoNamedMonths() {
        XCTAssertEqual(CompletedPeriod.allCases, [.thisWeek, .thisMonth, .lastMonth])
        let now = makeDate(2026, 10, 31, hour: 12)
        let english = Locale(identifier: "en")
        XCTAssertEqual(OrdersQueryBuilder.periodLabel(.thisWeek, locale: english, now: now), L10n.Orders.periodThisWeek)
        XCTAssertEqual(
            OrdersQueryBuilder.periodLabel(.thisMonth, locale: english, calendar: calendar, now: now),
            "October"
        )
        XCTAssertEqual(
            OrdersQueryBuilder.periodLabel(.lastMonth, locale: Locale(identifier: "ru"), calendar: calendar, now: now),
            "Сентябрь"
        )
    }

    // MARK: the Available pane's pay column

    /// The reserved block reports every tile or, scrolled off, none; the column narrows when its widest job leaves.
    func testThePayColumnKeepsItsWidthOverAnEmptyReportAndNarrowsWhenTheWidestLeaves() {
        var column: CGFloat = 108
        var columns: [CGFloat] = []
        for need: CGFloat in [123.4, 0, 124, 114.2, 90] {
            column = OrdersTileNeedKey.column(column, after: need)
            columns.append(column)
        }
        XCTAssertEqual(columns, [124, 124, 124, 115, 108])
    }

    // MARK: helpers

    private func makeDate(_ year: Int, _ month: Int, _ day: Int, hour: Int = 0) -> Date {
        var components = DateComponents()
        components.year = year
        components.month = month
        components.day = day
        components.hour = hour
        components.timeZone = TimeZone(identifier: "UTC")
        return calendar.date(from: components) ?? Date()
    }
}
