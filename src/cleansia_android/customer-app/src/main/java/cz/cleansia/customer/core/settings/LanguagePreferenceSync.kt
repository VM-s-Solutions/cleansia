package cz.cleansia.customer.core.settings

import cz.cleansia.customer.core.user.CurrentUser
import cz.cleansia.customer.core.user.UserRepository
import javax.inject.Inject
import javax.inject.Singleton

/**
 * Pushes the display language the user picked onto `User.PreferredLanguageCode`, which is what every
 * server-rendered mail (booking confirmations, reminders, receipts) is written in. Without it the
 * stamp is frozen at whatever signup happened to send, and [AppSettingsRepository] alone only ever
 * writes DataStore.
 */
interface LanguagePreferenceSync {
    suspend fun send(languageCode: String)
}

data class LanguagePush(
    val firstName: String,
    val lastName: String,
    val phoneNumber: String,
    val birthDate: String?,
    val languageCode: String,
)

/**
 * `UpdateCurrentUser` still replaces first and last name outright — only phone, birth date, photo and
 * language treat an absent value as "nothing to say" — so a language-only push has to replay the rest
 * of the profile verbatim, and must not run at all on a profile with holes the validators would reject.
 */
object LanguagePreferencePush {
    fun forUser(user: CurrentUser?, languageCode: String): LanguagePush? {
        if (user == null || user.preferredLanguageCode == languageCode) return null
        val phoneNumber = user.phoneNumber?.trim()?.ifBlank { null } ?: return null
        if (user.firstName.isBlank() || user.lastName.isBlank()) return null
        return LanguagePush(
            firstName = user.firstName,
            lastName = user.lastName,
            phoneNumber = phoneNumber,
            birthDate = user.birthDate,
            languageCode = languageCode,
        )
    }
}

@Singleton
class LiveLanguagePreferenceSync @Inject constructor(
    private val userRepository: UserRepository,
) : LanguagePreferenceSync {

    /**
     * Silent on failure by design: a display-language tap is not a save the user is waiting on, the
     * local switch has already happened, and the next profile save re-sends the code anyway.
     */
    override suspend fun send(languageCode: String) {
        val push = LanguagePreferencePush.forUser(userRepository.currentUser.value, languageCode) ?: return
        userRepository.updateCurrentUser(
            firstName = push.firstName,
            lastName = push.lastName,
            phoneNumber = push.phoneNumber,
            birthDate = push.birthDate,
            languageCode = push.languageCode,
        )
    }
}
