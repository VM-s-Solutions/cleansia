import Foundation

extension L10n {
    enum Recurring {
        static var bookingsTitle: String {
            localized("recurring_bookings_title")
        }

        static var emptyTitle: String {
            localized("recurring_bookings_empty_title")
        }

        static var emptySubtitle: String {
            localized("recurring_bookings_empty_subtitle")
        }

        static var emptyCta: String {
            localized("recurring_bookings_empty_cta")
        }

        static var createFab: String {
            localized("recurring_bookings_create_fab")
        }

        static var pausedBadge: String {
            localized("recurring_bookings_paused_badge")
        }

        static var pause: String {
            localized("recurring_bookings_pause")
        }

        static var resume: String {
            localized("recurring_bookings_resume")
        }

        static var delete: String {
            localized("recurring_bookings_delete")
        }

        static var deleteDialogTitle: String {
            localized("recurring_bookings_delete_dialog_title")
        }

        static var deleteDialogConfirm: String {
            localized("recurring_bookings_delete_dialog_confirm")
        }

        static var deleteDialogWhatStops: String {
            localized("recurring_bookings_delete_dialog_what_stops")
        }

        static var deleteDialogWhatStays: String {
            localized("recurring_bookings_delete_dialog_what_stays")
        }

        static var deleteDialogPauseHint: String {
            localized("recurring_bookings_delete_dialog_pause_hint")
        }

        static var cadenceWeekly: String {
            localized("recurring_bookings_cadence_weekly")
        }

        static var cadenceBiweekly: String {
            localized("recurring_bookings_cadence_biweekly")
        }

        static var cadenceMonthly: String {
            localized("recurring_bookings_cadence_monthly")
        }

        static func dayAtTime(_ day: String, _ time: String) -> String {
            format("recurring_bookings_day_at_time", day, time)
        }

        static func cadence(_ frequency: Int) -> String {
            switch frequency {
            case 2: cadenceBiweekly
            case 3: cadenceMonthly
            default: cadenceWeekly
            }
        }

        static var plusGateTitle: String {
            localized("recurring_plus_gate_title")
        }

        static var plusGateSubtitle: String {
            localized("recurring_plus_gate_subtitle")
        }

        static var plusGateCta: String {
            localized("recurring_plus_gate_cta")
        }

        static var createTitleBlank: String {
            localized("recurring_create_title_blank")
        }

        static var createTitleFromOrder: String {
            localized("recurring_create_title_from_order")
        }

        static var createFrequencyLabel: String {
            localized("recurring_create_frequency_label")
        }

        static var createDayLabel: String {
            localized("recurring_create_day_label")
        }

        static var createTimeLabel: String {
            localized("recurring_create_time_label")
        }

        static var createRoomsLabel: String {
            localized("recurring_create_rooms_label")
        }

        static var createBathroomsLabel: String {
            localized("recurring_create_bathrooms_label")
        }

        static var createAddressLabel: String {
            localized("recurring_create_address_label")
        }

        static var createAddressDefault: String {
            localized("recurring_create_address_default")
        }

        static var createAddressEmpty: String {
            localized("recurring_create_address_empty")
        }

        static var createServicesLabel: String {
            localized("recurring_create_services_label")
        }

        static var createSectionServices: String {
            localized("recurring_create_section_services")
        }

        static var createSectionPackages: String {
            localized("recurring_create_section_packages")
        }

        static var createPaymentLabel: String {
            localized("recurring_create_payment_label")
        }

        static var createPayCash: String {
            localized("recurring_create_pay_cash")
        }

        static var createPayCard: String {
            localized("recurring_create_pay_card")
        }

        static var createStartsLabel: String {
            localized("recurring_create_starts_label")
        }

        static var createSubmit: String {
            localized("recurring_create_submit")
        }

        static var createSuccess: String {
            localized("recurring_create_success")
        }

        static var createFailed: String {
            localized("recurring_create_failed")
        }

        static var freqWeeklyLabel: String {
            localized("recurring_freq_weekly_label")
        }

        static var freqBiweeklyLabel: String {
            localized("recurring_freq_biweekly_label")
        }

        static var freqMonthlyLabel: String {
            localized("recurring_freq_monthly_label")
        }

        static var confirmCta: String {
            localized("recurring_confirm_cta")
        }

        static var confirmSuccess: String {
            localized("recurring_confirm_success")
        }

        static var back: String {
            localized("common_back")
        }
    }
}
