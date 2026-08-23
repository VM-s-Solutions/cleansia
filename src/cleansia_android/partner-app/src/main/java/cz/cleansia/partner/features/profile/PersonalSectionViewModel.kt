package cz.cleansia.partner.features.profile

import android.content.Context
import android.net.Uri
import cz.cleansia.core.media.Base64Image
import cz.cleansia.core.media.ImageCompressor
import cz.cleansia.partner.data.user.UserRepository
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.core.ui.state.ActionState
import cz.cleansia.partner.R
import cz.cleansia.partner.core.network.ApiErrorTranslator
import cz.cleansia.core.network.ApiResult
import cz.cleansia.partner.data.profile.ProfileRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import dagger.hilt.android.qualifiers.ApplicationContext
import kotlinx.coroutines.flow.MutableSharedFlow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.SharedFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asSharedFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import javax.inject.Inject

data class PersonalForm(
    val employeeId: String = "",
    val firstName: String = "",
    val lastName: String = "",
    val birthDate: String = "",
    val phone: String = "",
    val email: String = "",
    /** Read-back URL of the stored photo. Lives on the USER row, not the employee record. */
    val profilePhotoUrl: String? = null,
    val firstNameError: String? = null,
    val lastNameError: String? = null,
    val birthDateError: String? = null,
)

sealed interface PersonalSectionUiState {
    data object Loading : PersonalSectionUiState
    data object Error : PersonalSectionUiState
    data class Loaded(val form: PersonalForm) : PersonalSectionUiState
}

@HiltViewModel
class PersonalSectionViewModel @Inject constructor(
    private val profileRepository: ProfileRepository,
    // The photo lives on the USER row, not the employee record, so it is a second repository — the
    // same split ProfileRepository/UserRepository draw everywhere else in this app.
    private val userRepository: UserRepository,
    private val errorTranslator: ApiErrorTranslator,
    private val snackbar: SnackbarController,
    @ApplicationContext private val appContext: Context,
) : ViewModel() {

    private val _uiState = MutableStateFlow<PersonalSectionUiState>(PersonalSectionUiState.Loading)
    val uiState: StateFlow<PersonalSectionUiState> = _uiState.asStateFlow()

    private val _saveState = MutableStateFlow<ActionState>(ActionState.Idle)
    val saveState: StateFlow<ActionState> = _saveState.asStateFlow()

    private val _saved = MutableSharedFlow<Unit>(extraBufferCapacity = 1)
    val saved: SharedFlow<Unit> = _saved.asSharedFlow()

    /**
     * The avatar is a three-way choice, not a nullable field — mirroring the customer app. A pick
     * replaces, a removal clears, and Unchanged says nothing about the photo at all, so a save that is
     * only about a phone number cannot silently delete a photo.
     */
    private val _avatarDraft = MutableStateFlow<PartnerAvatarDraft>(PartnerAvatarDraft.Unchanged)
    val avatarDraft: StateFlow<PartnerAvatarDraft> = _avatarDraft.asStateFlow()

    private val _avatarState = MutableStateFlow<ActionState>(ActionState.Idle)
    val avatarState: StateFlow<ActionState> = _avatarState.asStateFlow()

    /** Compress off the main thread, then stage. Nothing is uploaded until the cleaner saves. */
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
            _avatarDraft.value = PartnerAvatarDraft.Picked(previewUri = uri, image = encoded)
        }
    }

    fun removeAvatar() {
        _avatarDraft.value = PartnerAvatarDraft.Removed
    }

    fun discardAvatarDraft() {
        _avatarDraft.value = PartnerAvatarDraft.Unchanged
    }

    init { load() }

    fun retry() = load()

    private fun load() {
        viewModelScope.launch {
            _uiState.value = PersonalSectionUiState.Loading
            when (val result = profileRepository.getCurrentEmployee()) {
                is ApiResult.Success -> {
                    val e = result.data
                    // The photo is on the user row and the rest is on the employee record, so the
                    // screen needs both. A failure here is not worth failing the screen over — the
                    // form still works and the avatar simply falls back to initials.
                    val photoUrl = (userRepository.getCurrentUser() as? ApiResult.Success)
                        ?.data?.profilePhotoUrl
                    _uiState.value = PersonalSectionUiState.Loaded(
                        PersonalForm(
                            employeeId = e.id.orEmpty(),
                            firstName = e.firstName.orEmpty(),
                            lastName = e.lastName.orEmpty(),
                            birthDate = e.birthDate.orEmpty(),
                            phone = e.phoneNumber.orEmpty(),
                            email = e.email.orEmpty(),
                            profilePhotoUrl = photoUrl,
                        ),
                    )
                }
                is ApiResult.Error -> {
                    snackbar.showError(errorTranslator.translate(result.error))
                    _uiState.value = PersonalSectionUiState.Error
                }
            }
        }
    }

    fun onFirstNameChange(v: String) = updateForm { it.copy(firstName = v, firstNameError = null) }
    fun onLastNameChange(v: String) = updateForm { it.copy(lastName = v, lastNameError = null) }
    fun onBirthDateChange(v: String) = updateForm { it.copy(birthDate = v, birthDateError = null) }
    fun onPhoneChange(v: String) = updateForm { it.copy(phone = v) }

    fun save() {
        val form = (_uiState.value as? PersonalSectionUiState.Loaded)?.form ?: return
        if (_saveState.value is ActionState.Submitting) return
        var hasError = false
        if (form.firstName.isBlank()) {
            updateForm { it.copy(firstNameError = appContext.getString(R.string.error_first_name_required)) }
            hasError = true
        }
        if (form.lastName.isBlank()) {
            updateForm { it.copy(lastNameError = appContext.getString(R.string.error_last_name_required)) }
            hasError = true
        }
        if (form.birthDate.isBlank()) {
            updateForm { it.copy(birthDateError = appContext.getString(R.string.error_birth_date_required)) }
            hasError = true
        }
        if (hasError) return
        if (form.employeeId.isBlank()) {
            snackbar.showError(appContext.getString(R.string.error_profile_not_loaded))
            return
        }

        viewModelScope.launch {
            _saveState.value = ActionState.Submitting
            val result = profileRepository.updatePersonalInfo(
                employeeId = form.employeeId,
                firstName = form.firstName.trim(),
                lastName = form.lastName.trim(),
                birthDate = form.birthDate,
                phone = form.phone.takeIf { it.isNotBlank() },
            )
            when (result) {
                is ApiResult.Success -> {
                    // The employee record is saved; the photo is a second row and a second call. It
                    // runs only when there is something to say about it, so an ordinary save cannot
                    // touch the photo — and a failure here does NOT undo the fields that just saved.
                    val photoResult = pushAvatarDraft(form)
                    _saveState.value = ActionState.Idle
                    if (photoResult is ApiResult.Error) {
                        snackbar.showError(errorTranslator.translate(photoResult.error))
                        return@launch
                    }
                    _avatarDraft.value = PartnerAvatarDraft.Unchanged
                    _saved.emit(Unit)
                }
                is ApiResult.Error -> {
                    _saveState.value = ActionState.Idle
                    snackbar.showError(errorTranslator.translate(result.error))
                }
            }
        }
    }

    /**
     * Null when the draft says nothing — the common case, and the reason an ordinary save makes no
     * photo call at all. The names and phone are replayed because the command is a partial save whose
     * name fields are replaced outright, exactly as `LanguagePreferenceSync` replays them.
     */
    private suspend fun pushAvatarDraft(form: PersonalForm): ApiResult<Unit>? {
        val draft = _avatarDraft.value
        if (draft is PartnerAvatarDraft.Unchanged) return null
        return userRepository.updateCurrentUser(
            firstName = form.firstName.trim(),
            lastName = form.lastName.trim(),
            phoneNumber = form.phone.trim(),
            birthDate = form.birthDate,
            languageCode = null,
            photo = (draft as? PartnerAvatarDraft.Picked)?.image,
            removePhoto = draft is PartnerAvatarDraft.Removed,
        )
    }

    private inline fun updateForm(transform: (PersonalForm) -> PersonalForm) {
        _uiState.update { state ->
            if (state is PersonalSectionUiState.Loaded) state.copy(form = transform(state.form)) else state
        }
    }
}

/** What the cleaner has said about their photo, if anything, since the screen opened. */
sealed interface PartnerAvatarDraft {
    data object Unchanged : PartnerAvatarDraft
    data object Removed : PartnerAvatarDraft
    data class Picked(val previewUri: Uri, val image: Base64Image) : PartnerAvatarDraft
}
