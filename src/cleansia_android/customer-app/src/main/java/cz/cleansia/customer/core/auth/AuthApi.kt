package cz.cleansia.customer.core.auth

import kotlinx.serialization.SerialName
import kotlinx.serialization.Serializable
import retrofit2.Response
import retrofit2.http.Body
import retrofit2.http.POST
import retrofit2.http.PUT

/**
 * Hand-written Retrofit interface for the customer auth endpoints.
 *
 * **Deliberately not the generated client**: the default operation ids produce noisy method names, and
 * the refresh endpoint needs its own no-auth OkHttp client, which is far easier to wire against a small
 * Register, ResendConfirmationEmail and Logout answer 200 with NO BODY since T-0665 — the bool
 * they used to return was `true` on every success path, because a failure arrives as an error
 * status. They are typed Response<Unit> for that reason. Declaring Response<Boolean> against an
 * empty body still COMPILES and fails at runtime, so keep these in step with the server.
 *
 * hand-written interface. -> /flows/auth-and-identity
 */
interface AuthApi {
    @POST("api/Auth/Login")
    suspend fun login(@Body body: LoginRequest): Response<JwtTokenResponseDto>

    @POST("api/Auth/Register")
    suspend fun register(@Body body: RegisterRequest): Response<Unit>

    @POST("api/Auth/GoogleAuth")
    suspend fun googleAuth(@Body body: GoogleAuthRequest): Response<JwtTokenResponseDto>

    @PUT("api/Auth/ConfirmUserEmail")
    suspend fun confirmUserEmail(@Body body: ConfirmUserEmailRequest): Response<JwtTokenResponseDto>

    @POST("api/Auth/ResendConfirmationEmail")
    suspend fun resendConfirmationEmail(@Body body: ResendConfirmationEmailRequest): Response<Unit>

    @POST("api/Auth/RefreshToken")
    suspend fun refreshToken(@Body body: RefreshTokenRequest): Response<JwtTokenResponseDto>

    @POST("api/Auth/Logout")
    suspend fun logout(@Body body: LogoutRequest): Response<Unit>

    // Password-reset endpoints live on UserController server-side, but
    // conceptually they're auth flows (run pre-session). Keeping them in
    // AuthApi lets them share the unauthenticated OkHttp client.
    @PUT("api/User/RequestPasswordChange")
    suspend fun requestPasswordChange(@Body body: RequestPasswordChangeRequest): Response<Unit>

    @PUT("api/User/ChangePassword")
    suspend fun changePassword(@Body body: ChangePasswordRequest): Response<ChangePasswordResponseDto>
}

// ─── Request bodies ───
// Field names match the backend command shapes exactly — keep lowercased-first-letter
// to satisfy ASP.NET's default camel-case JSON binding.

@Serializable
data class LoginRequest(
    val email: String,
    val password: String,
    val rememberMe: Boolean,
    /**
     * Optional trusted-device marker — the refresh token from a previous session, which lets a
     * locked-out account sign in from its own device. Null when nothing is stored, and the
     * converter drops null keys, so "no previous session" reaches the server as an absent field.
     */
    val trustedDeviceToken: String? = null,
)

@Serializable
data class RegisterRequest(
    val email: String,
    val password: String,
    val firstName: String,
    val lastName: String,
    val language: String,
    /**
     * Loyalty Phase C — optional referral code entered at signup. Backend
     * accepts null/blank without failing registration; if non-null and valid,
     * a Referral row is created in Accepted state and the bonus pays out on
     * the user's first completed order.
     */
    val referralCode: String? = null,
)

@Serializable
data class GoogleAuthRequest(
    val token: String,
    val googleId: String,
    val email: String,
    val firstName: String,
    val lastName: String,
    /**
     * Whether the caller asserted the signup screen's terms tick. The backend provisions a new
     * identity only for a call that did; a sign-in of an existing account ignores it. Deliberately
     * without a default — a consent flag that can be omitted at a call site is a consent record
     * nobody agreed to.
     */
    val termsAccepted: Boolean,
)

// The email names the account the 6-digit code was issued to — the server verifies the code ONLY
// against that account (a bare code proves nothing by itself).
@Serializable
data class ConfirmUserEmailRequest(val code: String, val email: String)

@Serializable
data class ResendConfirmationEmailRequest(
    val email: String,
    val language: String,
)

@Serializable
data class RefreshTokenRequest(val token: String)

@Serializable
data class LogoutRequest(val token: String)

@Serializable
data class RequestPasswordChangeRequest(
    val email: String,
    val language: String,
)

@Serializable
data class ChangePasswordRequest(
    val email: String,
    val newPassword: String,
    val code: String,
)

@Serializable
data class ChangePasswordResponseDto(
    val email: String? = null,
)

// ─── Response ───

@Serializable
data class JwtTokenResponseDto(
    @SerialName("token") val token: String,
    @SerialName("isEmailConfirmed") val isEmailConfirmed: Boolean,
    @SerialName("hasAdminAccess") val hasAdminAccess: Boolean = true,
    @SerialName("userId") val userId: String? = null,
    @SerialName("email") val email: String? = null,
    @SerialName("refreshToken") val refreshToken: String? = null,
    /** ISO-8601 string; parsed into millis by the repo. */
    @SerialName("refreshTokenExpiresAt") val refreshTokenExpiresAt: String? = null,
)
