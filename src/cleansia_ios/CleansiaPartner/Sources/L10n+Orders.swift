import CleansiaPartnerApi
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

        static var locationPrompt: String {
            localized("orders_location_prompt")
        }

        static var locationPromptAction: String {
            localized("orders_location_prompt_action")
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

        static func crewSize(_ count: Int) -> String {
            format("crew_size", count)
        }

        static func crewSpotsOpen(_ count: Int) -> String {
            format("crew_spots_open", count)
        }

        static var crewNoSpots: String {
            localized("crew_no_spots")
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

        // Detail

        static var back: String {
            localized("back")
        }

        static var actionCall: String {
            localized("action_call")
        }

        static var actionSms: String {
            localized("action_sms")
        }

        static var actionNavigate: String {
            localized("action_navigate")
        }

        static var mapApproximateArea: String {
            localized("map_approximate_area")
        }

        static var scopeSectionTitle: String {
            localized("scope_section_title")
        }

        static var scopeServicesLabel: String {
            localized("scope_services_label")
        }

        static var scopeExtrasLabel: String {
            localized("scope_extras_label")
        }

        static var accessSectionTitle: String {
            localized("access_section_title")
        }

        static var fromCustomerSectionTitle: String {
            localized("from_customer_section_title")
        }

        static var noteGeneralLabel: String {
            localized("note_general_label")
        }

        static var noteSpecialLabel: String {
            localized("note_special_label")
        }

        static var paymentSectionTitle: String {
            localized("payment_section_title")
        }

        static var paymentSubtotal: String {
            localized("payment_subtotal")
        }

        static var paymentTierDiscount: String {
            localized("payment_tier_discount")
        }

        static var paymentMembershipDiscount: String {
            localized("payment_membership_discount")
        }

        static var paymentPromoDiscount: String {
            localized("payment_promo_discount")
        }

        static var paymentTotal: String {
            localized("payment_total")
        }

        static func paymentMethod(_ value: String) -> String {
            format("payment_method_value", value)
        }

        static var paymentMethodCash: String {
            localized("payment_method_cash")
        }

        static var paymentMethodCard: String {
            localized("payment_method_card")
        }

        static var paymentStatusPaid: String {
            localized("payment_status_paid")
        }

        static var paymentStatusPending: String {
            localized("payment_status_pending")
        }

        static var paymentStatusFailed: String {
            localized("payment_status_failed")
        }

        static var paymentStatusRefunded: String {
            localized("payment_status_refunded")
        }

        static var paymentStatusDisputed: String {
            localized("payment_status_disputed")
        }

        static var paymentStatusPartiallyRefunded: String {
            localized("payment_status_partially_refunded")
        }

        static var photosSectionTitle: String {
            localized("photos")
        }

        static var photosNoneRecorded: String {
            localized("photos_none_recorded")
        }

        static var photosBefore: String {
            localized("before")
        }

        static var photosAfter: String {
            localized("after")
        }

        static var addPhoto: String {
            localized("add_photo")
        }

        static var deletePhoto: String {
            localized("delete_photo")
        }

        static var takePhoto: String {
            localized("take_photo")
        }

        static var chooseFromLibrary: String {
            localized("choose_from_library")
        }

        static var photoEncodeFailed: String {
            localized("photo_encode_failed")
        }

        static var cameraPermissionTitle: String {
            localized("camera_permission_title")
        }

        static var cameraPermissionMessage: String {
            localized("camera_permission_message")
        }

        static var openSettings: String {
            localized("open_settings")
        }

        // MARK: - Timer hero

        /// Kept for key parity with Android even though the iOS card starts at
        /// Confirmed — see `OrderTimer.phase(for:now:)`.
        static var trackerHeadlineNew: String {
            localized("tracker_headline_new")
        }

        static var trackerHeadlineConfirmed: String {
            localized("tracker_headline_confirmed")
        }

        static var trackerHeadlineConfirmedSoon: String {
            localized("tracker_headline_confirmed_soon")
        }

        static var trackerHeadlineOnTheWay: String {
            localized("tracker_headline_on_the_way")
        }

        static var trackerHeadlineInProgress: String {
            localized("tracker_headline_in_progress")
        }

        static var trackerHeadlineDone: String {
            localized("tracker_headline_done")
        }

        static func trackerCountdownStartsIn(_ duration: String) -> String {
            format("tracker_countdown_starts_in", duration)
        }

        static func trackerSubtitleOnTheWayArriving(_ time: String) -> String {
            format("tracker_subtitle_on_the_way_arriving", time)
        }

        static func trackerCompletedIn(_ duration: String) -> String {
            format("tracker_completed_in", duration)
        }

        static func trackerFinishedAt(_ dateTime: String) -> String {
            format("tracker_finished_at", dateTime)
        }

        static func trackerStepCounter(_ step: Int, _ total: Int) -> String {
            format("tracker_step_counter", step, total)
        }

        static func durationHoursMinutes(_ hours: Int, _ minutes: Int) -> String {
            format("duration_hours_minutes", hours, minutes)
        }

        static func durationMinutesOnly(_ minutes: Int) -> String {
            format("duration_minutes_only", minutes)
        }

        static func statusLabel(_ status: OrderStatus?) -> String {
            switch status {
            case ._0: localized("status_new")
            case ._1: localized("status_pending")
            case ._2: localized("status_confirmed")
            case ._3: localized("status_on_the_way")
            case ._4: localized("status_in_progress")
            case ._5: localized("status_completed")
            case ._6: localized("status_cancelled")
            case .none: "—"
            }
        }
    }
}
