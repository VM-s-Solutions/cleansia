import Foundation

extension L10n {
    enum Profile {
        static var groupAccount: String {
            localized("profile_group_account")
        }

        static var groupWorkLegal: String {
            localized("profile_group_work_legal")
        }

        static var groupPreferences: String {
            localized("profile_group_preferences")
        }

        static var groupLegal: String {
            localized("profile_group_legal")
        }

        static var terms: String {
            localized("profile_terms")
        }

        static var termsSummary: String {
            localized("profile_terms_summary")
        }

        static var privacy: String {
            localized("profile_privacy")
        }

        static var privacySummary: String {
            localized("profile_privacy_summary")
        }

        static var language: String {
            localized("language")
        }

        static var languageSystem: String {
            localized("language_system")
        }

        static var theme: String {
            localized("theme")
        }

        static var themeSystem: String {
            localized("theme_system")
        }

        static var themeLight: String {
            localized("theme_light")
        }

        static var themeDark: String {
            localized("theme_dark")
        }

        static var devicesSummary: String {
            localized("profile_devices_summary")
        }

        static var personal: String {
            localized("personal")
        }

        static var address: String {
            localized("address")
        }

        static var emergencyContact: String {
            localized("emergency_contact")
        }

        static var identification: String {
            localized("identification_title")
        }

        static var bankDetails: String {
            localized("bank_details")
        }

        static var myDocuments: String {
            localized("my_documents")
        }

        static var documentsSummary: String {
            localized("documents_summary_view")
        }

        static var noData: String {
            localized("no_data")
        }

        /// The avatar picker says the same six things the order-photo rail says, so it reads the same
        /// six catalogue keys rather than a parallel `profile_photo_*` set nobody would keep in step.
        /// Only the two confirmations are its own — the rail has nothing to confirm.
        static var photoAdd: String {
            localized("add_photo")
        }

        static var photoTake: String {
            localized("take_photo")
        }

        static var photoLibrary: String {
            localized("choose_from_library")
        }

        static var photoRemove: String {
            localized("delete_photo")
        }

        static var photoUnreadable: String {
            localized("photo_encode_failed")
        }

        static var photoUploadSuccess: String {
            localized("profile_photo_upload_success")
        }

        static var photoRemoveSuccess: String {
            localized("profile_photo_remove_success")
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

        static var firstName: String {
            localized("profile_first_name")
        }

        static var lastName: String {
            localized("profile_last_name")
        }

        static var phone: String {
            localized("profile_phone")
        }

        static var email: String {
            localized("email")
        }

        static var emailReadonlyHelper: String {
            localized("email_readonly_helper")
        }

        static var street: String {
            localized("profile_street")
        }

        static var city: String {
            localized("profile_city")
        }

        static var zipCode: String {
            localized("profile_zip_code")
        }

        static var country: String {
            localized("profile_country")
        }

        static var nationality: String {
            localized("profile_nationality")
        }

        static var passport: String {
            localized("profile_passport")
        }

        static var businessCountry: String {
            localized("profile_business_country")
        }

        static var registrationNumber: String {
            localized("profile_registration_number")
        }

        static var vatNumber: String {
            localized("profile_vat_number")
        }

        static var legalEntityName: String {
            localized("profile_legal_entity_name")
        }

        static var entityType: String {
            localized("profile_entity_type")
        }

        static var entityTypeNatural: String {
            localized("profile_entity_type_natural")
        }

        static var entityTypeLegal: String {
            localized("profile_entity_type_legal")
        }

        static var iban: String {
            localized("profile_iban")
        }

        static var ibanHelper: String {
            localized("iban_helper")
        }

        static var bankCountry: String {
            localized("bank_country")
        }

        static var bankAccount: String {
            localized("bank_account")
        }

        static var bankAccountHelper: String {
            localized("bank_account_helper")
        }

        static var bankAccountPrefix: String {
            localized("bank_account_prefix")
        }

        static var bankAccountPrefixHelper: String {
            localized("bank_account_prefix_helper")
        }

        /// Short in-field hints, deliberately not the `*_prefix` / `*_number` / `bank_code` labels: those
        /// are full sentences-worth ("Account prefix", "Номер рахунку") and the segments are narrow.
        static var bankAccountPrefixPlaceholder: String {
            localized("bank_account_prefix_placeholder")
        }

        static var bankAccountNumberPlaceholder: String {
            localized("bank_account_number_placeholder")
        }

        static var bankCodePlaceholder: String {
            localized("bank_code_placeholder")
        }

        static var accountNumber: String {
            localized("account_number")
        }

        static var bankAccountNumberHelper: String {
            localized("bank_account_number_helper")
        }

        static var bankCode: String {
            localized("bank_code")
        }

        static var bankCodeHelper: String {
            localized("bank_code_helper")
        }

        static var swiftCode: String {
            localized("swift_code")
        }

        static var swiftCodeHelper: String {
            localized("swift_code_helper")
        }

        static var bankName: String {
            localized("bank_name")
        }

        static var bankAccountHolder: String {
            localized("bank_account_holder")
        }

        static var bankAccountHolderHelper: String {
            localized("bank_account_holder_helper")
        }

        static var emergencyName: String {
            localized("profile_emergency_name")
        }

        static var emergencyPhone: String {
            localized("profile_emergency_phone")
        }

        static var save: String {
            localized("save")
        }

        static var saveAndContinue: String {
            localized("save_and_continue")
        }

        static var addressPickOnMap: String {
            localized("address_pick_on_map")
        }

        static var addressPickOnMapHelper: String {
            localized("address_pick_on_map_helper")
        }

        static var addressWhyTitle: String {
            localized("address_why_title")
        }

        static var addressWhyReasonJobs: String {
            localized("address_why_reason_jobs")
        }

        static var addressWhyReasonDistancePay: String {
            localized("address_why_reason_distance_pay")
        }

        static var addressWhyReasonInvoice: String {
            localized("address_why_reason_invoice")
        }

        static var addressWhyPrivacy: String {
            localized("address_why_privacy")
        }

        static var documentsEmpty: String {
            localized("documents_empty")
        }

        static var documentsDelete: String {
            localized("documents_delete")
        }

        /// What the cleaner's country asks for, whether or not any of it is uploaded. The screen
        /// used to open on an empty box that named nothing, so the first step of onboarding was
        /// contacting support to ask which papers we wanted.
        static var documentRequirementsTitle: String {
            localized("document_requirements_title")
        }

        static var documentRequirementsSubtitle: String {
            localized("document_requirements_subtitle")
        }

        static var documentRequirementRequired: String {
            localized("document_requirement_required")
        }

        static var documentRequirementOptional: String {
            localized("document_requirement_optional")
        }

        static var documentRequirementMissing: String {
            localized("document_requirement_missing")
        }

        /// The door that needs no admin: replacing never empties the slot, so the registration
        /// lock never re-engages.
        static var documentReplace: String {
            localized("document_replace")
        }

        static var documentReplaceTitle: String {
            localized("document_replace_title")
        }

        static func documentReplaceMessage(_ fileName: String) -> String {
            format("document_replace_message", fileName)
        }

        /// The other door: nothing should be there at all, and that one an employer has to agree
        /// with. The request changes nothing until an admin answers it.
        static var documentRequestDeletion: String {
            localized("document_request_deletion")
        }

        static var documentRequestDeletionTitle: String {
            localized("document_request_deletion_title")
        }

        static var documentRequestDeletionMessage: String {
            localized("document_request_deletion_message")
        }

        static var documentDeletionReason: String {
            localized("document_deletion_reason")
        }

        static var documentDeletionRequested: String {
            localized("document_deletion_requested")
        }

        static var uploadDocument: String {
            localized("upload_document")
        }

        static var documentType: String {
            localized("document_type")
        }

        static var descriptionOptional: String {
            localized("description_optional")
        }

        static var documentTooLarge: String {
            localized("document_too_large")
        }

        static var documentStatusPending: String {
            localized("document_status_pending")
        }

        static var documentStatusApproved: String {
            localized("document_status_approved")
        }

        static var documentStatusRejected: String {
            localized("document_status_rejected")
        }

        static var documentTypeIdentity: String {
            localized("document_type_identity")
        }

        static var documentTypePassport: String {
            localized("document_type_passport")
        }

        static var documentTypeDriversLicense: String {
            localized("document_type_drivers_license")
        }

        static var documentTypeWorkPermit: String {
            localized("document_type_work_permit")
        }

        static var documentTypeContract: String {
            localized("document_type_contract")
        }

        static var documentTypeCertificate: String {
            localized("document_type_certificate")
        }

        static var documentTypeBankStatement: String {
            localized("document_type_bank_statement")
        }

        static var documentTypeTax: String {
            localized("document_type_tax")
        }

        static var documentTypeInsurance: String {
            localized("document_type_insurance")
        }

        static var documentTypeOther: String {
            localized("document_type_other")
        }

        static var errorGeneric: String {
            localized("error_generic")
        }

        static var logout: String {
            localized("logout")
        }

        static var logoutDialogTitle: String {
            localized("profile_logout_dialog_title")
        }

        static var logoutDialogMessage: String {
            localized("profile_logout_dialog_message")
        }

        static var logoutDialogConfirm: String {
            localized("profile_logout_dialog_confirm")
        }

        static var logoutDialogCancel: String {
            localized("profile_logout_dialog_cancel")
        }

        static var contractStatusPending: String {
            localized("contract_status_pending")
        }

        static var contractStatusActive: String {
            localized("contract_status_active")
        }

        static var contractStatusApproved: String {
            localized("contract_status_approved")
        }

        static var contractStatusTerminated: String {
            localized("contract_status_terminated")
        }

        static var contractStatusRejected: String {
            localized("contract_status_rejected")
        }

        static var onboardingHeaderSubtitle: String {
            localized("onboarding_header_subtitle")
        }

        static func onboardingStepProgress(_ step: Int, _ total: Int) -> String {
            format("onboarding_step_progress", step, total)
        }

        static var onboardingStepPersonal: String {
            localized("onboarding_step_personal")
        }

        static var onboardingStepAddress: String {
            localized("onboarding_step_address")
        }

        static var onboardingStepIdentification: String {
            localized("onboarding_step_identification")
        }

        static var onboardingStepBank: String {
            localized("onboarding_step_bank")
        }

        static var birthDate: String {
            localized("birth_date")
        }

        static var birthDatePlaceholder: String {
            localized("birth_date_placeholder")
        }

        static var errorFirstNameRequired: String {
            localized("profile_error_first_name_required")
        }

        static var errorLastNameRequired: String {
            localized("profile_error_last_name_required")
        }

        static var errorBirthDateRequired: String {
            localized("profile_error_birth_date_required")
        }

        static var errorProfileNotLoaded: String {
            localized("profile_error_not_loaded")
        }

        static var errorEmergencyNameRequired: String {
            localized("profile_error_emergency_name_required")
        }

        static var errorEmergencyPhoneRequired: String {
            localized("profile_error_emergency_phone_required")
        }

        static var errorNationalityRequired: String {
            localized("profile_error_nationality_required")
        }

        static var errorPassportRequired: String {
            localized("profile_error_passport_required")
        }

        static var errorBusinessCountryRequired: String {
            localized("profile_error_business_country_required")
        }

        static var errorRegistrationNumberRequired: String {
            localized("profile_error_registration_number_required")
        }

        static var errorLegalEntityNameRequired: String {
            localized("profile_error_legal_entity_name_required")
        }

        static var errorAddressNotPicked: String {
            localized("profile_error_address_not_picked")
        }

        static var errorCountryNotServiced: String {
            localized("profile_error_country_not_serviced")
        }
    }
}
