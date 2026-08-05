package cz.cleansia.customer.features.profile

import android.content.Context
import android.net.Uri
import androidx.annotation.StringRes
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.customer.R
import cz.cleansia.customer.core.memberships.MembershipRepository
import cz.cleansia.customer.core.settings.AppSettingsRepository
import cz.cleansia.core.media.Base64Image
import cz.cleansia.core.media.ImageCompressor
import cz.cleansia.core.network.ApiError
import cz.cleansia.customer.core.user.CurrentUser
import cz.cleansia.customer.core.user.UserRepository
import cz.cleansia.customer.ui.state.ActionState
import cz.cleansia.core.snackbar.SnackbarController
import dagger.hilt.android.lifecycle.HiltViewModel
import dagger.hilt.android.qualifiers.ApplicationContext
import javax.inject.Inject
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.SharingStarted
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.map
import kotlinx.coroutines.flow.stateIn
import kotlinx.coroutines.launch

/**
 * What the user has asked to happen to their avatar, pending a save.
 *
 * Three cases rather than a nullable image, because "no image" is ambiguous
 * between "leave it alone" and "delete it" — and the server treats those very
 * differently (`UpdateCurrentUser.cs:135`).
 */
sealed interface AvatarDraft {
    data object Unchanged : AvatarDraft

    /**
     * [previewUri] is the picked content URI, rendered locally so the change is
     * visible before the save round trip; [image] is what actually uploads.
     */
    data class Picked(val previewUri: Uri, val image: Base64Image) : AvatarDraft

    data object Removed : AvatarDraft
}

/**
 * What to confirm once the server has accepted a save carrying this draft.
 *
 * A save confirms exactly once, and a photo message wins over the general one
 * when there is a photo message: it is the more specific claim, about the change
 * the user can actually see. Null for [AvatarDraft.Unchanged] hands the save back
 * to that general confirmation.
 */
@StringRes
private fun avatarSaveMessageRes(draft: AvatarDraft): Int? = when (draft) {
    is AvatarDraft.Picked -> R.string.profile_avatar_upload_success
    AvatarDraft.Removed -> R.string.profile_avatar_remove_success
    AvatarDraft.Unchanged -> null
}

/**
 * Shared VM for the profile tab and the edit-profile screen. Both surface the
 * same cached [UserRepository.currentUser], so we keep one VM behind both and
 * let Compose re-use the instance via `hiltViewModel()`.
 */
@HiltViewModel
class ProfileViewModel @Inject constructor(
    private val userRepository: UserRepository,
    membershipRepository: MembershipRepository,
    private val settings: AppSettingsRepository,
    private val snackbar: SnackbarController,
    @ApplicationContext private val appContext: Context,
) : ViewModel() {

    val currentUser: StateFlow<CurrentUser?> = userRepository.currentUser

    /**
     * Drives the hero tier badge (Plus vs Regular) from the shared membership
     * cache — same source the management card and iOS's `membershipVM` read.
     */
    val isPlus: StateFlow<Boolean> = membershipRepository.current
        .map { it?.hasMembership == true }
        .stateIn(viewModelScope, SharingStarted.Eagerly, false)

    private val _refreshState = MutableStateFlow<ActionState>(ActionState.Idle)
    val refreshState: StateFlow<ActionState> = _refreshState.asStateFlow()

    private val _saveState = MutableStateFlow<ActionState>(ActionState.Idle)
    val saveState: StateFlow<ActionState> = _saveState.asStateFlow()

    private val _avatarDraft = MutableStateFlow<AvatarDraft>(AvatarDraft.Unchanged)
    val avatarDraft: StateFlow<AvatarDraft> = _avatarDraft.asStateFlow()

    /** Covers the decode + downscale of a pick, which is seconds on a big capture. */
    private val _avatarState = MutableStateFlow<ActionState>(ActionState.Idle)
    val avatarState: StateFlow<ActionState> = _avatarState.asStateFlow()

    /**
     * The avatar blob name we have already spent our one refetch on. Keyed on the
     * name rather than a bare flag because a new upload mints a new name, which
     * restores the budget for the image that actually changed.
     */
    private var avatarRetriedFor: String? = null

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
     * Downscale and strip the metadata from a picked image, then hold it as the
     * pending draft. Nothing is uploaded until the user saves.
     *
     * The picker callback is dispatched on the main thread and hands over only a
     * [Uri]; [ImageCompressor] does the read, decode, downscale and base64 off it.
     */
    fun pickAvatar(uri: Uri) {
        if (_avatarState.value is ActionState.Submitting) return
        _avatarState.value = ActionState.Submitting
        viewModelScope.launch {
            val encoded = ImageCompressor.compressToBase64(appContext.contentResolver, uri)
            _avatarState.value = ActionState.Idle
            if (encoded == null) {
                snackbar.showError(appContext.getString(R.string.profile_avatar_encode_failed))
                return@launch
            }
            _avatarDraft.value = AvatarDraft.Picked(previewUri = uri, image = encoded)
        }
    }

    /**
     * A removal is only meaningful against a photo the server actually holds. The
     * options sheet also opens over a pick that has never been uploaded, and there
     * the tap can only mean "drop what I just chose" — recording a removal instead
     * would have the app report deleting a photo that never existed.
     */
    fun removeAvatar() {
        val hasSavedPhoto = userRepository.currentUser.value?.avatarFileName != null
        _avatarDraft.value = if (hasSavedPhoto) AvatarDraft.Removed else AvatarDraft.Unchanged
    }

    /** Leaving the edit screen without saving must not carry the pick into the next visit. */
    fun discardAvatarDraft() {
        _avatarDraft.value = AvatarDraft.Unchanged
    }

    /**
     * The avatar image failed to load: refetch the profile once for a fresh SAS.
     *
     * The loader cannot see the status code, so there is nothing to branch on — an
     * expired SAS (403) and a deleted blob (404) arrive identically. Refetching once
     * fixes the first and is harmless for the second, which then falls back to the
     * initials; [avatarRetriedFor] is what stops the second case looping.
     */
    fun onAvatarLoadFailed() {
        val fileName = userRepository.currentUser.value?.avatarFileName ?: return
        if (avatarRetriedFor == fileName) return
        avatarRetriedFor = fileName
        viewModelScope.launch { userRepository.refreshCurrentUser() }
    }

    /** Hands the retry budget back, so an expiry later in a long session can still recover. */
    fun onAvatarLoadSucceeded() {
        avatarRetriedFor = null
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
        val draft = _avatarDraft.value
        viewModelScope.launch {
            val result = userRepository.updateCurrentUser(
                firstName = firstName.trim(),
                lastName = lastName.trim(),
                phoneNumber = phoneNumber?.trim(),
                birthDate = birthDate?.trim(),
                languageCode = languageCode,
                photo = (draft as? AvatarDraft.Picked)?.image,
                removePhoto = draft is AvatarDraft.Removed,
            )
            result
                .onSuccess {
                    snackbar.showSuccessKey(
                        avatarSaveMessageRes(draft) ?: R.string.profile_save_success,
                    )
                    _avatarDraft.value = AvatarDraft.Unchanged
                    _saveState.value = ActionState.Idle
                    onSaved()
                }
                .onError { error ->
                    _saveState.value = ActionState.Error(error.getUserMessage())
                    if (error !is ApiError.Network) snackbar.showError(error.getUserMessage())
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
            val result = userRepository.updateCurrentUser(
                firstName = user.firstName,
                lastName = user.lastName,
                phoneNumber = phoneNumber.trim(),
                birthDate = birthDate?.trim(),
                languageCode = languageCode,
            )
            result
                .onSuccess {
                    _saveState.value = ActionState.Idle
                    settings.markOnboardingSeen(user.id)
                    onCompleted()
                }
                .onError { error ->
                    _saveState.value = ActionState.Error(error.getUserMessage())
                    if (error !is ApiError.Network) snackbar.showError(error.getUserMessage())
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
