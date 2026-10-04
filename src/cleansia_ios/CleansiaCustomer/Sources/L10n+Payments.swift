import Foundation

extension L10n {
    enum Payments {
        static var profileRow: String {
            localized("profile_row_payments")
        }

        static var title: String {
            localized("payments_title")
        }

        static var dueTitle: String {
            localized("payments_due_title")
        }

        static var dueIntro: String {
            localized("payments_due_intro")
        }

        static func dueOrder(_ displayOrderNumber: String) -> String {
            format("payments_due_order", displayOrderNumber)
        }

        static var payAction: String {
            localized("payments_pay_action")
        }

        static func kind(_ kind: Receivable.Kind) -> String {
            switch kind {
            case .cancellationFee: localized("receivable_kind_cancellation_fee")
            case .lockout: localized("receivable_kind_lockout")
            case .unpaidCash: localized("receivable_kind_unpaid_cash")
            case .topUp: localized("receivable_kind_top_up")
            case .other: localized("receivable_kind_other")
            }
        }

        static var cardTitle: String {
            localized("payments_card_title")
        }

        static var cardIntro: String {
            localized("payments_card_intro")
        }

        static var cardEmpty: String {
            localized("payments_card_empty")
        }

        static func cardLabel(brand: String, last4: String) -> String {
            format("payments_card_label", brand, last4)
        }

        static func cardExpires(month: Int, year: Int, currencyCode: String) -> String {
            format("payments_card_expires", month, year % 100, currencyCode)
        }

        static var cardAddAction: String {
            localized("payments_card_add_action")
        }

        static var cardAddNote: String {
            localized("payments_card_add_note")
        }

        static var cardAdded: String {
            localized("payments_card_added")
        }

        static var cardAddCancelled: String {
            localized("payments_card_add_cancelled")
        }

        static var cardAddPending: String {
            localized("payments_card_add_pending")
        }

        static var cardRemoveAction: String {
            localized("payments_card_remove_action")
        }

        static var cardRemoveTitle: String {
            localized("payments_card_remove_title")
        }

        static var cardRemoveMessage: String {
            localized("payments_card_remove_message")
        }

        static var cardRemoveConfirm: String {
            localized("payments_card_remove_confirm")
        }

        static var cardRemoved: String {
            localized("payments_card_removed")
        }

        static var errorMessage: String {
            localized("payments_error_message")
        }
    }
}

extension L10n.Booking {
    static var cardGuaranteeTitle: String {
        L10n.localized("booking_card_guarantee_title")
    }

    /// The draft wording the server records as `SavedCard.ConsentTextVersionInForce`; a new wording gets
    /// a new key together with a new version on the server.
    static var cardGuaranteeConsent: String {
        L10n.localized("consent_card_guarantee_draft_2026_09_28")
    }

    static var saveCard: String {
        L10n.localized("booking_save_card")
    }
}
