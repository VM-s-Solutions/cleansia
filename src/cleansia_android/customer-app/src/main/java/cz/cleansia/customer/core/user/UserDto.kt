package cz.cleansia.customer.core.user

import cz.cleansia.customer.api.model.MyProfileDto
import kotlinx.serialization.SerialName
import kotlinx.serialization.Serializable

/**
 * Legacy hand-written DTO holders kept for the modules that haven't yet been
 * migrated to the OpenAPI-generated client (`AddressRepository`, dispute/order
 * DTOs reference these). New code should use the equivalents from
 * `cz.cleansia.customer.api.model.*`.
 *
 * Removed by this migration pass: the old `UserDto` + its `toCurrentUser()`
 * mapper. They lost the empty-profile fight because they declared required
 * fields (`id`, `firstName`, `lastName`) that the backend response either
 * omits (`id`) or sends nullable. The generated `MyProfileDto` has the right
 * nullability and is the new source.
 */

@Serializable
data class CodeDto(
    val type: String,
    val name: String,
    val value: Int,
)

@Serializable
data class BlobFileDto(
    val fileName: String? = null,
    @SerialName("base64Content") val base64Content: String? = null,
    val contentType: String? = null,
)

/**
 * UI-facing snapshot of the current user. Thinner than the wire shape — only
 * the fields screens actually read. Keep this stable; the wire layer can
 * churn freely on the generated side.
 *
 * [id] isn't in the backend response (the server's `MyProfileDto`
 * intentionally omits it — identity lives in the JWT `sub` claim). The
 * repository decodes the token and populates this field at mapping time so
 * downstream code can keep using `user.id`.
 */
data class CurrentUser(
    val id: String,
    val email: String,
    val firstName: String,
    val lastName: String,
    val phoneNumber: String?,
    /** ISO-8601 yyyy-MM-dd, or null if not set. */
    val birthDate: String?,
    val preferredLanguageCode: String?,
) {
    val fullName: String get() = "$firstName $lastName".trim()
    val initials: String get() =
        "${firstName.firstOrNull()?.uppercaseChar() ?: ""}${lastName.firstOrNull()?.uppercaseChar() ?: ""}"
}

/**
 * Map a generated [MyProfileDto] (nullable everywhere) into the UI's
 * [CurrentUser] (non-null strings, blanks where the backend gave null).
 * Takes the user id separately because the backend's `MyProfileDto` doesn't
 * carry it — caller pulls it from the JWT.
 */
internal fun MyProfileDto.toCurrentUser(userId: String): CurrentUser = CurrentUser(
    id = userId,
    email = email.orEmpty(),
    firstName = firstName.orEmpty(),
    lastName = lastName.orEmpty(),
    phoneNumber = phoneNumber,
    birthDate = birthDate?.toString(),
    preferredLanguageCode = preferredLanguageCode,
)
