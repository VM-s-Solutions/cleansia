package cz.cleansia.customer.core.auth

import kotlinx.serialization.SerialName
import kotlinx.serialization.Serializable
import retrofit2.Response
import retrofit2.http.Body
import retrofit2.http.POST
import retrofit2.http.PUT

/**
 * Hand-written Retrofit interface for Cleansia.Web.Customer auth endpoints.
 *
 * We don't use the OpenAPI-generated client here because (a) ASP.NET's default
 * operationIds produce noisy Kotlin method names and (b) the refresh endpoint
 * needs a separate no-auth OkHttp client, which is easier to wire when the
 * interface is small and under our control.
 *
 * All methods return [Response] so the caller can inspect HTTP status — auth
 * endpoints sometimes return 200 with a special payload (unconfirmed email
 * returns 200 with empty Token) and sometimes non-2xx. Wrapping in ProblemDetails
 * for errors happens in [AuthRepository].
 */
interface AuthApi {
    @POST("api/Auth/Login")
    suspend fun login(@Body body: LoginRequest): Response<JwtTokenResponseDto>

    @POST("api/Auth/Register")
    suspend fun register(@Body body: RegisterRequest): Response<Boolean>

    @POST("api/Auth/GoogleAuth")
    suspend fun googleAuth(@Body body: GoogleAuthRequest): Response<JwtTokenResponseDto>

    @PUT("api/Auth/ConfirmUserEmail")
    suspend fun confirmUserEmail(@Body body: ConfirmUserEmailRequest): Response<JwtTokenResponseDto>

    @POST("api/Auth/ResendConfirmationEmail")
    suspend fun resendConfirmationEmail(@Body body: ResendConfirmationEmailRequest): Response<Boolean>

    @POST("api/Auth/RefreshToken")
    suspend fun refreshToken(@Body body: RefreshTokenRequest): Response<JwtTokenResponseDto>

    @POST("api/Auth/Logout")
    suspend fun logout(@Body body: LogoutRequest): Response<Boolean>
}

// ─── Request bodies ───
// Field names match the backend command shapes exactly — keep lowercased-first-letter
// to satisfy ASP.NET's default camel-case JSON binding.

@Serializable
data class LoginRequest(
    val email: String,
    val password: String,
    val rememberMe: Boolean,
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
)

@Serializable
data class ConfirmUserEmailRequest(val code: String)

@Serializable
data class ResendConfirmationEmailRequest(
    val email: String,
    val language: String,
)

@Serializable
data class RefreshTokenRequest(val token: String)

@Serializable
data class LogoutRequest(val token: String)

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
