package cz.cleansia.customer.features.profile

import cz.cleansia.core.ui.components.isPossiblePhoneNumber
import cz.cleansia.customer.core.user.CurrentUser

/** What a booking needs, judged on the saved profile and the phone as typed now. */
internal data class OnboardingChecklist(val name: Boolean, val email: Boolean, val phone: Boolean) {
    val doneCount: Int get() = listOf(name, email, phone).count { it }
    val isReady: Boolean get() = name && email && phone

    companion object {
        fun of(user: CurrentUser, phone: String) = OnboardingChecklist(
            name = user.firstName.isNotBlank() && user.lastName.isNotBlank(),
            email = user.email.isNotBlank(),
            phone = isPossiblePhoneNumber(phone.trim()),
        )
    }
}

/** Apple's relay, the one suffix the server itself checks. */
internal fun isAppleRelayEmail(email: String): Boolean = email.endsWith("@privaterelay.appleid.com", ignoreCase = true)

/** The local part and the domain, split at the last @, so a long address can give way in its local part only. */
internal fun emailParts(email: String): Pair<String, String> {
    val at = email.lastIndexOf('@')
    return if (at < 0) email to "" else email.substring(0, at) to email.substring(at)
}
