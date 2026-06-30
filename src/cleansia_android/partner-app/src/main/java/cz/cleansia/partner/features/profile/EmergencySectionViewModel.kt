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

data class EmergencyForm(
    val employeeId: String = "",
    val name: String = "",
    val phone: String = "",
    val nameError: String? = null,
    val phoneError: String? = null,
)

sealed interface EmergencySectionUiState {
    data object Loading : EmergencySectionUiState
    data object Error : EmergencySectionUiState
    data class Loaded(val form: EmergencyForm) : EmergencySectionUiState
}

@HiltViewModel
class EmergencySectionViewModel @Inject constructor(
    private val profileRepository: ProfileRepository,
    private val errorTranslator: ApiErrorTranslator,
    private val snackbar: SnackbarController,
    @ApplicationContext private val appContext: Context,
) : ViewModel() {

    private val _uiState = MutableStateFlow<EmergencySectionUiState>(EmergencySectionUiState.Loading)
    val uiState: StateFlow<EmergencySectionUiState> = _uiState.asStateFlow()

    private val _saveState = MutableStateFlow<ActionState>(ActionState.Idle)
    val saveState: StateFlow<ActionState> = _saveState.asStateFlow()

    private val _saved = MutableSharedFlow<Unit>(extraBufferCapacity = 1)
    val saved: SharedFlow<Unit> = _saved.asSharedFlow()

    init { load() }

    private fun load() {
        viewModelScope.launch {
            _uiState.value = EmergencySectionUiState.Loading
            when (val result = profileRepository.getCurrentEmployee()) {
                is ApiResult.Success -> {
                    val e = result.data
                    _uiState.value = EmergencySectionUiState.Loaded(
                        EmergencyForm(
                            employeeId = e.id.orEmpty(),
                            name = e.emergencyContactName.orEmpty(),
                            phone = e.emergencyContactPhone.orEmpty(),
                        ),
                    )
                }
                is ApiResult.Error -> {
                    snackbar.showError(errorTranslator.translate(result.error))
                    _uiState.value = EmergencySectionUiState.Error
                }
            }
        }
    }

    fun onNameChange(v: String) = updateForm { it.copy(name = v, nameError = null) }
    fun onPhoneChange(v: String) = updateForm { it.copy(phone = v, phoneError = null) }

    fun save() {
        val form = (_uiState.value as? EmergencySectionUiState.Loaded)?.form ?: return
        if (_saveState.value is ActionState.Submitting) return
        var hasError = false
        if (form.name.isBlank()) {
            updateForm { it.copy(nameError = appContext.getString(R.string.error_name_required)) }
            hasError = true
        }
        if (form.phone.isBlank()) {
            updateForm { it.copy(phoneError = appContext.getString(R.string.error_phone_required)) }
            hasError = true
        }
        if (hasError) return
        if (form.employeeId.isBlank()) {
            snackbar.showError(appContext.getString(R.string.error_profile_not_loaded))
            return
        }
        viewModelScope.launch {
            _saveState.value = ActionState.Submitting
            val result = profileRepository.updateEmergencyContact(
                employeeId = form.employeeId,
                emergencyName = form.name.trim(),
                emergencyPhone = form.phone.trim(),
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

    private inline fun updateForm(transform: (EmergencyForm) -> EmergencyForm) {
        _uiState.update { state ->
            if (state is EmergencySectionUiState.Loaded) state.copy(form = transform(state.form)) else state
        }
    }
}
