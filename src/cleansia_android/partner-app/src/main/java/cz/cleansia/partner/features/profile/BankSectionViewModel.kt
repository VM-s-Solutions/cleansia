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

data class BankForm(
    val employeeId: String = "",
    val iban: String = "",
    val ibanError: String? = null,
)

sealed interface BankSectionUiState {
    data object Loading : BankSectionUiState
    data object Error : BankSectionUiState
    data class Loaded(val form: BankForm) : BankSectionUiState
}

@HiltViewModel
class BankSectionViewModel @Inject constructor(
    private val profileRepository: ProfileRepository,
    private val errorTranslator: ApiErrorTranslator,
    private val snackbar: SnackbarController,
    @ApplicationContext private val appContext: Context,
) : ViewModel() {

    private val _uiState = MutableStateFlow<BankSectionUiState>(BankSectionUiState.Loading)
    val uiState: StateFlow<BankSectionUiState> = _uiState.asStateFlow()

    private val _saveState = MutableStateFlow<ActionState>(ActionState.Idle)
    val saveState: StateFlow<ActionState> = _saveState.asStateFlow()

    private val _saved = MutableSharedFlow<Unit>(extraBufferCapacity = 1)
    val saved: SharedFlow<Unit> = _saved.asSharedFlow()

    init { load() }

    private fun load() {
        viewModelScope.launch {
            _uiState.value = BankSectionUiState.Loading
            when (val result = profileRepository.getCurrentEmployee()) {
                is ApiResult.Success -> {
                    val e = result.data
                    _uiState.value = BankSectionUiState.Loaded(
                        BankForm(employeeId = e.id.orEmpty(), iban = e.iban.orEmpty()),
                    )
                }
                is ApiResult.Error -> {
                    snackbar.showError(errorTranslator.translate(result.error))
                    _uiState.value = BankSectionUiState.Error
                }
            }
        }
    }

    fun onIbanChange(v: String) = updateForm {
        it.copy(iban = v.uppercase().filter { ch -> ch.isLetterOrDigit() }, ibanError = null)
    }

    fun save() {
        val form = (_uiState.value as? BankSectionUiState.Loaded)?.form ?: return
        if (_saveState.value is ActionState.Submitting) return
        if (form.iban.isBlank()) {
            updateForm { it.copy(ibanError = appContext.getString(R.string.error_iban_required)) }
            return
        }
        if (form.employeeId.isBlank()) {
            snackbar.showError(appContext.getString(R.string.error_profile_not_loaded))
            return
        }
        viewModelScope.launch {
            _saveState.value = ActionState.Submitting
            when (val result = profileRepository.updateBankDetails(form.employeeId, form.iban.trim())) {
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

    private inline fun updateForm(transform: (BankForm) -> BankForm) {
        _uiState.update { state ->
            if (state is BankSectionUiState.Loaded) state.copy(form = transform(state.form)) else state
        }
    }
}
