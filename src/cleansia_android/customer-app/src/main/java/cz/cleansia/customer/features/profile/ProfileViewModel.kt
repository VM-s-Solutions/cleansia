package cz.cleansia.customer.features.profile

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.customer.core.settings.AppSettingsRepository
import cz.cleansia.customer.core.user.CurrentUser
import cz.cleansia.customer.core.user.UserRepository
import cz.cleansia.customer.ui.state.ActionState
import cz.cleansia.core.snackbar.SnackbarController
import dagger.hilt.android.lifecycle.HiltViewModel
import javax.inject.Inject
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch

/**
 * Shared VM for the profile tab and the edit-profile screen. Both surface the
 * same cached [UserRepository.currentUser], so we keep one VM behind both and
 * let Compose re-use the instance via `hiltViewModel()`.
 */
@HiltViewModel
class ProfileViewModel @Inject constructor(
    private val userRepository: UserRepository,
    private val settings: AppSettingsRepository,
    private val snackbar: SnackbarController,
) : ViewModel() {

    val currentUser: StateFlow<CurrentUser?> = userRepository.currentUser

    private val _refreshState = MutableStateFlow<ActionState>(ActionState.Idle)
    val refreshState: StateFlow<ActionState> = _refreshState.asStateFlow()

    private val _saveState = MutableStateFlow<ActionState>(ActionState.Idle)
    val saveState: StateFlow<ActionState> = _saveState.asStateFlow()

    /**
     * Trigger a refresh. We don't auto-fetch in init so test harnesses can
     * inject a cached user; the tab calls this on first composition.
     *
     * No snackbar from here: the NetworkErrorInterceptor already shows a global
     * toast for infrastructure failures (IOException, 5xx); a second toast here
     * on every Home cold-start double-toasted genuine failures. HTTP 4xx
     * (auth/business) is not toasted by the interceptor — a caller that needs
     * those should surface the returned error itself.
     */
    fun refresh() {
        if (_refreshState.value is ActionState.Submitting) return
        _refreshState.value = ActionState.Submitting
        viewModelScope.launch {
            userRepository.refreshCurrentUser()
            _refreshState.value = ActionState.Idle
        }
    }

    /**
     * Save profile edits. Calls [onSaved] after the server accepts + the local
     * cache is refreshed, so the edit screen can pop back to a fresh profile.
     */
    fun saveProfile(
        firstName: String,
        lastName: String,
        phoneNumber: String?,
        birthDate: String?,
        languageCode: String?,
        onSaved: () -> Unit,
    ) {
        if (_saveState.value is ActionState.Submitting) return
        _saveState.value = ActionState.Submitting
        viewModelScope.launch {
            val error = userRepository.updateCurrentUser(
                firstName = firstName.trim(),
                lastName = lastName.trim(),
                phoneNumber = phoneNumber?.trim(),
                birthDate = birthDate?.trim(),
                languageCode = languageCode,
            )
            if (error == null) {
                _saveState.value = ActionState.Idle
                onSaved()
            } else {
                _saveState.value = ActionState.Error(error)
                snackbar.showError(error)
            }
        }
    }

    /**
     * Save the onboarding fields without disturbing first/last name (the
     * registration form already collected those). Language is auto-detected
     * from device locale — we only send it if the device language is one we
     * actually ship translations for, otherwise the backend default ("en") wins.
     * Marks onboarding seen on success so the screen doesn't reappear.
     */
    fun completeOnboarding(
        phoneNumber: String,
        birthDate: String?,
        onCompleted: () -> Unit,
    ) {
        if (_saveState.value is ActionState.Submitting) return
        val user = userRepository.currentUser.value ?: return
        val deviceLang = java.util.Locale.getDefault().language.lowercase()
        val languageCode = if (deviceLang in SUPPORTED_LANGUAGES) deviceLang else "en"
        _saveState.value = ActionState.Submitting
        viewModelScope.launch {
            val error = userRepository.updateCurrentUser(
                firstName = user.firstName,
                lastName = user.lastName,
                phoneNumber = phoneNumber.trim(),
                birthDate = birthDate?.trim(),
                languageCode = languageCode,
            )
            if (error == null) {
                _saveState.value = ActionState.Idle
                settings.markOnboardingSeen(user.id)
                onCompleted()
            } else {
                _saveState.value = ActionState.Error(error)
                snackbar.showError(error)
            }
        }
    }

    private companion object {
        val SUPPORTED_LANGUAGES = setOf("en", "cs", "sk", "uk", "ru")
    }

    /**
     * User dismissed onboarding. Mark seen so we don't re-prompt at startup.
     * The booking-submit pre-flight will still gate them if they try to book
     * with an incomplete profile.
     */
    fun skipOnboarding(onSkipped: () -> Unit) {
        val user = userRepository.currentUser.value ?: run {
            onSkipped()
            return
        }
        viewModelScope.launch {
            settings.markOnboardingSeen(user.id)
            onSkipped()
        }
    }
}
