package cz.cleansia.customer.core.user

import kotlinx.serialization.Serializable
import retrofit2.Response
import retrofit2.http.Body
import retrofit2.http.GET
import retrofit2.http.POST
import retrofit2.http.PUT

/**
 * Non-auth endpoints on the customer API that a signed-in user needs. Lives
 * separately from [cz.cleansia.customer.core.auth.AuthApi] so the two
 * concerns don't tangle; this one uses the authenticated OkHttp client
 * (Bearer token + refresh on 401).
 *
 * Add new methods here as we wire more screens in Phase 6 (profile, orders,
 * addresses, etc.) — or generate them via the OpenAPI plugin once we flip that on.
 */
interface UserApi {
    /**
     * Fetch the authenticated user's profile. Route is `api/User/GetCurrent`
     * (the UserController overrides the `api/v{version}` base route — see
     * [Cleansia.Web.Customer.Controllers.UserController] on the backend).
     */
    @GET("api/User/GetCurrent")
    suspend fun getCurrent(): Response<UserDto>

    /**
     * Update the authenticated user's profile fields. Backend responds with
     * an `UpdateCurrentUser_Response` carrying the new id, which we don't use —
     * after save we just re-fetch via [getCurrent] so the cache stays authoritative.
     */
    @PUT("api/User/UpdateCurrentUser")
    suspend fun updateCurrentUser(@Body command: UpdateCurrentUserCommand): Response<UpdateCurrentUserResponse>

    /**
     * Permanently deletes the authenticated user's account per Play Policy and GDPR.
     * Backend anonymizes profile, deletes blobs (photos, documents), wipes devices,
     * withdraws consents, deactivates the user. Operation is immediate.
     *
     * Response body is empty 200/204. We key off HTTP success for the UI flow.
     */
    @POST("api/v1/Gdpr/delete-account")
    suspend fun deleteAccount(): Response<Unit>
}

/**
 * Mirrors backend `UpdateCurrentUser.Command`. Backend validator requires
 * a non-empty `Id` — pass the current user's id from the cached profile.
 */
@Serializable
data class UpdateCurrentUserCommand(
    val id: String,
    val firstName: String? = null,
    val lastName: String? = null,
    val phoneNumber: String? = null,
    /** ISO-8601 yyyy-MM-dd. */
    val birthDate: String? = null,
    val photo: BlobFileDto? = null,
    val languageCode: String? = null,
)

@Serializable
data class UpdateCurrentUserResponse(
    val id: String? = null,
)
