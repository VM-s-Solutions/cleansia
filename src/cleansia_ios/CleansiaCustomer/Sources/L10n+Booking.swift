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
    }
}
