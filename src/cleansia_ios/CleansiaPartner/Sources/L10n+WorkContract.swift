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

        static func acceptedLine(_ date: String, _ version: String) -> String {
            format("work_contract_accepted_line", date, version)
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
    }
}
