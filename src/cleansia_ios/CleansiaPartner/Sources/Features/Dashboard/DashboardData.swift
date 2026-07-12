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
        stats: DashboardStatsDto,
        preview: AvailableJobsPreviewResponse?,
        firstName: String?
    ) -> DashboardData {
        let payPeriod: PayPeriod? = {
            guard let start = stats.currentPayPeriodStart, let end = stats.currentPayPeriodEnd else { return nil }
            return PayPeriod(
                start: start,
                end: end,
                earnings: stats.currentPeriodEarnings ?? 0,
                nextPayoutDate: stats.nextPayoutDate
            )
        }()

        return DashboardData(
            firstName: firstName,
            currencyCode: stats.currencyCode,
            weekEarnings: stats.weekEarnings ?? 0,
            weekCompletedCount: stats.weekCompletedCount ?? 0,
            todayEarnings: stats.todayEarnings ?? 0,
            payPeriod: payPeriod,
            lastMonthEarnings: stats.lastMonthEarnings ?? 0,
            lastMonthCompletedOrders: stats.lastMonthCompletedOrders ?? 0,
            thisMonthCompletedOrders: stats.thisMonthCompletedOrders ?? 0,
            averageRating: stats.averageRating,
            ratingCount: stats.ratingCount ?? 0,
            hero: hero(from: preview)
        )
    }

    private static func hero(from preview: AvailableJobsPreviewResponse?) -> DashboardHero {
        let jobCount = preview?.totalAvailableCount ?? 0
        guard jobCount > 0 else { return .empty }
        return .availableWork(jobCount: jobCount, potentialEarnings: preview?.totalPotentialEarnings ?? 0)
    }
}

enum DashboardHero: Equatable {
    case nextJob(title: String, subtitle: String?)
    case availableWork(jobCount: Int, potentialEarnings: Double)
    case empty
}
