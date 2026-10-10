import CleansiaCore
import CleansiaPartnerApi
import Foundation

struct DashboardData: Equatable {
    var firstName: String?
    let currencyCode: String?

    let weekEarnings: Double
    let weekCompletedCount: Int
    let todayEarnings: Double

    let payPeriod: PayPeriod?

    let lastMonthEarnings: Double
    let lastMonthCompletedOrders: Int
    let thisMonthCompletedOrders: Int
    let averageRating: Double?
    let ratingCount: Int

    let todaysJobsCount: Int
    let hero: DashboardHero

    struct PayPeriod: Equatable {
        let start: Date
        let end: Date
        let earnings: Double
        let nextPayoutDate: Date?
    }

    var averagePerJob: Double {
        weekCompletedCount > 0 ? weekEarnings / Double(weekCompletedCount) : 0
    }

    var monthDeltaPercent: Int? {
        if lastMonthCompletedOrders == 0 {
            return thisMonthCompletedOrders > 0 ? 100 : nil
        }
        let delta = Double(thisMonthCompletedOrders - lastMonthCompletedOrders)
        return Int(delta / Double(lastMonthCompletedOrders) * 100)
    }

    static func from(
        stats: DashboardStats,
        preview: AvailableJobsPreview?,
        upcoming: [OrderListItem],
        firstName: String?,
        now: Date = Date()
    ) -> DashboardData {
        let payPeriod: PayPeriod? = {
            guard let start = stats.currentPayPeriodStart, let end = stats.currentPayPeriodEnd else { return nil }
            return PayPeriod(
                start: start,
                end: end,
                earnings: stats.currentPeriodEarnings,
                nextPayoutDate: stats.nextPayoutDate
            )
        }()

        return DashboardData(
            firstName: firstName,
            currencyCode: stats.currencyCode,
            weekEarnings: stats.weekEarnings,
            weekCompletedCount: stats.weekCompletedCount,
            todayEarnings: stats.todayEarnings,
            payPeriod: payPeriod,
            lastMonthEarnings: stats.lastMonthEarnings,
            lastMonthCompletedOrders: stats.lastMonthCompletedOrders,
            thisMonthCompletedOrders: stats.thisMonthCompletedOrders,
            averageRating: stats.averageRating,
            ratingCount: stats.ratingCount,
            todaysJobsCount: todaysJobsCount(in: upcoming, now: now),
            hero: nextJob(in: upcoming) ?? hero(from: preview)
        )
    }

    private static func todaysJobsCount(in upcoming: [OrderListItem], now: Date) -> Int {
        upcoming.filter { $0.cleaningDateTime.map { Calendar.current.isDate($0, inSameDayAs: now) } ?? false }.count
    }

    /// Android's `pickNextJob`, which also orders an undated job first.
    private static func nextJob(in upcoming: [OrderListItem]) -> DashboardHero? {
        let active: Set<OrderStatus> = [._2, ._3, ._4]
        let soonest = upcoming
            .filter { $0.status.map(active.contains) ?? false }
            .min { ($0.cleaningDateTime ?? .distantPast) < ($1.cleaningDateTime ?? .distantPast) }
        guard let soonest, let status = soonest.status else { return nil }
        return .nextJob(
            orderId: soonest.id,
            status: status,
            startsAt: soonest.cleaningDateTime,
            whereLine: whereLine(name: soonest.customerName, address: soonest.customerAddress)
        )
    }

    private static func whereLine(name: String?, address: String?) -> String? {
        let parts = [name, address].compactMap { $0 }.filter { !$0.isBlank }
        return parts.isEmpty ? nil : parts.joined(separator: " · ")
    }

    /// A preview the caller never got is no jobs to show, not zero jobs available — the hero is
    /// simply absent. That is the optionality of the call, not a coerced field.
    private static func hero(from preview: AvailableJobsPreview?) -> DashboardHero {
        guard let preview, preview.totalAvailableCount > 0 else { return .empty }
        return .availableWork(
            jobCount: preview.totalAvailableCount,
            potentialEarnings: preview.totalPotentialEarnings
        )
    }
}

enum DashboardHero: Equatable {
    case nextJob(orderId: String?, status: OrderStatus, startsAt: Date?, whereLine: String?)
    case availableWork(jobCount: Int, potentialEarnings: Double)
    case empty
}
