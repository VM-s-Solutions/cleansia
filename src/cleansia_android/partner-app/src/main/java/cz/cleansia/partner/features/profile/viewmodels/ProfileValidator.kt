package cz.cleansia.partner.features.profile.viewmodels

import java.time.LocalDate
import java.time.format.DateTimeFormatter
import java.time.temporal.ChronoUnit

/**
 * Profile form validation matching backend rules
 */
object ProfileValidator {
    private val ZIP_CODE_REGEX = Regex("^[0-9A-Za-z\\s-]+$")
    private val PHONE_REGEX = Regex("^\\+?[0-9]{10,15}$") // Full phone with optional + prefix
    private val PASSPORT_REGEX = Regex("^[0-9A-Za-z]+$")
    private val IBAN_REGEX = Regex("^[A-Z]{2}[0-9]{2}[A-Z0-9]{4}[0-9]{7}([A-Z0-9]?){0,16}$")
    private val EMAIL_REGEX = Regex("^[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\\.[A-Za-z]{2,}$")
    private val TAX_ID_REGEX = Regex("^[0-9A-Za-z]+$")

    fun validateSection(section: ProfileSection, form: ProfileEditFormState): Map<String, String> {
        val errors = mutableMapOf<String, String>()
        when (section) {
            ProfileSection.PERSONAL -> {
                if (form.firstName.isBlank()) errors["firstName"] = "validation_required"
                else if (form.firstName.length > 50) errors["firstName"] = "validation_max_50"

                if (form.lastName.isBlank()) errors["lastName"] = "validation_required"
                else if (form.lastName.length > 50) errors["lastName"] = "validation_max_50"

                if (form.email.isBlank()) errors["email"] = "validation_required"
                else if (!EMAIL_REGEX.matches(form.email)) errors["email"] = "validation_invalid_email"

                if (form.dateOfBirth.isBlank()) errors["dateOfBirth"] = "validation_required"
                else {
                    try {
                        val dob = LocalDate.parse(form.dateOfBirth, DateTimeFormatter.ISO_LOCAL_DATE)
                        val age = ChronoUnit.YEARS.between(dob, LocalDate.now())
                        if (dob.isAfter(LocalDate.now())) errors["dateOfBirth"] = "validation_date_past"
                        else if (age < 18) errors["dateOfBirth"] = "validation_min_age_18"
                        else if (age > 120) errors["dateOfBirth"] = "validation_max_age_120"
                    } catch (_: Exception) {
                        errors["dateOfBirth"] = "validation_invalid_date"
                    }
                }

                if (form.phoneNumber.isBlank()) errors["phoneNumber"] = "validation_required"
                else if (!PHONE_REGEX.matches(form.phoneNumber)) errors["phoneNumber"] = "validation_invalid_phone"
            }
            ProfileSection.IDENTIFICATION -> {
                if (form.nationalityId.isBlank()) errors["nationalityId"] = "validation_required"

                if (form.passportId.isBlank()) errors["passportId"] = "validation_required"
                else if (form.passportId.length < 5 || form.passportId.length > 20) errors["passportId"] = "validation_length_5_20"
                else if (!PASSPORT_REGEX.matches(form.passportId)) errors["passportId"] = "validation_alphanumeric"

                if (form.taxId.isNotBlank()) {
                    if (form.taxId.length > 20) errors["taxId"] = "validation_max_20"
                    else if (!TAX_ID_REGEX.matches(form.taxId)) errors["taxId"] = "validation_alphanumeric"
                }
            }
            ProfileSection.ADDRESS -> {
                if (form.street.isBlank()) errors["street"] = "validation_required"
                else if (form.street.length < 5 || form.street.length > 255) errors["street"] = "validation_length_5_255"

                if (form.city.isBlank()) errors["city"] = "validation_required"
                else if (form.city.length < 2 || form.city.length > 100) errors["city"] = "validation_length_2_100"

                if (form.zipCode.isBlank()) errors["zipCode"] = "validation_required"
                else if (form.zipCode.length < 3 || form.zipCode.length > 20) errors["zipCode"] = "validation_length_3_20"
                else if (!ZIP_CODE_REGEX.matches(form.zipCode)) errors["zipCode"] = "validation_invalid_zip"

                if (form.countryId.isBlank()) errors["countryId"] = "validation_required"
            }
            ProfileSection.BANK -> {
                if (form.iban.isBlank()) errors["iban"] = "validation_required"
                else if (form.iban.length < 15 || form.iban.length > 34) errors["iban"] = "validation_length_15_34"
                else if (!IBAN_REGEX.matches(form.iban.uppercase().replace(" ", ""))) errors["iban"] = "validation_invalid_iban"
            }
            ProfileSection.EMERGENCY -> {
                if (form.emergencyContactName.isNotBlank() && form.emergencyContactName.length > 100) {
                    errors["emergencyContactName"] = "validation_max_100"
                }
            }
        }
        return errors
    }
}
