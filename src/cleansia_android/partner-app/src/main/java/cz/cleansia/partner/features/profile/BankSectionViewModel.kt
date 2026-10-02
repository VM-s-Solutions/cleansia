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
 * The bank countries whose accounts are entered as `prefix – number / bank code`: the server's
 * CzskDomesticWithIban scheme, which derives the IBAN from those parts (ADR-0034 D5.2). A bank in any
 * other country is entered as one IBAN (owner ruling 2026-10-02). The paste splitter in
 * CleansiaBankAccountInput reads a CZ or SK IBAN into the parts for the same two countries.
 */
private val DomesticAccountCountries = setOf("CZ", "SK")

/**
 * The payout destination. A Czech or Slovak cleaner enters the parts they read off their statement —
 * prefix, account number, bank code — and the server derives the IBAN, so everything about those
 * parts (the account checksum, the bank code, a card number typed in) is left to it, and its answers
 * are wired to `error_validation_payout_*`. Any other bank country takes one IBAN, checked here as
 * the server's IbanCalculator checks it ([ibanProblem]) so a typo is named before the round trip;
 * the server still checks it again.
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
    /** Whether a save has been tried, after which the IBAN's problem shows under it as it is edited. */
    val ibanChecked: Boolean = false,
) {
    /** The bank country's ISO code, which its IBANs start with. */
    val bankCountryAlpha2: String?
        get() = countries.firstOrNull { it.id == bankCountryId }?.isoAlpha2?.trim()?.uppercase()?.ifEmpty { null }

    /**
     * Whether the account is entered as the three domestic parts rather than an IBAN: a CZ or SK
     * bank, and a country the list cannot name (no country picked yet, or the list failed to load).
     */
    val usesDomesticAccount: Boolean
        get() = bankCountryAlpha2.let { it == null || it in DomesticAccountCountries }

    /** What the server would refuse about the IBAN; null when it would take it, or for a domestic account. */
    val ibanProblem: IbanProblem?
        get() = bankCountryAlpha2?.takeUnless { usesDomesticAccount }?.let { ibanProblem(iban, it) }

    /** The bank's country plus the account in the form that country takes. */
    val canSubmit: Boolean
        get() = !bankCountryId.isNullOrBlank() &&
            (if (usesDomesticAccount) accountNumber.isNotBlank() else iban.isNotBlank())
}

/** Why the server's IbanCalculator would refuse an IBAN for the bank country picked (ADR-0034 D4). */
sealed interface IbanProblem {
    /** Not an IBAN's shape, or its check digits do not add up: the server's `validation.payout.invalid_iban`. */
    data object Invalid : IbanProblem

    /** Another country's IBAN: the server's `validation.payout.iban_country_mismatch`. */
    data object OtherCountry : IbanProblem

    /** The country's IBANs are [expected] characters long. The server folds this into `invalid_iban`. */
    data class WrongLength(val expected: Int) : IbanProblem
}

/**
 * ISO 13616 registry lengths, the server's `IbanCalculator.RegistryLengths` entry for entry
 * (BankSectionViewModelTest reads the C# table). A country absent from it takes the generic 15–34.
 */
internal val IbanRegistryLengths = mapOf(
    "AT" to 20, "BE" to 16, "BG" to 22, "CH" to 21, "CY" to 28, "CZ" to 24, "DE" to 22,
    "DK" to 18, "EE" to 20, "ES" to 24, "FI" to 18, "FR" to 27, "GB" to 22, "GR" to 27,
    "HR" to 21, "HU" to 28, "IE" to 22, "IT" to 27, "LT" to 20, "LU" to 20, "LV" to 21,
    "MT" to 31, "NL" to 18, "NO" to 15, "PL" to 28, "PT" to 25, "RO" to 24, "SE" to 24,
    "SI" to 19, "SK" to 24, "UA" to 29,
)

/** The longest IBAN ISO 13616 allows, and the field's cap. */
internal const val IbanMaxLength = 34

/**
 * What the server would refuse about [iban] for a bank in [bankCountryAlpha2], in the order a cleaner
 * can act on it: another country's IBAN first, then a length that is not the country's, then the
 * shape and the ISO 7064 mod-97 check digits. Null when the server's IbanCalculator would take it.
 * The iOS and web twins are held to the same cases.
 */
internal fun ibanProblem(iban: String, bankCountryAlpha2: String): IbanProblem? {
    val value = iban.asBankReference()
    if (value.length < 2 || value[0] !in 'A'..'Z' || value[1] !in 'A'..'Z') return IbanProblem.Invalid
    val country = value.take(2)
    if (country != bankCountryAlpha2.uppercase()) return IbanProblem.OtherCountry
    val expected = IbanRegistryLengths[country]
    if (expected != null && value.length != expected) return IbanProblem.WrongLength(expected)
    val shaped = value.length in 15..IbanMaxLength &&
        value[2] in '0'..'9' && value[3] in '0'..'9' &&
        value.all { it in 'A'..'Z' || it in '0'..'9' }
    if (!shaped) return IbanProblem.Invalid
    return if (mod97(value.drop(4) + value.take(4)) == 1) null else IbanProblem.Invalid
}

/** ISO 7064 MOD 97-10 over an IBAN rearranged to `BBAN + country + check digits`, A = 10 … Z = 35. */
private fun mod97(value: String): Int = value.fold(0) { remainder, character ->
    if (character in '0'..'9') {
        (remainder * 10 + (character - '0')) % 97
    } else {
        (remainder * 100 + (character - 'A' + 10)) % 97
    }
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

    fun onIbanChange(v: String) = updateForm { it.copy(iban = v.asBankReference().take(IbanMaxLength)) }

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
        if (form.ibanProblem != null) {
            updateForm { it.copy(ibanChecked = true) }
            return
        }

        // Each scheme sends only its own identifier. A domestic IBAN is the server's to derive, and the
        // stored one sent back with edited parts was refused as a mismatch; an IBAN account has no parts.
        val domestic = form.usesDomesticAccount
        viewModelScope.launch {
            _saveState.value = ActionState.Submitting
            val result = profileRepository.updateBankDetails(
                employeeId = form.employeeId,
                bankCountryId = form.bankCountryId,
                accountPrefix = form.accountPrefix.takeIf { domestic }?.trimmedOrNull(),
                accountNumber = form.accountNumber.takeIf { domestic }?.trimmedOrNull(),
                bankCode = form.bankCode.takeIf { domestic }?.trimmedOrNull(),
                iban = form.iban.takeUnless { domestic }?.trimmedOrNull(),
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
