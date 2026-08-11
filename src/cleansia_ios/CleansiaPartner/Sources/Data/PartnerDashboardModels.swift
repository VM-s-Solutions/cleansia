import CleansiaCore
import CleansiaPartnerApi
import Foundation

/// **Refuse.** There is no page and no row here — one object whose every member is a figure the
/// dashboard and the earnings tab render directly. A coerced `0` is indistinguishable on screen from
/// a real zero week, so a renamed wire field would tell a cleaner who earned 4,800 that they earned
/// nothing, and nothing anywhere would go red.
///
/// `averageRating` is `double?` by design — null means never reviewed, which the rating row already
/// renders — so forcing it would destroy the distinction the server drew. The pay-period dates are
/// nullable for accounts not yet attached to a period; the window simply does not render.
struct DashboardStats: Equatable {
    let todayEarnings: Double
    let todayCompletedCount: Int
    let weekEarnings: Double
    let weekCompletedCount: Int
    let lastMonthEarnings: Double
    let lastMonthCompletedOrders: Int
    let thisMonthCompletedOrders: Int
    let currentPeriodEarnings: Double
    let ratingCount: Int
    let averageRating: Double?
    let currentPayPeriodStart: Date?
    let currentPayPeriodEnd: Date?
    let nextPayoutDate: Date?
    let latestInvoiceStatus: String?
    let currencyCode: String?
}

extension DashboardStats {
    init(_ dto: DashboardStatsDto) throws {
        todayEarnings = try dto.todayEarnings.require("todayEarnings")
        todayCompletedCount = try dto.todayCompletedCount.require("todayCompletedCount")
        weekEarnings = try dto.weekEarnings.require("weekEarnings")
        weekCompletedCount = try dto.weekCompletedCount.require("weekCompletedCount")
        lastMonthEarnings = try dto.lastMonthEarnings.require("lastMonthEarnings")
        lastMonthCompletedOrders = try dto.lastMonthCompletedOrders.require("lastMonthCompletedOrders")
        thisMonthCompletedOrders = try dto.thisMonthCompletedOrders.require("thisMonthCompletedOrders")
        currentPeriodEarnings = try dto.currentPeriodEarnings.require("currentPeriodEarnings")
        ratingCount = try dto.ratingCount.require("ratingCount")
        averageRating = dto.averageRating
        currentPayPeriodStart = dto.currentPayPeriodStart
        currentPayPeriodEnd = dto.currentPayPeriodEnd
        nextPayoutDate = dto.nextPayoutDate
        latestInvoiceStatus = dto.latestInvoiceStatus
        currencyCode = dto.currencyCode
    }
}

/// **Refuse.** The hero says "N jobs, earn up to X"; both numbers come from this response and
/// neither is derived from the `jobs` list, which the iOS hero never renders. The whole call is
/// optional at the call site — a preview that fails leaves the hero empty — so a refusal here costs
/// the hero and nothing else.
struct AvailableJobsPreview: Equatable {
    let totalAvailableCount: Int
    let totalPotentialEarnings: Double
}

extension AvailableJobsPreview {
    init(_ response: AvailableJobsPreviewResponse) throws {
        totalAvailableCount = try response.totalAvailableCount.require("totalAvailableCount")
        totalPotentialEarnings = try response.totalPotentialEarnings.require("totalPotentialEarnings")
    }
}

#if DEBUG
    extension DashboardStats {
        static let previewEmpty = DashboardStats(
            todayEarnings: 0,
            todayCompletedCount: 0,
            weekEarnings: 0,
            weekCompletedCount: 0,
            lastMonthEarnings: 0,
            lastMonthCompletedOrders: 0,
            thisMonthCompletedOrders: 0,
            currentPeriodEarnings: 0,
            ratingCount: 0,
            averageRating: nil,
            currentPayPeriodStart: nil,
            currentPayPeriodEnd: nil,
            nextPayoutDate: nil,
            latestInvoiceStatus: nil,
            currencyCode: nil
        )
    }
#endif
