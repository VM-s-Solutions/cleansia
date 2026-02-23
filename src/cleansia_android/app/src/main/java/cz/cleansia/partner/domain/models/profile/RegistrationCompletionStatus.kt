package cz.cleansia.partner.domain.models.profile

import kotlinx.serialization.Serializable

@Serializable
data class RegistrationCompletionStatus(
    val areDocumentsUploaded: Boolean = false,
    val hasCompletedProfile: Boolean = false
) {
    val isComplete: Boolean
        get() = areDocumentsUploaded && hasCompletedProfile
}
