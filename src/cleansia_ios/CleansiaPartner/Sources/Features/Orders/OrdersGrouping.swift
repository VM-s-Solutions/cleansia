import CleansiaPartnerApi
import Foundation

enum OrdersGrouping {
    /// Active-tab grouping: Today / Tomorrow / Later, buckets in lifecycle order,
    /// empty buckets dropped (the `ActivePane` parity).
    static func byDayBucket(
        _ orders: [OrderListItem],
        calendar: Calendar = .current,
        now: Date = Date()
    ) -> [(ActiveDayBucket, [OrderListItem])] {
        let grouped = Dictionary(grouping: orders) { order in
            ActiveDayBucket.of(order.cleaningDateTime, calendar: calendar, now: now)
        }
        return ActiveDayBucket.allCases.compactMap { bucket in
            guard let rows = grouped[bucket], !rows.isEmpty else { return nil }
            return (bucket, rows)
        }
    }

    /// History-tab grouping: by calendar day, most-recent first; nil dates
    /// (unscheduled) sort last (the `HistoryPane` parity).
    static func byDay(
        _ orders: [OrderListItem],
        calendar: Calendar = .current
    ) -> [(Date?, [OrderListItem])] {
        let grouped = Dictionary(grouping: orders) { order -> Date? in
            order.cleaningDateTime.map { calendar.startOfDay(for: $0) }
        }
        return grouped.sorted { lhs, rhs in
            switch (lhs.key, rhs.key) {
            case let (left?, right?): left > right
            case (_?, nil): true
            case (nil, _?): false
            case (nil, nil): false
            }
        }.map { ($0.key, $0.value) }
    }
}
