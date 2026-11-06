import Foundation

extension L10n {
    static var close: String {
        localized("close")
    }

    enum WorkContract {
        static var title: String {
            localized("work_contract_title")
        }

        static func version(_ version: String) -> String {
            format("work_contract_version", version)
        }

        static func orderNumber(_ number: String) -> String {
            format("work_contract_order_number", number)
        }

        static var swipeToAccept: String {
            localized("work_contract_swipe_to_accept")
        }

        static var accepting: String {
            localized("work_contract_accepting")
        }

        static var loadError: String {
            localized("work_contract_load_error")
        }

        static func acceptedOn(_ date: String, _ version: String) -> String {
            format("work_contract_accepted_on", date, version)
        }

        static func acceptedInLanguage(_ language: String) -> String {
            format("work_contract_accepted_in_language", language)
        }

        static var pendingBanner: String {
            localized("work_contract_pending_banner")
        }

        static var acceptCta: String {
            localized("work_contract_accept_cta")
        }

        static var read: String {
            localized("work_contract_read")
        }

        static var reward: String {
            localized("work_contract_reward")
        }

        static var parties: String {
            localized("work_contract_parties")
        }

        // MARK: - Taking from the board, and the contract summary

        static var takeSheetTitle: String {
            localized("take_sheet_title")
        }

        static var loading: String {
            localized("work_contract_loading")
        }

        static func cancelFreeUntil(_ when: String) -> String {
            format("take_cancel_free_until", when)
        }

        static var cancelUnderWay: String {
            localized("take_cancel_under_way")
        }

        static var cancelStartPassed: String {
            localized("take_cancel_start_passed")
        }

        static func teamLine(_ crew: String) -> String {
            format("take_team_line", crew)
        }

        static var doneCannotCancel: String {
            localized("take_done_cannot_cancel")
        }

        static func cashLine(_ cash: String, _ reward: String) -> String {
            format("take_cash_line", cash, reward)
        }

        static func payUpdated(_ previous: String) -> String {
            format("take_pay_updated", previous)
        }

        static var doneTitle: String {
            localized("take_done_title")
        }

        static var backToJobs: String {
            localized("take_back_to_jobs")
        }

        static var summaryLead: String {
            localized("work_contract_summary_lead")
        }

        static var summaryLockout: String {
            localized("work_contract_summary_lockout")
        }

        static var summaryLateCancel: String {
            localized("work_contract_summary_late_cancel")
        }

        static var summaryComplaint: String {
            localized("work_contract_summary_complaint")
        }

        static var summaryEquipment: String {
            localized("work_contract_summary_equipment")
        }
    }
}
