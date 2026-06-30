import Foundation

extension L10n {
    enum Disputes {
        static var listTitle: String {
            localized("dispute_list_title")
        }

        static var listEmptyTitle: String {
            localized("dispute_list_empty_title")
        }

        static var listEmptySubtitle: String {
            localized("dispute_list_empty_subtitle")
        }

        static var listEmptyCta: String {
            localized("dispute_list_empty_cta")
        }

        static var listErrorTitle: String {
            localized("dispute_list_error_title")
        }

        static var listErrorRetry: String {
            localized("dispute_list_error_retry")
        }

        static var listFabNew: String {
            localized("dispute_list_fab_new")
        }

        static var listFabNoOrder: String {
            localized("dispute_list_fab_no_order")
        }

        static var createTitle: String {
            localized("dispute_create_title")
        }

        static var createOrderLabel: String {
            localized("dispute_create_order_label")
        }

        static var createReasonLabel: String {
            localized("dispute_create_reason_label")
        }

        static var createDescriptionLabel: String {
            localized("dispute_create_description_label")
        }

        static var createDescriptionPlaceholder: String {
            localized("dispute_create_description_placeholder")
        }

        static func createCharCount(_ count: Int) -> String {
            format("dispute_create_char_count", count)
        }

        static var createSubmit: String {
            localized("dispute_create_submit")
        }

        static var createMissingOrder: String {
            localized("dispute_create_missing_order")
        }

        static var createRetryHint: String {
            localized("dispute_create_retry_hint")
        }

        static var detailTitle: String {
            localized("dispute_detail_title")
        }

        static var detailAuthorYou: String {
            localized("dispute_detail_author_you")
        }

        static var detailAuthorSupport: String {
            localized("dispute_detail_author_support")
        }

        static var detailClosedNote: String {
            localized("dispute_detail_closed_note")
        }

        static var detailMessagePlaceholder: String {
            localized("dispute_detail_message_placeholder")
        }

        static var detailMessageSend: String {
            localized("dispute_detail_message_send")
        }

        static var detailSendRetry: String {
            localized("dispute_create_retry_hint")
        }

        static var evidenceSectionTitle: String {
            localized("dispute_evidence_section_title")
        }

        static var evidenceAddButton: String {
            localized("dispute_evidence_add_button")
        }

        static var evidenceUploading: String {
            localized("dispute_evidence_uploading")
        }

        static var evidenceTooLarge: String {
            localized("dispute_evidence_too_large")
        }

        static var evidenceUnsupportedType: String {
            localized("dispute_evidence_unsupported_type")
        }

        static var evidenceOpenError: String {
            localized("dispute_evidence_open_error")
        }

        static func evidenceCaption(_ when: String) -> String {
            format("dispute_evidence_caption", when)
        }

        static var evidencePdfLabel: String {
            localized("dispute_evidence_pdf_label")
        }

        static var evidenceImageLabel: String {
            localized("dispute_evidence_image_label")
        }

        static var addEvidenceTakePhoto: String {
            localized("dispute_evidence_take_photo")
        }

        static var addEvidenceChooseImage: String {
            localized("dispute_evidence_choose_image")
        }

        static var addEvidenceChoosePdf: String {
            localized("dispute_evidence_choose_pdf")
        }

        static var cameraPermissionTitle: String {
            localized("camera_permission_title")
        }

        static var cameraPermissionMessage: String {
            localized("camera_permission_message")
        }

        static var openSettings: String {
            localized("open_settings")
        }

        static func reason(_ value: Int) -> String {
            switch value {
            case 1: localized("dispute_reason_quality_issue")
            case 2: localized("dispute_reason_service_not_provided")
            case 3: localized("dispute_reason_service_incomplete")
            case 4: localized("dispute_reason_damaged_property")
            case 5: localized("dispute_reason_unauthorized_charge")
            case 6: localized("dispute_reason_incorrect_amount")
            default: localized("dispute_reason_other")
            }
        }
    }
}
