import Foundation

extension L10n {
    /// The dashboard shortcuts row. Keys and copy are the Android partner app's, verbatim, so the two
    /// platforms read identically — `dash_quick_actions` and the four `dash_qa_*` labels.
    enum Shortcuts {
        static var sectionTitle: String {
            localized("dash_quick_actions")
        }

        static var profile: String {
            localized("dash_qa_profile")
        }

        static var payHistory: String {
            localized("dash_qa_pay_history")
        }

        static var documents: String {
            localized("dash_qa_documents")
        }

        static var help: String {
            localized("dash_qa_help")
        }
    }
}
