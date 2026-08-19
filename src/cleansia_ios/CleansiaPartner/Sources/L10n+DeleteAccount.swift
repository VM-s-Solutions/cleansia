import Foundation

extension L10n {
    /// Copy for the account-deletion REQUEST. → /decisions/adr-0052
    ///
    /// Named the same as the customer app's namespace but the wording is not shared and must not be:
    /// a customer is deleted on the spot, a cleaner files a request that an admin fulfils after the
    /// paperwork. Copying the customer strings across would reintroduce the exact lie this screen
    /// exists to stop telling.
    enum DeleteAccount {
        static var rowTitle: String { localized("delete_account_row_title") }
        static var rowSummary: String { localized("delete_account_row_summary") }

        static var title: String { localized("delete_account_title") }
        static var headline: String { localized("delete_account_headline") }
        static var body: String { localized("delete_account_body") }

        static var requestedHeadline: String { localized("delete_account_requested_headline") }
        static var requestedBody: String { localized("delete_account_requested_body") }
        static var requestedSnackbar: String { localized("delete_account_requested_snackbar") }

        /// The list both stores require: what survives a deletion. For a cleaner that is the
        /// financial record, which is not theirs to remove.
        static var keptLabel: String { localized("delete_account_kept_label") }
        static var keptInvoices: String { localized("delete_account_kept_invoices") }
        static var keptPay: String { localized("delete_account_kept_pay") }
        static var keptAgreement: String { localized("delete_account_kept_agreement") }

        static var cta: String { localized("delete_account_cta") }
        static var dialogTitle: String { localized("delete_account_dialog_title") }
        static var dialogMessage: String { localized("delete_account_dialog_message") }
        static var dialogConfirm: String { localized("delete_account_dialog_confirm") }

        static var errorBlockedByAssignedOrder: String {
            localized("delete_account_error_blocked_by_assigned_order")
        }

        static var errorBlockedByUnsettledPay: String {
            localized("delete_account_error_blocked_by_unsettled_pay")
        }

        static var errorBlockedByInvoice: String { localized("delete_account_error_blocked_by_invoice") }
        static var errorBlockedByOrder: String { localized("delete_account_error_blocked_by_order") }
        static var errorAlreadyPending: String { localized("delete_account_error_already_pending") }
    }
}
