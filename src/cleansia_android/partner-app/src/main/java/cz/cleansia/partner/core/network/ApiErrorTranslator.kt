package cz.cleansia.partner.core.network

import android.content.Context
import cz.cleansia.partner.R
import dagger.hilt.android.qualifiers.ApplicationContext
import javax.inject.Inject
import javax.inject.Singleton

/**
 * Translates backend API error keys (e.g. "user.not_existing_email") to localized strings.
 * Mirrors the Angular frontend pattern: translate.instant(`api.${errorKey}`)
 *
 * Backend sends error keys via ProblemDetails `errors` map. The first error value
 * is the translation key. This class maps those keys to Android string resources.
 */
@Singleton
class ApiErrorTranslator @Inject constructor(
    @ApplicationContext private val context: Context
) {
    /**
     * Maps a backend error key to a localized string.
     * Returns null if the key is not recognized.
     */
    fun translate(errorKey: String): String? {
        val resId = errorKeyToResId[errorKey] ?: return null
        return context.getString(resId)
    }

    /**
     * Translates an ApiError to a user-friendly localized message.
     * For BadRequest errors with an errorKey, it tries to translate the key first.
     * Falls back to the raw message if no translation is found.
     */
    fun translateError(error: ApiError): String {
        return when (error) {
            is ApiError.BadRequest -> {
                // Try to translate the error key first
                error.errorKey?.let { translate(it) }
                    ?: error.message
            }
            is ApiError.Network -> context.getString(R.string.error_network)
            is ApiError.Unauthorized -> context.getString(R.string.error_unauthorized)
            is ApiError.NotFound -> error.message
            is ApiError.Server -> context.getString(R.string.error_server)
            is ApiError.Unknown -> context.getString(R.string.error_unknown)
        }
    }

    companion object {
        /**
         * Mapping of backend error keys to Android string resource IDs.
         * Keys match the constants in BusinessErrorMessage.cs on the backend.
         */
        private val errorKeyToResId: Map<String, Int> = mapOf(
            // Auth
            "auth.google_type_error" to R.string.api_error_auth_google_type_error,
            "auth.internal_type_error" to R.string.api_error_auth_internal_type_error,
            "auth.invalid_confirmation_code" to R.string.api_error_auth_invalid_confirmation_code,
            "auth.invalid_google_token" to R.string.api_error_auth_invalid_google_token,
            "auth.invalid_password_format" to R.string.api_error_auth_invalid_password_format,
            "auth.invalid_reset_token" to R.string.api_error_auth_invalid_reset_token,
            "auth.same_reset_password" to R.string.api_error_auth_same_reset_password,
            "auth.insufficient_privileges" to R.string.api_error_auth_insufficient_privileges,

            // Common
            "common.invalid_enum_value" to R.string.api_error_common_invalid_enum_value,
            "common.max_length" to R.string.api_error_common_max_length,
            "common.required" to R.string.api_error_common_required,

            // Email
            "email.sending_failed" to R.string.api_error_email_sending_failed,
            "email.invalid_format" to R.string.api_error_email_invalid_format,
            "email.invalid_email" to R.string.api_error_email_invalid_email,

            // Order
            "order.cleaning_date.future" to R.string.api_error_order_cleaning_date_future,
            "order.empty" to R.string.api_error_order_empty,
            "order.not_found" to R.string.api_error_order_not_found,
            "order.already_assigned" to R.string.api_error_order_already_assigned,
            "order.no_available_spots" to R.string.api_error_order_no_available_spots,
            "order.employee_already_assigned" to R.string.api_error_order_employee_already_assigned,
            "order.employee_not_assigned" to R.string.api_error_order_employee_not_assigned,
            "order.max_employees_exceeded" to R.string.api_error_order_max_employees_exceeded,
            "order.not_in_progress" to R.string.api_error_order_not_in_progress,
            "order.not_confirmed" to R.string.api_error_order_not_confirmed,
            "order.employee_already_has_order_in_progress" to R.string.api_error_order_employee_already_has_order_in_progress,
            "order.note.content_required" to R.string.api_error_order_note_content_required,
            "order.issue.description_required" to R.string.api_error_order_issue_description_required,

            // User
            "user.email_confirmed" to R.string.api_error_user_email_confirmed,
            "user.existing_email" to R.string.api_error_user_existing_email,
            "user.not_existing_email" to R.string.api_error_user_not_existing_email,
            "user.not_existing_id" to R.string.api_error_user_not_existing_id,
            "user.not_allowed_to_update" to R.string.api_error_user_not_allowed_to_update,
            "user.existing_phone_number" to R.string.api_error_user_existing_phone_number,
            "user.not_found" to R.string.api_error_user_not_found,

            // Employee
            "employee.not_found" to R.string.api_error_employee_not_found,
            "employee.not_existing_email" to R.string.api_error_employee_not_existing_email,
            "employee.not_allowed_to_update" to R.string.api_error_employee_not_allowed_to_update,
            "employee.profile_incomplete" to R.string.api_error_employee_profile_incomplete,
            "employee.documents_missing" to R.string.api_error_employee_documents_missing,

            // Employee Documents
            "employee_document.not_found" to R.string.api_error_employee_document_not_found,
            "employee_document.unauthorized" to R.string.api_error_employee_document_unauthorized,
            "employee_document.not_owned" to R.string.api_error_employee_document_not_owned,

            // Validation
            "validation.invalid_password" to R.string.api_error_validation_invalid_password,
            "validation.invalid_date" to R.string.api_error_validation_invalid_date,
            "validation.date_must_be_in_past" to R.string.api_error_validation_date_must_be_in_past,
            "validation.invalid_age" to R.string.api_error_validation_invalid_age,
            "validation.invalid_phone_number" to R.string.api_error_validation_invalid_phone_number,
            "validation.invalid_national_id" to R.string.api_error_validation_invalid_national_id,
            "validation.invalid_tax_id" to R.string.api_error_validation_invalid_tax_id,
            "validation.invalid_iban" to R.string.api_error_validation_invalid_iban,
            "validation.invalid_zip_code" to R.string.api_error_validation_invalid_zip_code,
            "validation.must_be_positive" to R.string.api_error_validation_must_be_positive,
            "validation.invalid_availability_format" to R.string.api_error_validation_invalid_availability_format,
            "validation.page_must_be_positive" to R.string.api_error_validation_page_must_be_positive,
            "validation.page_size_must_be_positive" to R.string.api_error_validation_page_size_must_be_positive,
            "validation.page_size_exceeded" to R.string.api_error_validation_page_size_exceeded,
            "validation.invalid_contract_status" to R.string.api_error_validation_invalid_contract_status,

            // File
            "file.content_type_doesnt_match" to R.string.api_error_file_content_type_doesnt_match,
            "file.invalid_file_type" to R.string.api_error_file_invalid_file_type,
            "file.size_exceeded" to R.string.api_error_file_size_exceeded,
            "file.size_exceeded_10mb" to R.string.api_error_file_size_exceeded_10mb,
            "file.type_not_allowed" to R.string.api_error_file_type_not_allowed,
            "file.required" to R.string.api_error_file_required,

            // General
            "general.not_found" to R.string.api_error_general_not_found,

            // Device
            "device.invalid_platform" to R.string.api_error_device_invalid_platform,

            // Country
            "country.not_existing_id" to R.string.api_error_country_not_existing_id,

            // Address
            "address.invalid_length" to R.string.api_error_address_invalid_length,
        )
    }
}
