import Foundation

extension L10n {
    enum Orders {
        static var title: String {
            localized("orders")
        }

        static var available: String {
            localized("available")
        }

        static var active: String {
            localized("active")
        }

        static var history: String {
            localized("history")
        }

        static var completed: String {
            localized("completed")
        }

        static var takeOrder: String {
            localized("take_order")
        }

        static var youEarn: String {
            localized("you_earn")
        }

        static func kmAway(_ value: String) -> String {
            format("km_away", value)
        }

        static var topPay: String {
            localized("top_pay")
        }

        static var startsSoon: String {
            localized("starts_soon")
        }

        static var noOrdersAvailable: String {
            localized("no_orders_available")
        }

        static var noActiveOrders: String {
            localized("no_active_orders")
        }

        static var noCompletedOrders: String {
            localized("no_completed_orders")
        }

        static var noMatchingOrders: String {
            localized("no_matching_orders")
        }

        static var searchHint: String {
            localized("search_orders_hint")
        }

        static func availableSummary(_ count: Int, _ earnings: String) -> String {
            format("available_summary", count, earnings)
        }

        static var sortEarningsHighToLow: String {
            localized("sort_earnings_high_to_low")
        }

        static var sortSoonestFirst: String {
            localized("sort_soonest_first")
        }

        static var sortPriceHighToLow: String {
            localized("sort_price_high_to_low")
        }

        static var periodThisWeek: String {
            localized("period_this_week")
        }

        static var periodThisMonth: String {
            localized("period_this_month")
        }

        static var periodLastMonth: String {
            localized("period_last_month")
        }

        static var periodAll: String {
            localized("period_all")
        }

        static var dayToday: String {
            localized("day_today")
        }

        static var dayTomorrow: String {
            localized("day_tomorrow")
        }

        static var dayLater: String {
            localized("day_later")
        }

        static func rooms(_ count: Int) -> String {
            format("scope_rooms", count)
        }

        static func baths(_ count: Int) -> String {
            format("scope_baths", count)
        }

        static func extras(_ count: Int) -> String {
            format("scope_extras", count)
        }

        static var inProgressNow: String {
            localized("in_progress_now")
        }

        static var earnings: String {
            localized("earnings")
        }

        static var jobs: String {
            localized("jobs")
        }

        static var guest: String {
            localized("guest")
        }

        static var unscheduled: String {
            localized("unscheduled")
        }
    }
}
