package cz.cleansia.customer.core.settings

import cz.cleansia.customer.core.user.CurrentUser
import cz.cleansia.customer.core.user.UserRepository
import kotlinx.coroutines.flow.first
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

    /**
     * What a beginning session calls ([LanguageSessionObserver]): re-states the language the user
     * *chose* in this app, so a [send] lost to a dead connection — or a sign-up stamp the server set on
     * its own, as Google and Apple sign-up do — stops waiting for the next visit to the picker.
     *
     * Reads the server before comparing, because the cached profile is cleared with the session and a
     * reconcile trusting it would compare against nothing on the exact launch it exists for. Only an
     * explicit choice reconciles: with "System" the resolved tag is the handset's locale ordering, and
     * pushing it would overwrite a language picked on another client. Mirrors iOS `LanguageReconciler`
     * and the partner app.
     */
    suspend fun reconcile()
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
 * of the profile verbatim, and must not run on a profile whose names the validators would reject.
 *
 * A missing phone is NOT such a hole: a customer who signed up with Google or Apple often has none, the
 * server validates a phone only when one is given, and a blank leaves the stored one untouched. Gating
 * on it left exactly those customers on the sign-up stamp ('en') for every promo and status e-mail.
 */
object LanguagePreferencePush {
    fun forUser(user: CurrentUser?, languageCode: String): LanguagePush? {
        if (user == null || user.preferredLanguageCode == languageCode) return null
        if (user.firstName.isBlank() || user.lastName.isBlank()) return null
        return LanguagePush(
            firstName = user.firstName,
            lastName = user.lastName,
            phoneNumber = user.phoneNumber?.trim().orEmpty(),
            birthDate = user.birthDate,
            languageCode = languageCode,
        )
    }
}

@Singleton
class LiveLanguagePreferenceSync @Inject constructor(
    private val userRepository: UserRepository,
    private val appSettingsRepository: AppSettingsRepository,
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

    /** Silent for the same reason as [send]; a user who never chose a language costs nothing at all. */
    override suspend fun reconcile() {
        val chosen = appSettingsRepository.settings.first().language.tag ?: return
        userRepository.refreshCurrentUser()
        send(chosen)
    }
}
