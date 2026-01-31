package cz.cleansia.partner.domain.models.auth

import kotlinx.serialization.SerialName
import kotlinx.serialization.Serializable

@Serializable
data class LoginRequest(
    val email: String,
    val password: String
)

@Serializable
data class LoginResponse(
    val token: String = "",
    @SerialName("userId")
    val userId: String = "",
    @SerialName("id")
    val id: String? = null,
    val email: String = "",
    val firstName: String? = null,
    val lastName: String? = null,
    val isEmailConfirmed: Boolean = false
) {
    // Helper to get the actual user ID (API might return it as 'id' or 'userId')
    val actualUserId: String
        get() = userId.ifEmpty { id ?: "" }
}

@Serializable
data class RegisterRequest(
    val email: String,
    val password: String,
    val confirmPassword: String,
    val firstName: String,
    val lastName: String,
    val phoneNumber: String? = null
)

@Serializable
data class RegisterResponse(
    val userId: String = "",
    @SerialName("id")
    val id: String? = null,
    val email: String = "",
    val message: String? = null
) {
    val actualUserId: String
        get() = userId.ifEmpty { id ?: "" }
}

@Serializable
data class ConfirmEmailRequest(
    val email: String,
    val token: String
)

@Serializable
data class ResendConfirmationRequest(
    val email: String
)

@Serializable
data class ForgotPasswordRequest(
    val email: String
)

@Serializable
data class ResetPasswordRequest(
    val email: String,
    val token: String,
    val newPassword: String,
    val confirmPassword: String
)

/**
 * Represents the current user
 */
data class User(
    val id: String,
    val email: String,
    val firstName: String?,
    val lastName: String?,
    val isEmailConfirmed: Boolean
) {
    val fullName: String
        get() = listOfNotNull(firstName, lastName).joinToString(" ").ifEmpty { email }
}
