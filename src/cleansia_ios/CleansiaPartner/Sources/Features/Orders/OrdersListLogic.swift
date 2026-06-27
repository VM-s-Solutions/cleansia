import CleansiaCore
import CleansiaPartnerApi
import Foundation

enum OrdersTab: CaseIterable {
    case available
    case active
    case history

    var pane: OrdersPane {
        switch self {
        case .available: .available
        case .active: .active
        case .history: .history
        }
    }
}

enum AvailableSort: CaseIterable {
    case earningsHighToLow
    case soonestFirst
    case priceHighToLow

    var field: String {
        switch self {
        case .earningsHighToLow: "estimatedCleanerPay"
        case .soonestFirst: "cleaningDateTime"
        case .priceHighToLow: "totalPrice"
        }
    }

    var ascending: Bool {
        switch self {
        case .earningsHighToLow, .priceHighToLow: false
        case .soonestFirst: true
        }
    }
}

enum CompletedPeriod: CaseIterable {
    case thisWeek
    case thisMonth
    case lastMonth
    case all
}

enum ActiveDayBucket: Int, CaseIterable {
    case today
    case tomorrow
    case later

    static func of(_ date: Date?, calendar: Calendar = .current, now: Date = Date()) -> ActiveDayBucket {
        guard let date else { return .later }
        let day = calendar.startOfDay(for: date)
        let today = calendar.startOfDay(for: now)
        guard let tomorrow = calendar.date(byAdding: .day, value: 1, to: today) else { return .later }
        if day == today { return .today }
        if day == tomorrow { return .tomorrow }
        return .later
    }
}

enum OrdersQueryBuilder {
    /// Per-tab query (the `OrdersListViewModel.kt:235-261` parity). O3: `employeeId`
    /// is supplied ONLY for the "mine" panes (Active/History) and is the caller's
    /// own id; Available passes `isUnassigned=true` with no employee scope.
    static func query(
        tab: OrdersTab,
        ownEmployeeId: String?,
        sort: AvailableSort,
        period: CompletedPeriod,
        calendar: Calendar = .current,
        now: Date = Date()
    ) -> OrderPageQuery {
        switch tab {
        case .available:
            return OrderPageQuery(
                statuses: [._0, ._2],
                isUnassigned: true,
                employeeId: nil,
                cleaningDateFrom: nil,
                cleaningDateTo: nil,
                sortField: sort.field,
                sortAscending: sort.ascending
            )
        case .active:
            return OrderPageQuery(
                statuses: [._2, ._3, ._4],
                isUnassigned: nil,
                employeeId: ownEmployeeId,
                cleaningDateFrom: nil,
                cleaningDateTo: nil,
                sortField: "cleaningDateTime",
                sortAscending: true
            )
        case .history:
            let range = periodToDateRange(period, calendar: calendar, now: now)
            return OrderPageQuery(
                statuses: [._5],
                isUnassigned: nil,
                employeeId: ownEmployeeId,
                cleaningDateFrom: range.from,
                cleaningDateTo: range.to,
                sortField: "cleaningDateTime",
                sortAscending: false
            )
        }
    }

    /// Half-open [from, to) date range for the History period filter (the
    /// `periodToDateRange` parity, `OrdersListViewModel.kt:366-388`).
    static func periodToDateRange(
        _ period: CompletedPeriod,
        calendar baseCalendar: Calendar = .current,
        now: Date = Date()
    ) -> (from: Date?, to: Date?) {
        var calendar = baseCalendar
        calendar.timeZone = baseCalendar.timeZone
        let today = calendar.startOfDay(for: now)

        switch period {
        case .thisWeek:
            guard let weekStart = calendar.dateInterval(of: .weekOfYear, for: today)?.start,
                  let weekEnd = calendar.date(byAdding: .day, value: 7, to: weekStart)
            else { return (nil, nil) }
            return (weekStart, weekEnd)
        case .thisMonth:
            guard let monthStart = calendar.dateInterval(of: .month, for: today)?.start,
                  let monthEnd = calendar.date(byAdding: .month, value: 1, to: monthStart)
            else { return (nil, nil) }
            return (monthStart, monthEnd)
        case .lastMonth:
            guard let thisMonthStart = calendar.dateInterval(of: .month, for: today)?.start,
                  let lastMonthStart = calendar.date(byAdding: .month, value: -1, to: thisMonthStart)
            else { return (nil, nil) }
            return (lastMonthStart, thisMonthStart)
        case .all:
            return (nil, nil)
        }
    }
}

extension OrderListItem {
    /// Client-side row filter (the `matchesSearch` parity): order number,
    /// customer name, or address contains the query (case-insensitive).
    func matchesSearch(_ query: String) -> Bool {
        let trimmed = query.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty else { return true }
        let needle = trimmed.lowercased()
        let haystacks = [displayOrderNumber, customerName, customerAddress]
        return haystacks.contains { $0?.lowercased().contains(needle) == true }
    }

    var coordinate: Coordinate? {
        guard let lat = customerAddressLatitude, let lon = customerAddressLongitude else { return nil }
        return Coordinate(latitude: lat, longitude: lon)
    }

    func distanceKm(from current: Coordinate?) -> Double? {
        guard let current, let coordinate else { return nil }
        return current.distanceKm(to: coordinate)
    }

    var isInProgress: Bool {
        status == ._4
    }
}
