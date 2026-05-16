package cz.cleansia.partner.features.profile.viewmodels

import cz.cleansia.partner.domain.models.profile.EmployeeProfile

enum class ProfileSection {
    PERSONAL, IDENTIFICATION, ADDRESS, BANK, EMERGENCY
}

/**
 * Form state for editing profile
 */
data class ProfileEditFormState(
    val firstName: String = "",
    val lastName: String = "",
    val email: String = "",
    val phoneNumber: String = "",
    val dateOfBirth: String = "",
    // Identification
    val nationalityId: String = "",
    val passportId: String = "",
    val taxId: String = "",
    // Address
    val street: String = "",
    val city: String = "",
    val zipCode: String = "",
    val state: String = "",
    val countryId: String = "",
    // Bank
    val iban: String = "",
    // Emergency contact
    val emergencyContactName: String = "",
    val emergencyContactPhone: String = ""
) {
    companion object {
        fun fromProfile(profile: EmployeeProfile?): ProfileEditFormState {
            return profile?.let {
                ProfileEditFormState(
                    firstName = it.firstName ?: "",
                    lastName = it.lastName ?: "",
                    email = it.email,
                    phoneNumber = it.phoneNumber ?: "",
                    dateOfBirth = it.dateOfBirth ?: "",
                    nationalityId = it.nationalityId ?: "",
                    passportId = it.passportId ?: "",
                    taxId = it.taxId ?: "",
                    street = it.street ?: "",
                    city = it.city ?: "",
                    zipCode = it.zipCode ?: "",
                    state = it.state ?: "",
                    countryId = it.countryId ?: "",
                    iban = it.iban ?: "",
                    emergencyContactName = it.emergencyContactName ?: "",
                    emergencyContactPhone = it.emergencyContactPhone ?: ""
                )
            } ?: ProfileEditFormState()
        }
    }
}
