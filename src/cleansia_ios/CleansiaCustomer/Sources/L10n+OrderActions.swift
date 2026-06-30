import Foundation

extension L10n {
    enum OrderCancel {
        static var title: String {
            localized("order_cancel_title")
        }

        static var keep: String {
            localized("order_cancel_keep")
        }

        static var confirm: String {
            localized("order_cancel_confirm")
        }

        static var reasonPickerLabel: String {
            localized("order_cancel_reason_picker_label")
        }

        static var reasonSchedule: String {
            localized("order_cancel_reason_schedule")
        }

        static var reasonMistake: String {
            localized("order_cancel_reason_mistake")
        }

        static var reasonPrice: String {
            localized("order_cancel_reason_price")
        }

        static var reasonAlternative: String {
            localized("order_cancel_reason_alternative")
        }

        static var reasonNotNeeded: String {
            localized("order_cancel_reason_not_needed")
        }

        static var reasonOther: String {
            localized("order_cancel_reason_other")
        }

        static var notesRequiredLabel: String {
            localized("order_cancel_notes_required_label")
        }

        static var notesOptionalLabel: String {
            localized("order_cancel_notes_optional_label")
        }

        static var notesOtherPlaceholder: String {
            localized("order_cancel_notes_other_placeholder")
        }

        static var notesExtraPlaceholder: String {
            localized("order_cancel_notes_extra_placeholder")
        }

        static var feeNeutral: String {
            localized("order_cancel_fee_neutral")
        }

        static var feeEstimateNote: String {
            localized("order_cancel_fee_estimate_note")
        }

        static var feeOops: String {
            localized("order_cancel_fee_oops")
        }

        static var feeFree: String {
            localized("order_cancel_fee_free")
        }

        static func fee50(_ refund: String) -> String {
            format("order_cancel_fee_50", refund)
        }

        static var fee100: String {
            localized("order_cancel_fee_100")
        }

        static var retryHint: String {
            localized("order_cancel_retry_hint")
        }

        static var successNoRefund: String {
            localized("order_cancel_success_no_refund")
        }

        static func successWithRefund(_ refund: String) -> String {
            format("order_cancel_success_with_refund", refund)
        }
    }

    enum OrderReview {
        static var sheetTitle: String {
            localized("order_review_sheet_title")
        }

        static var editTitle: String {
            localized("order_review_edit_title")
        }

        static var commentLabel: String {
            localized("order_review_comment_label")
        }

        static var commentPlaceholder: String {
            localized("order_review_comment_placeholder")
        }

        static var cancel: String {
            localized("order_review_cancel")
        }

        static var submit: String {
            localized("order_review_submit")
        }

        static var save: String {
            localized("order_review_save")
        }

        static func starContentDesc(_ star: Int) -> String {
            format("order_review_star_content_desc", star)
        }

        static func ratingDescription(_ rating: Int) -> String {
            switch rating {
            case 1: localized("order_review_rating_1")
            case 2: localized("order_review_rating_2")
            case 3: localized("order_review_rating_3")
            case 4: localized("order_review_rating_4")
            case 5: localized("order_review_rating_5")
            default: localized("order_review_rating_hint")
            }
        }

        static var success: String {
            localized("order_review_success")
        }

        static var updated: String {
            localized("order_review_updated")
        }

        static var retryHint: String {
            localized("order_review_retry_hint")
        }
    }

    enum OrderPhotos {
        static var sectionTitle: String {
            localized("order_photos_section_title")
        }

        static var viewButton: String {
            localized("order_photos_view_button")
        }

        static func summaryBefore(_ count: Int) -> String {
            format("order_photos_summary_before", count)
        }

        static func summaryAfter(_ count: Int) -> String {
            format("order_photos_summary_after", count)
        }
    }

    enum OrderReceipt {
        static var noViewer: String {
            localized("order_receipt_no_viewer")
        }

        static var openError: String {
            localized("order_receipt_open_error")
        }
    }
}
