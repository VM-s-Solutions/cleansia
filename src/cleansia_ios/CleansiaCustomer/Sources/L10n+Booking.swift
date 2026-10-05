import Foundation

extension L10n {
    enum Booking {
        static func stepTitle(_ step: Int) -> String {
            switch step {
            case 1: localized("booking_step1_title")
            case 2: dirtinessQuestion
            case 3: localized("booking_step2_title")
            case 4: localized("booking_step3_title")
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
            plural("booking_package_more", count)
        }

        static func priceFrom(_ price: String) -> String {
            format("booking_price_from", price)
        }

        static func pricePerRoom(_ price: String) -> String {
            format("booking_price_per_room", price)
        }

        static var yourHome: String {
            localized("booking_your_home")
        }

        static func roomsShort(_ count: Int) -> String {
            plural("booking_rooms_short", count)
        }

        static func bathShort(_ count: Int) -> String {
            plural("booking_bath_short", count)
        }

        /// "Up to 8 rooms and 4 bathrooms" — the numbers are `PropertySize`'s, pinned to the server's caps.
        static var sizeLimitCaption: String {
            format("booking_size_limit_caption", PropertySize.maxRooms, PropertySize.maxBathrooms)
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

        static var today: String {
            localized("booking_today")
        }

        static var whereLabel: String {
            localized("booking_where")
        }

        static var whenLabel: String {
            localized("booking_when")
        }

        static var selectTime: String {
            localized("booking_select_time")
        }

        static var arrivalWindow: String {
            localized("booking_arrival_window")
        }

        static var slotExpress: String {
            localized("booking_slot_express")
        }

        static var slotExpressWaived: String {
            localized("booking_slot_express_waived")
        }

        static func expressWaiverAvailable(_ remaining: Int) -> String {
            format("booking_express_waiver_available", remaining)
        }

        static var expressWaiverUsed: String {
            localized("booking_express_waiver_used")
        }

        static var dayPartMorning: String {
            localized("booking_day_part_morning")
        }

        static var dayPartAfternoon: String {
            localized("booking_day_part_afternoon")
        }

        static var dayPartEvening: String {
            localized("booking_day_part_evening")
        }

        /// "6 slots available" — read after the part's name.
        static func dayPartSlotsAvailable(_ count: Int) -> String {
            plural("booking_day_part_slots_available", count)
        }

        static var allSlotsBooked: String {
            localized("booking_all_slots_booked")
        }

        static var cancelHint: String {
            localized("booking_cancel_hint")
        }

        static var selectAddress: String {
            localized("address_manager_select")
        }

        static var selectAddressHint: String {
            localized("booking_select_address_hint")
        }

        static var extrasHeader: String {
            localized("booking_extras_header")
        }

        static var extrasSubtitle: String {
            localized("booking_extras_subtitle")
        }

        static var summaryItemsLabel: String {
            localized("booking_summary_items_label")
        }

        static var summaryDetailsLabel: String {
            localized("booking_summary_details_label")
        }

        static var summaryAddress: String {
            localized("booking_summary_address")
        }

        static var summaryProperty: String {
            localized("booking_summary_property")
        }

        static var summaryDate: String {
            localized("booking_summary_date")
        }

        static var summaryTime: String {
            localized("booking_summary_time")
        }

        static var summarySubtotal: String {
            localized("booking_summary_subtotal")
        }

        static var summaryExpressSurcharge: String {
            localized("booking_summary_express_surcharge")
        }

        static var summaryExpressSurchargeWaived: String {
            localized("booking_summary_express_surcharge_waived")
        }

        static func summaryPromoDiscount(_ code: String) -> String {
            format("booking_summary_promo_discount", code)
        }

        static var summaryMembershipDiscount: String {
            localized("booking_summary_membership_discount")
        }

        static var summaryTierDiscount: String {
            localized("booking_summary_tier_discount")
        }

        static var summaryTotal: String {
            localized("booking_summary_total")
        }

        static var paymentMethod: String {
            localized("booking_payment_method")
        }

        static var payCard: String {
            localized("booking_pay_card")
        }

        static var payCardDesc: String {
            localized("booking_pay_card_desc")
        }

        static var payCash: String {
            localized("booking_pay_cash")
        }

        static var payCashDesc: String {
            localized("booking_pay_cash_desc")
        }

        static var promoRowTitle: String {
            localized("booking_promo_code_row_title")
        }

        static func promoRowApplied(_ code: String) -> String {
            format("booking_promo_code_row_applied", code)
        }

        static var promoRowClear: String {
            localized("booking_promo_code_row_clear")
        }

        static var promoDialogTitle: String {
            localized("booking_promo_code_dialog_title")
        }

        static var promoDialogHelper: String {
            localized("booking_promo_code_dialog_helper")
        }

        static var promoDialogApply: String {
            localized("booking_promo_code_dialog_apply")
        }

        static var promoDialogCancel: String {
            localized("booking_promo_code_dialog_cancel")
        }

        static var promoDialogDone: String {
            localized("booking_promo_code_dialog_done")
        }

        static func promoDialogSuccess(_ amount: String) -> String {
            format("booking_promo_code_dialog_success", amount)
        }

        static var promoValidating: String {
            localized("booking_promo_code_validating")
        }

        static func promoError(_ error: PromoCodeError?) -> String {
            switch error {
            case .notFound: localized("booking_promo_code_error_not_found")
            case .inactive: localized("booking_promo_code_error_inactive")
            case .expired: localized("booking_promo_code_error_expired")
            case .notYetValid: localized("booking_promo_code_error_not_yet_valid")
            case .globalLimitReached: localized("booking_promo_code_error_global_limit")
            case .perUserLimitReached: localized("booking_promo_code_error_used")
            case .belowMinimumOrderAmount: localized("booking_promo_code_error_min_order")
            case .currencyMismatch: localized("booking_promo_code_error_currency")
            case nil: localized("booking_promo_code_error_generic")
            }
        }

        static var referralRowTitle: String {
            localized("booking_referral_code_row_title")
        }

        static func referralRowApplied(_ code: String) -> String {
            format("booking_referral_code_row_applied", code)
        }

        static var referralRowClear: String {
            localized("booking_referral_code_row_clear")
        }

        static var referralDialogTitle: String {
            localized("booking_referral_code_dialog_title")
        }

        static func referralDialogHelper(_ credit: String) -> String {
            format("booking_referral_code_dialog_helper", credit)
        }

        static var referralDialogHelperNoFigure: String {
            localized("booking_referral_code_dialog_helper_no_figure")
        }

        static var referralDialogApply: String {
            localized("booking_referral_code_dialog_apply")
        }

        static var referralDialogCancel: String {
            localized("booking_referral_code_dialog_cancel")
        }

        static var referralDialogDone: String {
            localized("booking_referral_code_dialog_done")
        }

        static func referralDialogSuccessNamed(_ name: String, _ credit: String) -> String {
            format("booking_referral_code_dialog_success_named", name, credit)
        }

        static func referralDialogSuccessNamedNoFigure(_ name: String) -> String {
            format("booking_referral_code_dialog_success_named_no_figure", name)
        }

        static func referralDialogSuccess(_ credit: String) -> String {
            format("booking_referral_code_dialog_success", credit)
        }

        static var referralDialogSuccessNoFigure: String {
            localized("booking_referral_code_dialog_success_no_figure")
        }

        static func referralError(_ error: ReferralValidationError?) -> String {
            switch error {
            case .notFound: localized("error_referral_not_found")
            case .selfReferral: localized("error_referral_self_referral")
            case .alreadyReferred: localized("error_referral_already_referred")
            case .inactive: localized("error_referral_inactive")
            case nil: localized("error_referral_generic")
            }
        }

        static var packageIncludes: String {
            localized("booking_package_includes")
        }

        static var addToBooking: String {
            localized("details_add_to_booking")
        }

        static var removeFromBooking: String {
            localized("details_remove_from_booking")
        }

        static func inYourPackage(_ packages: String) -> String {
            format("booking_in_your_package", packages)
        }

        static func inYourPackages(_ packages: String) -> String {
            format("booking_in_your_packages", packages)
        }

        static var twiceServiceTitle: String {
            localized("booking_twice_service_title")
        }

        static var twiceServiceTitleMany: String {
            localized("booking_twice_service_title_many")
        }

        static func twiceServiceMessage(service: String, packages: String) -> String {
            format("booking_twice_service_message", service, packages)
        }

        static func twiceServiceMessageMany(service: String, packages: String) -> String {
            format("booking_twice_service_message_many", service, packages)
        }

        static var twiceServiceConfirm: String {
            localized("booking_twice_service_confirm")
        }

        static var twicePackageTitle: String {
            localized("booking_twice_package_title")
        }

        static func twicePackageMessage(package: String, services: String) -> String {
            format("booking_twice_package_message", package, services)
        }

        static var twicePackageConfirm: String {
            localized("booking_twice_package_confirm")
        }
    }
}
