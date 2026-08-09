package cz.cleansia.partner.core.settings

import cz.cleansia.core.auth.TokenStore
import cz.cleansia.core.network.ApiResult
import cz.cleansia.partner.core.coroutines.ApplicationScope
import cz.cleansia.partner.data.user.CurrentUser
import cz.cleansia.partner.data.user.UserRepository
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.launch
import javax.inject.Inject
import javax.inject.Singleton

/**
 * Pushes the display language the cleaner picked onto `User.PreferredLanguageCode`. That stamp is
 * what `PayPeriodBackgroundService` threads into both the period-closed email and the rendered payout
 * invoice PDF — a tax document the cleaner files — so a language that only ever reached DataStore
 * leaves the app in Czech and the document that matters in whatever signup happened to send.
 */
interface LanguagePreferenceSync {
    /**
     * Returns before the push completes, and deliberately does not run in the caller's scope: the
     * settings picker pops the back stack in the same tap, and a round trip owned by that screen's
     * ViewModel would be cancelled somewhere between the two requests.
     */
    fun send(languageCode: String)
}

data class LanguagePush(
    val firstName: String,
    val lastName: String,
    val phoneNumber: String,
    val birthDate: String?,
    val languageCode: String,
)

/**
 * `UpdateCurrentUser` still replaces first and last name outright — phone, birth date, photo and
 * language treat an absent value as "nothing to say" — so a language-only push has to replay the rest
 * of the profile verbatim, and must not run on a profile whose names the validators would reject.
 *
 * A missing phone or birth date is NOT such a hole: the cleaner on the registration lock frequently
 * has neither yet, and both survive the save untouched. The phone still travels, as a blank, because
 * an omitted member is refused by the model binder rather than read as silence.
 */
object LanguagePreferencePush {
    fun forUser(user: CurrentUser?, languageCode: String): LanguagePush? {
        if (user == null || user.preferredLanguageCode == languageCode) return null
        val firstName = user.firstName?.trim()?.ifBlank { null } ?: return null
        val lastName = user.lastName?.trim()?.ifBlank { null } ?: return null
        return LanguagePush(
            firstName = firstName,
            lastName = lastName,
            phoneNumber = user.phoneNumber.orEmpty(),
            birthDate = user.birthDate,
            languageCode = languageCode,
        )
    }
}

@Singleton
class LiveLanguagePreferenceSync @Inject constructor(
    private val tokenStore: TokenStore,
    private val userRepository: UserRepository,
    @ApplicationScope private val scope: CoroutineScope,
) : LanguagePreferenceSync {

    override fun send(languageCode: String) {
        scope.launch { push(languageCode) }
    }

    /**
     * Silent on failure by design: a display-language tap is not a save the cleaner is waiting on and
     * the local switch has already happened, so there is nothing to report and nothing to retry.
     *
     * The session is answered from the token store rather than by letting the call 401. The pre-login
     * carousel carries this same seam, and a 401 there hands the shared authenticator a refresh it
     * cannot perform on a handset that has never signed in.
     */
    private suspend fun push(languageCode: String) {
        if (tokenStore.current() == null) return
        val user = (userRepository.getCurrentUser() as? ApiResult.Success)?.data ?: return
        val push = LanguagePreferencePush.forUser(user, languageCode) ?: return
        userRepository.updateCurrentUser(
            firstName = push.firstName,
            lastName = push.lastName,
            phoneNumber = push.phoneNumber,
            birthDate = push.birthDate,
            languageCode = push.languageCode,
        )
    }
}
