package cz.cleansia.partner.domain.models.profile

import cz.cleansia.partner.R
import kotlinx.serialization.SerialName
import kotlinx.serialization.Serializable

/**
 * Document type enum
 */
enum class DocumentType(val apiValue: String, val apiNumericValue: Int) {
    ID_CARD("IdentityCard", 1),
    PASSPORT("Passport", 2),
    DRIVING_LICENSE("DriversLicense", 3),
    WORK_PERMIT("WorkPermit", 4),
    CONTRACT("Contract", 5),
    CERTIFICATE("Certificate", 6),
    BANK_STATEMENT("BankStatement", 7),
    TAX_DOCUMENT("TaxDocument", 8),
    INSURANCE_DOCUMENT("InsuranceDocument", 9),
    OTHER("Other", 10);

    companion object {
        fun fromApiValue(value: String): DocumentType =
            entries.find { it.apiValue.equals(value, ignoreCase = true) } ?: OTHER

        fun fromNumericValue(value: Int): DocumentType =
            entries.find { it.apiNumericValue == value } ?: OTHER
    }
}

/**
 * Document status enum
 */
enum class DocumentStatus(val apiValue: String, val apiNumericValue: Int) {
    PENDING("Pending", 1),
    APPROVED("Approved", 2),
    REJECTED("Rejected", 3),
    EXPIRED("Expired", 4);

    companion object {
        fun fromApiValue(value: String): DocumentStatus =
            entries.find { it.apiValue.equals(value, ignoreCase = true) } ?: PENDING

        fun fromNumericValue(value: Int): DocumentStatus =
            entries.find { it.apiNumericValue == value } ?: PENDING
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
    @SerialName("birthDate")
    val dateOfBirth: String? = null,
    val profileImageUrl: String? = null,

    // Additional personal info
    val nationality: String? = null,
    val nationalId: String? = null,
    val nationalityId: String? = null,
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
    val updatedAt: String? = null,

    // Availability (API format: day name → list of time ranges)
    val availability: Map<String, List<AvailabilityTimeRange>>? = null
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

    /** Returns field label resource IDs that are missing for the Personal Info section */
    val missingPersonalFields: List<Int>
        get() = buildList {
            if (firstName.isNullOrBlank()) add(R.string.first_name)
            if (lastName.isNullOrBlank()) add(R.string.last_name)
            if (phoneNumber.isNullOrBlank()) add(R.string.phone_number)
            if (dateOfBirth.isNullOrBlank()) add(R.string.date_of_birth)
        }

    /** Returns field label resource IDs that are missing for the Identification section */
    val missingIdentificationFields: List<Int>
        get() = buildList {
            if (nationalityId.isNullOrBlank()) add(R.string.nationality)
            if (passportId.isNullOrBlank()) add(R.string.passport_id)
        }

    /** Returns field label resource IDs that are missing for the Address section */
    val missingAddressFields: List<Int>
        get() = buildList {
            if (street.isNullOrBlank()) add(R.string.street)
            if (city.isNullOrBlank()) add(R.string.city)
            if (zipCode.isNullOrBlank()) add(R.string.zip_code)
            if (countryId.isNullOrBlank()) add(R.string.country)
        }

    /** Returns field label resource IDs that are missing for the Bank Details section */
    val missingBankFields: List<Int>
        get() = buildList {
            if (iban.isNullOrBlank()) add(R.string.iban)
        }

    /** Returns field label resource IDs that are missing for the Emergency Contact section */
    val missingEmergencyFields: List<Int>
        get() = buildList {
            if (emergencyContactName.isNullOrBlank()) add(R.string.contact_name)
            if (emergencyContactPhone.isNullOrBlank()) add(R.string.contact_phone)
        }
}

@Serializable
data class EmployeeDocument(
    @SerialName("documentId")
    val id: String,
    val employeeId: String? = null,
    val fileName: String? = null,
    @SerialName("blobUrl")
    val fileUrl: String? = null,
    @SerialName("contentType")
    val mimeType: String? = null,
    @SerialName("fileSizeBytes")
    val fileSize: Long? = null,
    @SerialName("documentType")
    val documentTypeValue: Int = 10,
    @SerialName("status")
    val statusValue: Int = 0,
    val uploadedAt: String? = null,
    val reviewNotes: String? = null,
    val version: Int? = null,
    val description: String? = null
) {
    val documentType: DocumentType get() = DocumentType.fromNumericValue(documentTypeValue)
    val documentStatus: DocumentStatus get() = DocumentStatus.fromNumericValue(statusValue)
}

/**
 * Wrapper for GetMyDocuments response
 */
@Serializable
data class GetMyDocumentsResponse(
    val documents: List<EmployeeDocument> = emptyList()
)

/**
 * Request model for SaveMyDocuments (JSON with base64 files)
 */
@Serializable
data class SaveMyDocumentsRequest(
    val documents: List<DocumentToSave>
)

@Serializable
data class DocumentToSave(
    val documentType: Int,
    val file: BlobFileDto,
    val description: String? = null
)

@Serializable
data class BlobFileDto(
    val fileName: String,
    val base64Content: String,
    val contentType: String? = null
)

/**
 * Response from SaveMyDocuments
 */
@Serializable
data class SaveMyDocumentsResponse(
    val documents: List<SavedDocumentDto> = emptyList()
)

@Serializable
data class SavedDocumentDto(
    val documentId: String,
    val fileName: String,
    val blobUrl: String,
    val documentType: Int,
    val version: Int,
    val uploadedAt: String
)

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
 * Date-specific availability override.
 * Overrides the regular weekly schedule for a specific date.
 */
@Serializable
data class DateOverride(
    val date: String, // yyyy-MM-dd format
    val isAvailable: Boolean = false,
    val timeSlots: List<TimeSlot> = listOf(TimeSlot()),
    val note: String? = null
)

/**
 * Weekly availability schedule
 */
@Serializable
data class WeeklyAvailability(
    val employeeId: String,
    val schedule: List<DayAvailability> = emptyList(),
    val dateOverrides: List<DateOverride> = emptyList()
)

/**
 * Time range for API availability format (matches backend TimeRangeDto)
 */
@Serializable
data class AvailabilityTimeRange(
    val start: String, // HH:mm format
    val end: String    // HH:mm format
)

// ===== Per-Section Update Request Models =====

@Serializable
data class UpdatePersonalInfoRequest(
    val employeeId: String,
    val firstName: String,
    val lastName: String,
    val birthDate: String,
    val phone: String,
    val email: String
)

@Serializable
data class UpdateIdentificationInfoRequest(
    val employeeId: String,
    val nationalityId: String,
    val passportId: String,
    val taxId: String? = null
)

@Serializable
data class UpdateAddressInfoRequest(
    val employeeId: String,
    val street: String,
    val city: String,
    val zipCode: String,
    val countryId: String
)

@Serializable
data class UpdateBankDetailsRequest(
    val employeeId: String,
    val iban: String
)

@Serializable
data class UpdateEmergencyContactRequest(
    val employeeId: String,
    val emergencyName: String? = null,
    val emergencyPhone: String? = null
)

@Serializable
data class UpdateAvailabilityRequest(
    val employeeId: String,
    val availability: Map<String, List<AvailabilityTimeRange>>
)

/**
 * Generic response for section updates
 */
@Serializable
data class UpdateSectionResponse(
    val employeeId: String
)

/**
 * Translation model matching backend Translation class
 */
@Serializable
data class CountryTranslation(
    val name: String? = null,
    val description: String? = null
)

/**
 * Country model matching backend CountryListItem DTO
 */
@Serializable
data class Country(
    val id: String,
    val isoCode: String? = null,
    val name: String? = null,
    val translations: Map<String, CountryTranslation>? = null
) {
    fun getLocalizedName(languageCode: String): String {
        return translations?.get(languageCode)?.name ?: name ?: id
    }
}

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
