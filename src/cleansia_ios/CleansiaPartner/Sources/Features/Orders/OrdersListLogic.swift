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

/// The Available tab's order. The server sorts by start across every page; pay it sorts within a page only, so
/// "best pay" is a local sort offered only once every row is loaded (`OrdersBoardRules.bestPayOffered`).
enum AvailableSort: CaseIterable {
    case soonest
    case bestPay
}

enum CompletedPeriod: CaseIterable {
    case thisWeek
    case thisMonth
    case lastMonth
}

enum OrdersQueryBuilder {
    static func query(
        tab: OrdersTab,
        ownEmployeeId: String?,
        period: CompletedPeriod,
        calendar: Calendar = .current,
        now: Date = Date()
    ) -> OrderPageQuery {
        switch tab {
        case .available:
            return OrderPageQuery(
                statuses: [._0, ._2, ._3, ._4],
                isUnassigned: nil,
                employeeId: nil,
                hasAvailableSpots: true,
                excludeEmployeeId: ownEmployeeId,
                cleaningDateFrom: nil,
                cleaningDateTo: nil,
                sortField: "cleaningDateTime",
                sortAscending: true
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

    /// The row a take just made the cleaner's own, read back through the list (never the audited detail read)
    /// so the taken state can show its street.
    static func takenRow(orderId: String, ownEmployeeId: String) -> OrderPageQuery {
        var query = query(tab: .active, ownEmployeeId: ownEmployeeId, period: .thisMonth)
        query.id = orderId
        query.limit = 1
        return query
    }

    /// A reserved job's board row when it is not on the loaded page: the Available query narrowed to its id.
    static func reservedRow(orderId: String, ownEmployeeId: String) -> OrderPageQuery {
        var query = query(tab: .available, ownEmployeeId: ownEmployeeId, period: .thisMonth)
        query.id = orderId
        query.limit = 1
        return query
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
        calendar.firstWeekday = 2
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
        }
    }

    /// The period's name on the History control: "This week", else the standalone month name ("LLLL"), its first
    /// letter upper-cased in the locale.
    static func periodLabel(
        _ period: CompletedPeriod,
        locale: Locale,
        calendar: Calendar = .current,
        now: Date = Date()
    ) -> String {
        guard period != .thisWeek, let start = periodToDateRange(period, calendar: calendar, now: now).from else {
            return L10n.Orders.periodThisWeek
        }
        let month = OrdersFormat.pattern("LLLL", start, locale: locale)
        guard let first = month.first else { return month }
        return String(first).uppercased(with: locale) + month.dropFirst()
    }
}

extension OrderListItem {
    var isInProgress: Bool {
        status == ._4
    }
}

extension PartnerOrderClient {
    /// Every page of `query`, so the overlap check and a period's totals see every row: the Active and History
    /// tabs, and the job detail's own read of her jobs when the board holds none.
    func everyPage(_ query: OrderPageQuery) async -> ApiResult<OrderPage> {
        var rows: [OrderListItem] = []
        var offset = 0
        for _ in 0 ..< 50 {
            var page = query
            page.offset = offset
            switch await getPaged(page) {
            case let .success(result):
                rows = OrdersBoardRules.appendPage(rows, result.rows)
                offset += result.rows.count
                let done = OrdersBoardRules.pagesComplete(
                    loaded: offset,
                    total: result.total,
                    lastPageCount: result.rows.count
                )
                if done { return .success(OrderPage(rows: rows, total: result.total)) }
            case let .failure(error):
                return .failure(error)
            }
        }
        return .success(OrderPage(rows: rows, total: rows.count))
    }
}
