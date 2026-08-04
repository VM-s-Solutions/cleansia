package cz.cleansia.partner.features.profile

import android.content.Context
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.network.safeApiCall
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.core.ui.state.ActionState
import cz.cleansia.partner.R
import cz.cleansia.partner.api.client.CountryApi
import cz.cleansia.partner.api.model.CountryListItem
import cz.cleansia.partner.core.network.ApiErrorTranslator
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
import kotlinx.serialization.json.Json
import javax.inject.Inject

/**
 * The payout destination is captured as the parts a Czech or Slovak cleaner reads off their
 * statement — prefix, account number, bank code — because the server derives the IBAN from
 * them. Everything the server can check (the account checksum, the bank code, whether a
 * supplied IBAN agrees, whether a card number was typed in) is left to the server: it owns
 * the rules, its answers are wired to `error_validation_payout_*`, and a second copy of a
 * checksum here would gate a cleaner's income on a client that disagrees.
 */
data class BankForm(
    val employeeId: String = "",
    val countries: List<CountryListItem> = emptyList(),
    val bankCountryId: String? = null,
    val accountPrefix: String = "",
    val accountNumber: String = "",
    val bankCode: String = "",
    val iban: String = "",
    val swift: String = "",
    val bankName: String = "",
    val holderName: String = "",
) {
    /** The bank's country plus something that identifies the account — the rest is the server's call. */
    val canSubmit: Boolean
        get() = !bankCountryId.isNullOrBlank() && (accountNumber.isNotBlank() || iban.isNotBlank())
}

sealed interface BankSectionUiState {
    data object Loading : BankSectionUiState
    data object Error : BankSectionUiState
    data class Loaded(val form: BankForm) : BankSectionUiState
}

@HiltViewModel
class BankSectionViewModel @Inject constructor(
    private val profileRepository: ProfileRepository,
    private val countryApi: CountryApi,
    private val errorTranslator: ApiErrorTranslator,
    private val snackbar: SnackbarController,
    private val json: Json,
    @ApplicationContext private val appContext: Context,
) : ViewModel() {

    private val _uiState = MutableStateFlow<BankSectionUiState>(BankSectionUiState.Loading)
    val uiState: StateFlow<BankSectionUiState> = _uiState.asStateFlow()

    private val _saveState = MutableStateFlow<ActionState>(ActionState.Idle)
    val saveState: StateFlow<ActionState> = _saveState.asStateFlow()

    private val _saved = MutableSharedFlow<Unit>(extraBufferCapacity = 1)
    val saved: SharedFlow<Unit> = _saved.asSharedFlow()

    init { load() }

    fun retry() = load()

    private fun load() {
        viewModelScope.launch {
            _uiState.value = BankSectionUiState.Loading
            val countries = (safeApiCall(json) { countryApi.countryGetOverview() } as? ApiResult.Success)
                ?.data.orEmpty()

            val employee = when (val result = profileRepository.getCurrentEmployee()) {
                is ApiResult.Success -> result.data
                is ApiResult.Error -> return@launch failLoad(errorTranslator.translate(result.error))
            }
            val payout = when (val result = profileRepository.getPayoutDetails()) {
                is ApiResult.Success -> result.data
                is ApiResult.Error -> return@launch failLoad(errorTranslator.translate(result.error))
            }

            _uiState.value = BankSectionUiState.Loaded(
                BankForm(
                    employeeId = employee.id.orEmpty(),
                    countries = countries,
                    // The bank is usually in the country the cleaner lives in, so pre-fill it and
                    // let them change it — the same zero-tap default the identification section uses.
                    bankCountryId = payout?.bankCountryId ?: employee.countryId,
                    accountPrefix = payout?.accountPrefix.withoutPayoutPadding(),
                    accountNumber = payout?.accountNumber.withoutPayoutPadding(),
                    bankCode = payout?.bankCode.orEmpty(),
                    iban = payout?.iban.orEmpty(),
                    swift = payout?.swift.orEmpty(),
                    bankName = payout?.bankName.orEmpty(),
                    holderName = payout?.holderName.orEmpty(),
                ),
            )
        }
    }

    private fun failLoad(message: String) {
        snackbar.showError(message)
        _uiState.value = BankSectionUiState.Error
    }

    fun onBankCountrySelected(id: String) = updateForm { it.copy(bankCountryId = id) }

    fun onAccountPrefixChange(v: String) = updateForm { it.copy(accountPrefix = v.digitsOnly()) }

    fun onAccountNumberChange(v: String) = updateForm { it.copy(accountNumber = v.digitsOnly()) }

    fun onBankCodeChange(v: String) = updateForm { it.copy(bankCode = v.digitsOnly()) }

    fun onIbanChange(v: String) = updateForm { it.copy(iban = v.asBankReference()) }

    fun onSwiftChange(v: String) = updateForm { it.copy(swift = v.asBankReference()) }

    fun onBankNameChange(v: String) = updateForm { it.copy(bankName = v) }

    fun onHolderNameChange(v: String) = updateForm { it.copy(holderName = v) }

    fun save() {
        val form = (_uiState.value as? BankSectionUiState.Loaded)?.form ?: return
        if (_saveState.value is ActionState.Submitting) return
        if (form.employeeId.isBlank()) {
            snackbar.showError(appContext.getString(R.string.error_profile_not_loaded))
            return
        }
        if (!form.canSubmit) return

        viewModelScope.launch {
            _saveState.value = ActionState.Submitting
            val result = profileRepository.updateBankDetails(
                employeeId = form.employeeId,
                bankCountryId = form.bankCountryId,
                accountPrefix = form.accountPrefix.trimmedOrNull(),
                accountNumber = form.accountNumber.trimmedOrNull(),
                bankCode = form.bankCode.trimmedOrNull(),
                iban = form.iban.trimmedOrNull(),
                swift = form.swift.trimmedOrNull(),
                bankName = form.bankName.trimmedOrNull(),
                holderName = form.holderName.trimmedOrNull(),
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

    private inline fun updateForm(transform: (BankForm) -> BankForm) {
        _uiState.update { state ->
            if (state is BankSectionUiState.Loaded) state.copy(form = transform(state.form)) else state
        }
    }
}

private fun String.digitsOnly(): String = filter { it.isDigit() }

private fun String.asBankReference(): String = uppercase().filter { it.isLetterOrDigit() }

private fun String.trimmedOrNull(): String? = trim().ifBlank { null }
