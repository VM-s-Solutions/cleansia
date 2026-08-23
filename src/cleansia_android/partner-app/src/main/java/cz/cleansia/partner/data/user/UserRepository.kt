package cz.cleansia.partner.data.user

import cz.cleansia.core.media.Base64Image
import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.network.safeApiCall
import cz.cleansia.partner.api.client.UserApi
import cz.cleansia.partner.api.model.BlobFileDto
import cz.cleansia.partner.api.model.MyProfileDto
import cz.cleansia.partner.api.model.UpdateCurrentUserCommand
import kotlinx.serialization.json.Json
import javax.inject.Inject
import javax.inject.Singleton

/**
 * The cleaner's USER row — the login identity — as opposed to
 * [cz.cleansia.partner.data.profile.ProfileRepository], which owns the employee record.
 * `PreferredLanguageCode` lives here, and it is the only input to the language
 * `PayPeriodBackgroundService` renders the period-closed mail and the payout invoice PDF in.
 */
interface UserRepository {
    suspend fun getCurrentUser(): ApiResult<CurrentUser>

    /**
     * A partial save: absent optional fields mean "nothing to say", never "delete it". First and last
     * name are the exception — the handler replaces them outright — so every caller replays them.
     *
     * [phoneNumber] is deliberately non-null. The command's `PhoneNumber` is a non-nullable reference
     * type on a host with nullable reference types enabled, so an omitted member is refused by the
     * model binder before the handler runs; a blank string is what "nothing to say" looks like there.
     */
    suspend fun updateCurrentUser(
        firstName: String,
        lastName: String,
        phoneNumber: String,
        birthDate: String?,
        languageCode: String?,
        photo: Base64Image? = null,
        removePhoto: Boolean = false,
    ): ApiResult<Unit>
}

data class CurrentUser(
    val email: String?,
    val firstName: String?,
    val lastName: String?,
    val phoneNumber: String?,
    val birthDate: String?,
    val preferredLanguageCode: String?,
    /** Read-back URL of the stored photo, if the cleaner has one. Null means initials. */
    val profilePhotoUrl: String?,
)

@Singleton
class UserRepositoryImpl @Inject constructor(
    private val userApi: UserApi,
    private val json: Json,
) : UserRepository {

    override suspend fun getCurrentUser(): ApiResult<CurrentUser> =
        safeApiCall(json) { userApi.userGetCurrentUser() }.map { it.toDomain() }

    override suspend fun updateCurrentUser(
        firstName: String,
        lastName: String,
        phoneNumber: String,
        birthDate: String?,
        languageCode: String?,
        photo: Base64Image?,
        removePhoto: Boolean,
    ): ApiResult<Unit> = safeApiCall(json) {
        userApi.userUpdateCurrentUser(
            // No id: the row written is always the JWT caller's and the command's id is inert.
            UpdateCurrentUserCommand(
                firstName = firstName,
                lastName = lastName,
                phoneNumber = phoneNumber,
                birthDate = birthDate,
                languageCode = languageCode,
                // A three-way choice, exactly as the customer app sends it: a photo replaces, the flag
                // clears, and both absent means "nothing to say about the photo".
                photo = photo?.let {
                    BlobFileDto(
                        fileName = it.fileName,
                        base64Content = it.base64,
                        contentType = it.contentType,
                    )
                },
                removePhoto = removePhoto,
            ),
        )
    }.map { }
}

private fun MyProfileDto.toDomain() = CurrentUser(
    email = email,
    firstName = firstName,
    lastName = lastName,
    phoneNumber = phoneNumber,
    birthDate = birthDate,
    preferredLanguageCode = preferredLanguageCode,
    profilePhotoUrl = profilePhoto?.blobUrl,
)
