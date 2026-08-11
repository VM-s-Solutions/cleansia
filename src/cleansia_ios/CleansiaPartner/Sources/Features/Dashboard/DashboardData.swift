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
        firstName: String?
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
            hero: hero(from: preview)
        )
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
    case nextJob(title: String, subtitle: String?)
    case availableWork(jobCount: Int, potentialEarnings: Double)
    case empty
}
