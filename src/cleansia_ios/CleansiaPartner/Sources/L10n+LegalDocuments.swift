import Foundation

extension L10n {
    enum LegalDocuments {
        static var title: String {
            localized("legal_documents_title")
        }

        static var intro: String {
            localized("legal_documents_intro")
        }

        static var empty: String {
            localized("legal_documents_empty")
        }

        static func version(_ version: String) -> String {
            format("legal_documents_version", version)
        }

        static func acceptedOn(_ date: String, _ version: String) -> String {
            format("legal_documents_accepted_on", date, version)
        }

        static func newVersion(_ acceptedVersion: String) -> String {
            format("legal_documents_new_version", acceptedVersion)
        }

        static var awaiting: String {
            localized("legal_documents_awaiting")
        }

        static var accept: String {
            localized("legal_documents_accept")
        }

        static var acceptedToast: String {
            localized("legal_documents_accepted_toast")
        }

        static var textUpdated: String {
            localized("legal_documents_text_updated")
        }

        static var review: String {
            localized("legal_documents_review")
        }

        static var profileSummary: String {
            localized("profile_legal_documents_summary")
        }
    }
}
