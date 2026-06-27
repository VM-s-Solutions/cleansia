import Foundation

extension L10n {
    enum Profile {
        static var title: String {
            localized("profile")
        }

        static var groupAccount: String {
            localized("profile_group_account")
        }

        static var groupWorkLegal: String {
            localized("profile_group_work_legal")
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

        static var uploadDocument: String {
            localized("upload_document")
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

        static var errorFirstNameRequired: String {
            localized("profile_error_first_name_required")
        }

        static var errorLastNameRequired: String {
            localized("profile_error_last_name_required")
        }

        static var errorProfileNotLoaded: String {
            localized("profile_error_not_loaded")
        }

        static var errorIbanRequired: String {
            localized("profile_error_iban_required")
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
