package cz.cleansia.customer.core.recurring

import kotlinx.serialization.Serializable

/**
 * Frequency enum mirroring backend `RecurrenceFrequency`. Persisted as int —
 * don't reorder. Keep aligned with `Cleansia.Core.Domain.Bookings.RecurrenceFrequency`.
 */
enum class RecurrenceFrequency(val code: Int) {
    Weekly(1),
    Biweekly(2),
    Monthly(3),
    ;

    companion object {
        fun fromCode(code: Int?): RecurrenceFrequency = when (code) {
            1 -> Weekly
            2 -> Biweekly
            3 -> Monthly
            else -> Weekly
        }
    }
}

/**
 * Mirrors backend `RecurringBookingTemplateDto`. dayOfWeek follows the
 * .NET DayOfWeek enum (Sun=0, Mon=1, ..., Sat=6) — convert when displaying
 * with Java's java.time.DayOfWeek (Mon=1, ..., Sun=7) by mapping
 * Java's value % 7 to match.
 */
@Serializable
data class RecurringBookingTemplateDto(
    val id: String,
    val frequency: Int,
    val dayOfWeek: Int,
    /** "HH:mm" 24h format. */
    val timeOfDay: String,
    val rooms: Int,
    val bathrooms: Int,
    val savedAddressId: String,
    val addressLine: String? = null,
    val selectedServiceIds: List<String> = emptyList(),
    val selectedPackageIds: List<String> = emptyList(),
    /** 1 = Cash, 2 = Card. */
    val paymentType: Int,
    val startsOn: String,
    val endsOn: String? = null,
    val lastMaterializedFor: String? = null,
    val isActive: Boolean,
)

@Serializable
data class CreateRecurringBookingRequest(
    val frequency: Int,
    val dayOfWeek: Int,
    val timeOfDay: String,
    val rooms: Int,
    val bathrooms: Int,
    val savedAddressId: String,
    val selectedServiceIds: List<String>,
    val selectedPackageIds: List<String>,
    val paymentType: Int,
    /** ISO-8601 instant; backend Validator requires today or later. */
    val startsOn: String,
    val endsOn: String? = null,
)

@Serializable
data class UpdateRecurringBookingRequest(
    val templateId: String,
    val frequency: Int,
    val dayOfWeek: Int,
    val timeOfDay: String,
    val rooms: Int,
    val bathrooms: Int,
    val savedAddressId: String,
    val selectedServiceIds: List<String>,
    val selectedPackageIds: List<String>,
    val paymentType: Int,
    val startsOn: String,
    val endsOn: String? = null,
)

@Serializable
data class SetRecurringBookingActiveRequest(
    val templateId: String,
    val isActive: Boolean,
)

@Serializable
data class DeleteRecurringBookingRequest(
    val templateId: String,
)
