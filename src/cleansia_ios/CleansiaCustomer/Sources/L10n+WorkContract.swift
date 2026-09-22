import Foundation

extension L10n {
    enum WorkContract {
        static var title: String {
            localized("work_contract_title")
        }

        static func acceptedLine(_ name: String, _ date: String, _ version: String) -> String {
            format("work_contract_accepted_line", name, date, version)
        }

        static var read: String {
            localized("work_contract_read")
        }

        static func version(_ version: String) -> String {
            format("work_contract_version", version)
        }

        static var factsTitle: String {
            localized("work_contract_facts_title")
        }

        static var orderNumber: String {
            localized("work_contract_order_number")
        }

        static var window: String {
            localized("work_contract_window")
        }

        static var price: String {
            localized("work_contract_price")
        }

        static var location: String {
            localized("work_contract_location")
        }

        static func acceptedOn(_ date: String, _ version: String) -> String {
            format("work_contract_accepted_on", date, version)
        }

        static func acceptedInLanguage(_ language: String) -> String {
            format("work_contract_accepted_in_language", language)
        }

        static var loadError: String {
            localized("work_contract_load_error")
        }
    }
}
