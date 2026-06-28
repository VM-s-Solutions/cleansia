import Foundation

extension L10n {
    enum Booking {
        static func stepTitle(_ step: Int) -> String {
            switch step {
            case 1: localized("booking_step1_title")
            case 2: localized("booking_step2_title")
            case 3: localized("booking_step3_title")
            default: ""
            }
        }

        static func stepIndicator(_ step: Int, _ total: Int) -> String {
            format("booking_step_indicator", step, total)
        }

        static var continueAction: String {
            localized("booking_continue")
        }

        static var slideToConfirm: String {
            localized("booking_slide_to_confirm")
        }

        static var close: String {
            localized("common_close")
        }

        static var back: String {
            localized("common_back")
        }

        static var stepComingSoon: String {
            localized("booking_step_coming_soon")
        }

        static var packagesFeatured: String {
            localized("booking_packages_featured")
        }

        static var pickService: String {
            localized("booking_pick_service")
        }

        static var packageIncludesPrefix: String {
            localized("booking_package_includes_prefix")
        }

        static func packageMore(_ count: Int) -> String {
            format("booking_package_more", count)
        }

        static func priceFrom(_ amount: Int) -> String {
            format("booking_price_from", amount)
        }

        static func pricePerRoom(_ amount: Int) -> String {
            format("booking_price_per_room", amount)
        }

        static var yourHome: String {
            localized("booking_your_home")
        }

        static func roomsShort(_ count: Int) -> String {
            format("booking_rooms_short", count)
        }

        static func bathShort(_ count: Int) -> String {
            format("booking_bath_short", count)
        }

        static var noResults: String {
            localized("booking_no_results")
        }

        static var catAll: String {
            localized("booking_cat_all")
        }

        static var catalogLoading: String {
            localized("booking_catalog_loading")
        }

        static var catalogError: String {
            localized("booking_catalog_error")
        }

        static var catalogEmpty: String {
            localized("booking_catalog_empty")
        }

        static var catalogRetry: String {
            localized("booking_catalog_retry")
        }

        static var details: String {
            localized("common_details")
        }

        static func continuePrice(_ total: String) -> String {
            format("booking_continue_price", total)
        }

        static func slideToConfirmPrice(_ total: String) -> String {
            format("booking_slide_to_confirm_price", total)
        }
    }
}
