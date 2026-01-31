package cz.cleansia.partner.domain.models.profile

import kotlinx.serialization.Serializable

/**
 * Document type enum
 */
enum class DocumentType(val apiValue: String) {
    ID_CARD("IdCard"),
    PASSPORT("Passport"),
    DRIVING_LICENSE("DrivingLicense"),
    WORK_PERMIT("WorkPermit"),
    RESIDENCE_PERMIT("ResidencePermit"),
    TAX_DOCUMENT("TaxDocument"),
    OTHER("Other");

    companion object {
        fun fromApiValue(value: String): DocumentType =
            entries.find { it.apiValue.equals(value, ignoreCase = true) } ?: OTHER
    }
}

/**
 * Document status enum
 */
enum class DocumentStatus(val apiValue: String) {
    PENDING("Pending"),
    APPROVED("Approved"),
    REJECTED("Rejected"),
    EXPIRED("Expired");

    companion object {
        fun fromApiValue(value: String): DocumentStatus =
            entries.find { it.apiValue.equals(value, ignoreCase = true) } ?: PENDING
    }
}

@Serializable
data class EmployeeProfile(
    val id: String,
    val userId: String? = null,
    val email: String,
    val firstName: String? = null,
    val lastName: String? = null,
    val phoneNumber: String? = null,
    val dateOfBirth: String? = null,
    val profileImageUrl: String? = null,

    // Additional personal info
    val nationality: String? = null,
    val nationalId: String? = null,
    val passportId: String? = null,
    val taxId: String? = null,

    // Address
    val street: String? = null,
    val city: String? = null,
    val zipCode: String? = null,
    val country: String? = null,
    val countryId: String? = null,

    // Bank details
    val bankName: String? = null,
    val accountNumber: String? = null,
    val iban: String? = null,
    val swiftCode: String? = null,

    // Emergency contact
    val emergencyContactName: String? = null,
    val emergencyContactRelationship: String? = null,
    val emergencyContactPhone: String? = null,
    val emergencyContactEmail: String? = null,

    // Terms & Consent
    val termsAccepted: Boolean = false,
    val termsAcceptedAt: String? = null,

    // Status
    val isActive: Boolean = true,
    val isVerified: Boolean = false,

    val createdAt: String? = null,
    val updatedAt: String? = null
) {
    val fullName: String
        get() = listOfNotNull(firstName, lastName).joinToString(" ").ifEmpty { email }

    val fullAddress: String
        get() = listOfNotNull(street, city, zipCode, country)
            .filter { it.isNotBlank() }
            .joinToString(", ")

    val hasBankDetails: Boolean
        get() = !bankName.isNullOrBlank() || !accountNumber.isNullOrBlank() || !iban.isNullOrBlank()

    val hasEmergencyContact: Boolean
        get() = !emergencyContactName.isNullOrBlank() || !emergencyContactPhone.isNullOrBlank()
}

@Serializable
data class EmployeeDocument(
    val id: String,
    val employeeId: String,
    val type: String = "Other",
    val status: String = "Pending",
    val fileName: String? = null,
    val fileUrl: String? = null,
    val thumbnailUrl: String? = null,
    val mimeType: String? = null,
    val fileSize: Long? = null,
    val expiryDate: String? = null,
    val notes: String? = null,
    val uploadedAt: String? = null,
    val reviewedAt: String? = null,
    val reviewNotes: String? = null
) {
    val documentType: DocumentType get() = DocumentType.fromApiValue(type)
    val documentStatus: DocumentStatus get() = DocumentStatus.fromApiValue(status)
}

/**
 * A single time slot with start and end times
 */
@Serializable
data class TimeSlot(
    val startTime: String = "09:00", // HH:mm format
    val endTime: String = "17:00" // HH:mm format
)

/**
 * Availability schedule for a day (supports multiple time slots)
 */
@Serializable
data class DayAvailability(
    val dayOfWeek: Int, // 0 = Sunday, 1 = Monday, etc.
    val isAvailable: Boolean = false,
    val timeSlots: List<TimeSlot> = listOf(TimeSlot()),
    // Backward-compatible computed properties
    val startTime: String? = null, // HH:mm format (deprecated, use timeSlots)
    val endTime: String? = null // HH:mm format (deprecated, use timeSlots)
) {
    /** Get effective time slots (from timeSlots list, or fallback to legacy startTime/endTime) */
    fun effectiveTimeSlots(): List<TimeSlot> {
        return if (timeSlots.isNotEmpty()) timeSlots
        else if (startTime != null || endTime != null) listOf(TimeSlot(startTime ?: "09:00", endTime ?: "17:00"))
        else listOf(TimeSlot())
    }
}

/**
 * Weekly availability schedule
 */
@Serializable
data class WeeklyAvailability(
    val employeeId: String,
    val schedule: List<DayAvailability> = emptyList()
)

/**
 * Country model for dropdown selection
 */
@Serializable
data class Country(
    val id: String,
    val name: String,
    val code: String? = null,
    val phoneCode: String? = null
)

/**
 * Relationship types for emergency contact
 */
enum class RelationshipType(val displayName: String) {
    SPOUSE("Spouse"),
    PARENT("Parent"),
    SIBLING("Sibling"),
    CHILD("Child"),
    FRIEND("Friend"),
    RELATIVE("Relative"),
    OTHER("Other");

    companion object {
        fun fromDisplayName(name: String): RelationshipType =
            entries.find { it.displayName.equals(name, ignoreCase = true) } ?: OTHER
    }
}
