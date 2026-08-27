import CleansiaCustomerApi
import Foundation

extension L10n {
    enum Orders {
        /// Shown over the backdrop when an order carries no usable coordinate — an order older
        /// than the geocoder, or one whose geocode failed. Worded the same as the Android twin.
        static var mapUnavailable: String {
            localized("order_detail_map_unavailable")
        }

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
            plural("orders_services_more", count)
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

        /// Two counts, so it cannot be one plural entry — a plural variation selects on ONE argument
        /// and would freeze the other noun. Composed from the two the booking wizard already uses,
        /// which also keeps the wizard and the order detail reading identically. Mirrors Android's
        /// `roomsAndBathrooms`.
        static func roomsBathrooms(_ rooms: Int, _ bathrooms: Int) -> String {
            let roomsText = plural("booking_rooms_short", rooms)
            let bathText = plural("booking_bath_short", bathrooms)
            return "\(roomsText) · \(bathText)"
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

        static var copy: String {
            localized("order_detail_copy")
        }

        static var copied: String {
            localized("order_detail_copied")
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

        static func discountLabel(_ source: OrderDiscountSource) -> String {
            switch source {
            case .tier: localized("order_detail_discount_tier")
            case .membership: localized("order_detail_discount_membership")
            case .promo: localized("order_detail_discount_promo")
            }
        }

        static var priceBreakdown: String {
            localized("order_detail_services")
        }

        static var subtotal: String {
            localized("order_detail_subtotal")
        }

        static var total: String {
            localized("order_detail_total")
        }

        static var paymentMethod: String {
            localized("order_detail_payment_method")
        }

        static var paymentStatus: String {
            localized("order_detail_payment_status")
        }

        static func paymentMethodLabel(_ method: OrderPaymentMethod) -> String {
            switch method {
            case .cash: localized("booking_pay_cash")
            case .card: localized("booking_pay_card")
            case let .named(name): name
            }
        }

        static func paymentStatusLabel(_ status: OrderPaymentStatus) -> String {
            switch status {
            case .pending: localized("orders_payment_pending")
            case .paid: localized("orders_payment_paid")
            case .failed: localized("orders_payment_failed")
            case .refunded: localized("orders_payment_refunded")
            case .disputed: localized("orders_payment_disputed")
            case let .named(name): name
            }
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

        static var headlineCompleted: String {
            localized("order_detail_headline_completed")
        }

        static func trackerStepCounter(_ step: Int, _ total: Int) -> String {
            format("tracker_step_counter", step, total)
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

        static var actionReportIssue: String {
            localized("order_action_report_issue")
        }

        static var actionRebook: String {
            localized("order_action_rebook")
        }

        static var actionMakeRecurring: String {
            localized("order_action_make_recurring")
        }

        static var rebookUnavailableItems: String {
            localized("order_rebook_unavailable_items")
        }
    }
}
