package cz.cleansia.partner.core.database.entities

import androidx.room.Entity
import androidx.room.PrimaryKey
import cz.cleansia.partner.domain.models.profile.EmployeeProfile

/**
 * Room entity for caching the user's profile locally.
 * This allows the profile screen to display data when offline.
 */
@Entity(tableName = "cached_profile")
data class CachedProfile(
    @PrimaryKey
    val id: String,
    val userId: String?,
    val email: String,
    val firstName: String?,
    val lastName: String?,
    val phoneNumber: String?,
    val dateOfBirth: String?,
    val profileImageUrl: String?,
    val nationality: String?,
    val nationalId: String?,
    val passportId: String?,
    val taxId: String?,
    val street: String?,
    val city: String?,
    val zipCode: String?,
    val state: String?,
    val country: String?,
    val countryId: String?,
    val bankName: String?,
    val accountNumber: String?,
    val iban: String?,
    val swiftCode: String?,
    val emergencyContactName: String?,
    val emergencyContactRelationship: String?,
    val emergencyContactPhone: String?,
    val emergencyContactEmail: String?,
    val termsAccepted: Boolean,
    val termsAcceptedAt: String?,
    val isActive: Boolean,
    val isVerified: Boolean,
    val cachedAt: Long = System.currentTimeMillis()
) {
    /**
     * Convert cached entity back to domain model
     */
    fun toDomainModel(): EmployeeProfile {
        return EmployeeProfile(
            id = id,
            userId = userId,
            email = email,
            firstName = firstName,
            lastName = lastName,
            phoneNumber = phoneNumber,
            dateOfBirth = dateOfBirth,
            profileImageUrl = profileImageUrl,
            nationality = nationality,
            nationalId = nationalId,
            passportId = passportId,
            taxId = taxId,
            street = street,
            city = city,
            zipCode = zipCode,
            state = state,
            country = country,
            countryId = countryId,
            bankName = bankName,
            accountNumber = accountNumber,
            iban = iban,
            swiftCode = swiftCode,
            emergencyContactName = emergencyContactName,
            emergencyContactRelationship = emergencyContactRelationship,
            emergencyContactPhone = emergencyContactPhone,
            emergencyContactEmail = emergencyContactEmail,
            termsAccepted = termsAccepted,
            termsAcceptedAt = termsAcceptedAt,
            isActive = isActive,
            isVerified = isVerified
        )
    }

    companion object {
        /**
         * Create a cached entity from domain model
         */
        fun fromDomainModel(profile: EmployeeProfile): CachedProfile {
            return CachedProfile(
                id = profile.id,
                userId = profile.userId,
                email = profile.email,
                firstName = profile.firstName,
                lastName = profile.lastName,
                phoneNumber = profile.phoneNumber,
                dateOfBirth = profile.dateOfBirth,
                profileImageUrl = profile.profileImageUrl,
                nationality = profile.nationality,
                nationalId = profile.nationalId,
                passportId = profile.passportId,
                taxId = profile.taxId,
                street = profile.street,
                city = profile.city,
                zipCode = profile.zipCode,
                state = profile.state,
                country = profile.country,
                countryId = profile.countryId,
                bankName = profile.bankName,
                accountNumber = profile.accountNumber,
                iban = profile.iban,
                swiftCode = profile.swiftCode,
                emergencyContactName = profile.emergencyContactName,
                emergencyContactRelationship = profile.emergencyContactRelationship,
                emergencyContactPhone = profile.emergencyContactPhone,
                emergencyContactEmail = profile.emergencyContactEmail,
                termsAccepted = profile.termsAccepted,
                termsAcceptedAt = profile.termsAcceptedAt,
                isActive = profile.isActive,
                isVerified = profile.isVerified
            )
        }
    }
}
