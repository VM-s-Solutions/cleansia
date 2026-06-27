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

    func testAvailableQueryIsUnassignedNoEmployeeSortEarningsDesc() {
        let query = OrdersQueryBuilder.query(
            tab: .available, ownEmployeeId: "emp-self", sort: .earningsHighToLow, period: .all
        )
        XCTAssertEqual(query.statuses, [._0, ._2])
        XCTAssertEqual(query.isUnassigned, true)
        XCTAssertNil(query.employeeId)
        XCTAssertEqual(query.sortField, "estimatedCleanerPay")
        XCTAssertFalse(query.sortAscending)
    }

    func testActiveQueryScopesToOwnEmployeeSortDateAsc() {
        let query = OrdersQueryBuilder.query(
            tab: .active, ownEmployeeId: "emp-self", sort: .earningsHighToLow, period: .all
        )
        XCTAssertEqual(query.statuses, [._2, ._3, ._4])
        XCTAssertNil(query.isUnassigned)
        XCTAssertEqual(query.employeeId, "emp-self")
        XCTAssertEqual(query.sortField, "cleaningDateTime")
        XCTAssertTrue(query.sortAscending)
    }

    func testHistoryQueryScopesToOwnEmployeeSortDateDesc() {
        let query = OrdersQueryBuilder.query(
            tab: .history, ownEmployeeId: "emp-self", sort: .earningsHighToLow, period: .all
        )
        XCTAssertEqual(query.statuses, [._5])
        XCTAssertEqual(query.employeeId, "emp-self")
        XCTAssertFalse(query.sortAscending)
    }

    // MARK: sort options

    func testSortFieldsAndDirections() {
        XCTAssertEqual(AvailableSort.earningsHighToLow.field, "estimatedCleanerPay")
        XCTAssertFalse(AvailableSort.earningsHighToLow.ascending)
        XCTAssertEqual(AvailableSort.soonestFirst.field, "cleaningDateTime")
        XCTAssertTrue(AvailableSort.soonestFirst.ascending)
        XCTAssertEqual(AvailableSort.priceHighToLow.field, "totalPrice")
        XCTAssertFalse(AvailableSort.priceHighToLow.ascending)
    }

    // MARK: matchesSearch (pure)

    func testMatchesSearchEmptyMatchesAll() {
        let order = OrderListItem.sample(id: "o1", customerName: "Jana")
        XCTAssertTrue(order.matchesSearch(""))
        XCTAssertTrue(order.matchesSearch("   "))
    }

    func testMatchesSearchByNameAddressOrNumberCaseInsensitive() {
        let order = OrderListItem.sample(
            id: "o1", customerName: "Jana Nováková", customerAddress: "Vinohradská 12",
            displayOrderNumber: "ORD-2025"
        )
        XCTAssertTrue(order.matchesSearch("jana"))
        XCTAssertTrue(order.matchesSearch("vinohrad"))
        XCTAssertTrue(order.matchesSearch("ord-2025"))
        XCTAssertFalse(order.matchesSearch("zzz"))
    }

    // MARK: periodToDateRange (half-open)

    func testPeriodAllIsUnbounded() {
        let range = OrdersQueryBuilder.periodToDateRange(.all, calendar: calendar)
        XCTAssertNil(range.from)
        XCTAssertNil(range.to)
    }

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

    func testThisWeekIsSevenDayHalfOpenRangeFromMonday() {
        let now = makeDate(2026, 3, 18) // a Wednesday
        let range = OrdersQueryBuilder.periodToDateRange(.thisWeek, calendar: calendar, now: now)
        XCTAssertEqual(range.from, makeDate(2026, 3, 16)) // Monday
        XCTAssertEqual(range.to, makeDate(2026, 3, 23)) // next Monday
    }

    // MARK: day bucket (pure)

    func testDayBucketTodayTomorrowLater() {
        let now = makeDate(2026, 3, 18, hour: 10)
        XCTAssertEqual(ActiveDayBucket.of(makeDate(2026, 3, 18, hour: 14), calendar: calendar, now: now), .today)
        XCTAssertEqual(ActiveDayBucket.of(makeDate(2026, 3, 19, hour: 9), calendar: calendar, now: now), .tomorrow)
        XCTAssertEqual(ActiveDayBucket.of(makeDate(2026, 3, 25), calendar: calendar, now: now), .later)
        XCTAssertEqual(ActiveDayBucket.of(nil, calendar: calendar, now: now), .later)
    }

    // MARK: grouping (pure)

    func testActiveGroupingOrdersBucketsAndDropsEmpties() {
        let now = makeDate(2026, 3, 18, hour: 8)
        let orders = [
            OrderListItem.sample(id: "later", cleaningDateTime: makeDate(2026, 3, 25)),
            OrderListItem.sample(id: "today", cleaningDateTime: makeDate(2026, 3, 18, hour: 14))
        ]
        let grouped = OrdersGrouping.byDayBucket(orders, calendar: calendar, now: now)
        XCTAssertEqual(grouped.map(\.0), [.today, .later])
    }

    func testHistoryGroupingMostRecentFirstNilLast() {
        let orders = [
            OrderListItem.sample(id: "old", cleaningDateTime: makeDate(2026, 3, 10)),
            OrderListItem.sample(id: "new", cleaningDateTime: makeDate(2026, 3, 20)),
            OrderListItem.sample(id: "unscheduled", cleaningDateTime: nil)
        ]
        let grouped = OrdersGrouping.byDay(orders, calendar: calendar)
        XCTAssertEqual(grouped.first?.0, makeDate(2026, 3, 20))
        XCTAssertNil(grouped.last?.0)
    }

    // MARK: distance (pure)

    func testDistanceKmNilWhenNoLocationOrNoCoordinate() {
        let order = OrderListItem.sample(id: "o1")
        XCTAssertNil(order.distanceKm(from: Coordinate(latitude: 50, longitude: 14)))
        let located = OrderListItem.sample(id: "o2", latitude: 50.1, longitude: 14.5)
        XCTAssertNil(located.distanceKm(from: nil))
    }

    func testDistanceKmComputesGreatCircle() {
        let order = OrderListItem.sample(id: "o1", latitude: 50.0755, longitude: 14.4378)
        let here = Coordinate(latitude: 50.0875, longitude: 14.4213) // ~1.7km
        let distance = order.distanceKm(from: here)
        XCTAssertNotNil(distance)
        XCTAssertEqual(distance ?? 0, 1.7, accuracy: 0.5)
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
