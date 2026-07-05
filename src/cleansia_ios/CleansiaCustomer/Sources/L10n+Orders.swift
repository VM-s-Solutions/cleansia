import CleansiaCustomerApi
import Foundation

extension L10n {
    enum Orders {
        static func statusLabel(_ status: OrderStatus) -> String {
            switch status {
            case ._0: localized("orders_status_new")
            case ._1: localized("orders_status_pending")
            case ._2: localized("orders_status_confirmed")
            case ._3: localized("orders_status_on_the_way")
            case ._4: localized("orders_status_in_progress")
            case ._5: localized("orders_status_completed")
            case ._6: localized("orders_status_cancelled")
            }
        }

        static var title: String {
            localized("orders_title")
        }

        static var filterAll: String {
            localized("orders_filter_all")
        }

        static var filterUpcoming: String {
            localized("orders_filter_upcoming")
        }

        static var filterCompleted: String {
            localized("orders_filter_completed")
        }

        static var filterCancelled: String {
            localized("orders_filter_cancelled")
        }

        static func filterCount(_ label: String, _ count: Int) -> String {
            format("orders_filter_count", label, count)
        }

        static func servicesMore(_ count: Int) -> String {
            format("orders_services_more", count)
        }

        static var errorTitle: String {
            localized("orders_error_title")
        }

        static var errorRetry: String {
            localized("orders_error_retry")
        }

        static var emptyTitle: String {
            localized("orders_empty_title")
        }

        static var emptySubtitle: String {
            localized("orders_empty_subtitle")
        }

        static var emptyCta: String {
            localized("orders_empty_cta")
        }

        static var viewOrder: String {
            localized("order_detail_view_order")
        }

        static var back: String {
            localized("common_back")
        }
    }

    enum OrderDetail {
        static var timeline: String {
            localized("order_detail_timeline")
        }

        static var yourReview: String {
            localized("order_detail_your_review")
        }

        static var leaveReview: String {
            localized("order_detail_leave_review")
        }

        static var editReview: String {
            localized("order_review_edit_action")
        }

        static var downloadReceipt: String {
            localized("order_detail_download_receipt")
        }

        static var receiptNotReady: String {
            localized("order_receipt_not_ready")
        }

        static var address: String {
            localized("order_detail_address")
        }

        static var sectionDetails: String {
            localized("order_detail_section_details")
        }

        static var rooms: String {
            localized("order_detail_rooms")
        }

        static func roomsBathrooms(_ rooms: Int, _ bathrooms: Int) -> String {
            format("order_detail_rooms_bathrooms", rooms, bathrooms)
        }

        static var estimated: String {
            localized("order_detail_estimated")
        }

        static func durationMinutes(_ minutes: Int) -> String {
            format("order_detail_duration_minutes", minutes)
        }

        static var completedAt: String {
            localized("order_detail_completed_at")
        }

        static var extras: String {
            localized("order_detail_extras")
        }

        static var servicesHeader: String {
            localized("order_detail_services_header")
        }

        static var packagesHeader: String {
            localized("order_detail_packages_header")
        }

        static var instructions: String {
            localized("order_detail_instructions")
        }

        static var specialInstructions: String {
            localized("order_detail_special_instructions")
        }

        static var accessInstructions: String {
            localized("order_detail_access_instructions")
        }

        static var notes: String {
            localized("order_detail_notes")
        }

        static var cleaners: String {
            localized("order_detail_cleaners")
        }

        static var cleanerFallback: String {
            localized("order_detail_cleaner_fallback")
        }

        static var codeLabel: String {
            localized("order_detail_code_label")
        }

        static var discountTier: String {
            localized("order_detail_discount_tier")
        }

        static var discountMembership: String {
            localized("order_detail_discount_membership")
        }

        static var discountPromo: String {
            localized("order_detail_discount_promo")
        }

        static var errorTitle: String {
            localized("order_detail_error_title")
        }

        static var errorMessage: String {
            localized("order_detail_error_message")
        }

        static var errorRetry: String {
            localized("order_detail_error_retry")
        }

        static var headlineConfirmed: String {
            localized("order_detail_headline_confirmed")
        }

        static func headlineConfirmedNamed(_ name: String) -> String {
            format("order_detail_headline_confirmed_named", name)
        }

        static var headlineOnTheWay: String {
            localized("order_detail_headline_on_the_way")
        }

        static func headlineOnTheWayNamed(_ name: String) -> String {
            format("order_detail_headline_on_the_way_named", name)
        }

        static var headlineInProgress: String {
            localized("order_detail_headline_in_progress")
        }

        static func headlineInProgressNamed(_ name: String) -> String {
            format("order_detail_headline_in_progress_named", name)
        }

        static var headlineDefault: String {
            localized("order_detail_headline_default")
        }

        static var subheadConfirmed: String {
            localized("order_detail_subhead_confirmed")
        }

        static var subheadOnTheWay: String {
            localized("order_detail_subhead_on_the_way")
        }

        static var subheadInProgress: String {
            localized("order_detail_subhead_in_progress")
        }

        static func subheadInProgressEta(_ minutes: Int) -> String {
            format("order_detail_subhead_in_progress_eta", minutes)
        }

        static func progressPercent(_ percent: Int) -> String {
            format("order_detail_progress_percent", percent)
        }

        static var stepBooked: String {
            localized("order_detail_step_booked")
        }

        static var stepAccepted: String {
            localized("order_detail_step_accepted")
        }

        static var stepOnTheWay: String {
            localized("order_detail_step_on_the_way")
        }

        static var stepStarted: String {
            localized("order_detail_step_started")
        }

        static var stepFinished: String {
            localized("order_detail_step_finished")
        }

        static var actionCancel: String {
            localized("order_action_cancel")
        }

        static var actionRebook: String {
            localized("order_action_rebook")
        }

        static var rebookUnavailableItems: String {
            localized("order_rebook_unavailable_items")
        }
    }
}
