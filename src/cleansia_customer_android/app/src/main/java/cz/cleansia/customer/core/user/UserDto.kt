package cz.cleansia.customer.core.user

import kotlinx.serialization.SerialName
import kotlinx.serialization.Serializable

/**
 * Mirrors backend `UserListItem` (Cleansia.Core.AppServices.Features.Users.DTOs.UserListItem).
 * Field casing matches the default JSON policy on the server (camelCase).
 *
 * We don't send this wire DTO into UI layers directly — the repository maps it
 * into [CurrentUser] so UI code doesn't couple to network shapes.
 */
@Serializable
data class UserDto(
    val id: String,
    val email: String,
    val firstName: String,
    val lastName: String,
    val phoneNumber: String? = null,
    val profile: CodeDto? = null,
    val authenticationType: CodeDto? = null,
    val isEmailConfirmed: Boolean = false,
    /** ISO-8601 yyyy-MM-dd, nullable. */
    val birthDate: String? = null,
    val profilePhoto: BlobFileDto? = null,
    val preferredLanguageCode: String? = null,
    val preferredLanguageName: String? = null,
)

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
 * UI-facing snapshot of the current user. Thinner than [UserDto] — only the
 * fields any screen actually reads. Keep this stable; add fields as screens
 * start to need them rather than front-loading.
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

internal fun UserDto.toCurrentUser(): CurrentUser = CurrentUser(
    id = id,
    email = email,
    firstName = firstName,
    lastName = lastName,
    phoneNumber = phoneNumber,
    birthDate = birthDate,
    preferredLanguageCode = preferredLanguageCode,
)
