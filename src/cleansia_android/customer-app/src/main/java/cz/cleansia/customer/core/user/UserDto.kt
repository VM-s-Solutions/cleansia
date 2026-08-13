package cz.cleansia.customer.core.user

import cz.cleansia.customer.api.model.Code as GenCode
import cz.cleansia.core.network.required
import cz.cleansia.customer.api.model.MyProfileDto
import kotlinx.serialization.SerialName
import kotlinx.serialization.Serializable

/**
 * Legacy hand-written DTO holders, kept for the modules not yet migrated to the generated client.
 *
 * **New code uses the generated equivalents.**
 */

@Serializable
data class CodeDto(
    val type: String,
    val name: String,
    val value: Int,
)

/**
 * `value` is the ordinal every status decision is made from, and `0` is a real status (`New`) rather
 * than an absence — a defaulted code tells the screen the order has not started yet. `type` and
 * `name` are display labels the app never branches on, so they blank rather than refuse.
 *
 * Shared because `Code` is one wire type reached from several surfaces: a second copy of this mapper
 * is a second ruling on the same field, and the order and dispute copies had already diverged.
 */
internal fun GenCode.toAppDto(): CodeDto? {
    return CodeDto(
        type = type.orEmpty(),
        name = name.orEmpty(),
        value = `value` ?: return null,
    )
}

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
    /** Account creation instant — the profile hero "member since"; null if unknown. */
    val memberSince: kotlinx.datetime.Instant? = null,
    /** Total orders the user has placed. */
    val totalBookings: Int = 0,
    /** Realized money saved (tier + promo + membership discounts), in [savingsCurrencyCode]. */
    val totalSavings: Double = 0.0,
    /** Currency code for [totalSavings]; null when the user has no realized orders. */
    val savingsCurrencyCode: String? = null,
    /**
     * Blob name of the profile photo, or null when the user has none.
     *
     * The backend mints a fresh GUID on every upload, so this is content-addressed:
     * it is the image cache key, and it changes exactly when the image changes.
     */
    val avatarFileName: String? = null,
    /**
     * Read SAS for [avatarFileName] — a live credential valid for one hour, minted
     * per profile fetch. Fetch target only: never cache on it (it differs on every
     * read) and never persist it.
     */
    val avatarUrl: String? = null,
) {
    val fullName: String get() = "$firstName $lastName".trim()
    val initials: String get() =
        "${firstName.firstOrNull()?.uppercaseChar() ?: ""}${lastName.firstOrNull()?.uppercaseChar() ?: ""}"
}

/**
 * Map a generated [MyProfileDto] into the UI's [CurrentUser]. Takes the user id separately because
 * the backend's `MyProfileDto` doesn't carry it — caller pulls it from the JWT.
 *
 * `email`, `firstName` and `lastName` are non-nullable on the C# record, so blanking them renders a
 * signed-in account with no name and no address to recover it from. The spec calls them
 * `nullable: true` — it calls every string on this wire that, which is why the C# is the contract.
 */
internal fun MyProfileDto.toCurrentUser(userId: String): CurrentUser? {
    return CurrentUser(
        id = userId,
        email = email.required("email"),
        firstName = firstName.required("firstName"),
        lastName = lastName.required("lastName"),
        phoneNumber = phoneNumber,
        birthDate = birthDate?.toString(),
        preferredLanguageCode = preferredLanguageCode,
        memberSince = memberSince,
        // "You have saved 0 Kc" is a claim about this customer's money, not a blank field.
        totalBookings = totalBookings ?: return null,
        totalSavings = totalSavings ?: return null,
        savingsCurrencyCode = savingsCurrencyCode,
        avatarFileName = profilePhoto?.fileName?.takeIf { it.isNotBlank() },
        avatarUrl = profilePhoto?.blobUrl?.takeIf { it.isNotBlank() },
    )
}
