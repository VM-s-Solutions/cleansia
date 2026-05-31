package cz.cleansia.partner.features.profile.viewmodels

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.partner.core.network.ApiErrorTranslator
import cz.cleansia.partner.core.network.ApiResult
import cz.cleansia.partner.data.profile.ProfileRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import javax.inject.Inject

data class BankSectionUiState(
    val isLoading: Boolean = false,
    val isSaving: Boolean = false,
    val employeeId: String = "",
    val iban: String = "",
    val ibanError: String? = null,
    val error: String? = null,
    val isSaved: Boolean = false,
)

@HiltViewModel
class BankSectionViewModel @Inject constructor(
    private val profileRepository: ProfileRepository,
    private val errorTranslator: ApiErrorTranslator,
    private val snackbar: SnackbarController,
) : ViewModel() {

    private val _uiState = MutableStateFlow(BankSectionUiState())
    val uiState: StateFlow<BankSectionUiState> = _uiState.asStateFlow()

    init { load() }

    private fun load() {
        viewModelScope.launch {
            _uiState.update { it.copy(isLoading = true) }
            when (val result = profileRepository.getCurrentEmployee()) {
                is ApiResult.Success -> {
                    val e = result.data
                    _uiState.update { it.copy(isLoading = false, employeeId = e.id.orEmpty(), iban = e.iban.orEmpty()) }
                }
                is ApiResult.Error -> {
                    _uiState.update { it.copy(isLoading = false) }
                    snackbar.showError(errorTranslator.translate(result.error))
                }
            }
        }
    }

    fun onIbanChange(v: String) = _uiState.update {
        it.copy(iban = v.uppercase().filter { ch -> ch.isLetterOrDigit() }, ibanError = null)
    }

    fun save() {
        val state = _uiState.value
        if (state.iban.isBlank()) {
            _uiState.update { it.copy(ibanError = "IBAN is required") }
            return
        }
        if (state.employeeId.isBlank()) {
            snackbar.showError("Profile not loaded yet")
            return
        }
        viewModelScope.launch {
            _uiState.update { it.copy(isSaving = true) }
            when (val result = profileRepository.updateBankDetails(state.employeeId, state.iban.trim())) {
                is ApiResult.Success -> _uiState.update { it.copy(isSaving = false, isSaved = true) }
                is ApiResult.Error -> {
                    _uiState.update { it.copy(isSaving = false) }
                    snackbar.showError(errorTranslator.translate(result.error))
                }
            }
        }
    }

    fun clearError() = _uiState.update { it.copy(error = null) }
}
