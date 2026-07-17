package cz.cleansia.partner.features.profile

import android.content.Context
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

    init { load() }

    fun retry() = load()

    private fun load() {
        viewModelScope.launch {
            _uiState.value = PersonalSectionUiState.Loading
            when (val result = profileRepository.getCurrentEmployee()) {
                is ApiResult.Success -> {
                    val e = result.data
                    _uiState.value = PersonalSectionUiState.Loaded(
                        PersonalForm(
                            employeeId = e.id.orEmpty(),
                            firstName = e.firstName.orEmpty(),
                            lastName = e.lastName.orEmpty(),
                            birthDate = e.birthDate.orEmpty(),
                            phone = e.phoneNumber.orEmpty(),
                            email = e.email.orEmpty(),
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
    fun onEmailChange(v: String) = updateForm { it.copy(email = v) }

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
                email = form.email.takeIf { it.isNotBlank() },
            )
            when (result) {
                is ApiResult.Success -> {
                    _saveState.value = ActionState.Idle
                    _saved.emit(Unit)
                }
                is ApiResult.Error -> {
                    _saveState.value = ActionState.Idle
                    snackbar.showError(errorTranslator.translate(result.error))
                }
            }
        }
    }

    private inline fun updateForm(transform: (PersonalForm) -> PersonalForm) {
        _uiState.update { state ->
            if (state is PersonalSectionUiState.Loaded) state.copy(form = transform(state.form)) else state
        }
    }
}
