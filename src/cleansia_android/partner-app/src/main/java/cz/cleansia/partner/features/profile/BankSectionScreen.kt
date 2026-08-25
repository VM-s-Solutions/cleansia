package cz.cleansia.partner.features.profile

import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.height
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.stringResource
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import cz.cleansia.core.ui.components.CleansiaPrimaryButton
import cz.cleansia.core.ui.components.CleansiaBankAccountInput
import cz.cleansia.core.ui.components.CleansiaTextField
import cz.cleansia.core.ui.state.ActionState
import cz.cleansia.core.ui.theme.Spacing
import cz.cleansia.partner.R
import cz.cleansia.partner.features.orders.OnboardingChainViewModel
import cz.cleansia.partner.features.orders.ProfileSection

@Composable
fun BankSectionScreen(
    onNavigateBack: () -> Unit,
    onSaved: () -> Unit,
    onboarding: Boolean = false,
    /**
     * Tapping a completed step dot in the onboarding header. Defaulted to a no-op because the same
     * screen is reachable from the profile menu, where there is no chain to jump around in.
     */
    onJumpToSection: (cz.cleansia.partner.features.orders.ProfileSection) -> Unit = {},
    viewModel: BankSectionViewModel = hiltViewModel(),
    chainViewModel: OnboardingChainViewModel = hiltViewModel(),
) {
    val uiState by viewModel.uiState.collectAsStateWithLifecycle()
    val saveState by viewModel.saveState.collectAsStateWithLifecycle()
    val chainState by chainViewModel.state.collectAsStateWithLifecycle()
    val saving = saveState is ActionState.Submitting

    LaunchedEffect(viewModel) { viewModel.saved.collect { onSaved() } }

    SectionScaffold(
        title = stringResource(R.string.bank_details),
        isLoading = uiState is BankSectionUiState.Loading,
        isError = uiState is BankSectionUiState.Error,
        onRetry = viewModel::retry,
        onNavigateBack = onNavigateBack,
        headerSlot = if (!onboarding) null else ({
            OnboardingChainHeader(
                currentSection = ProfileSection.Bank,
                state = chainState,
                onSelect = onJumpToSection,
            )
        }),
    ) {
        val form = (uiState as? BankSectionUiState.Loaded)?.form ?: BankForm()
        val countryOptions = form.countries.map { country ->
            country.id.orEmpty() to country.localizedName()
        }

        FormSectionCard(title = stringResource(R.string.bank_details)) {
            PickerDropdown(
                selectedId = form.bankCountryId,
                options = countryOptions,
                onSelected = viewModel::onBankCountrySelected,
                label = stringResource(R.string.bank_country),
                enabled = !saving,
                searchable = true,
            )
            Spacer(Modifier.height(Spacing.XS))
            // One control, three segments — the account is a single thing to the cleaner typing it.
            CleansiaBankAccountInput(
                prefix = form.accountPrefix,
                number = form.accountNumber,
                bankCode = form.bankCode,
                onPrefixChange = viewModel::onAccountPrefixChange,
                onNumberChange = viewModel::onAccountNumberChange,
                onBankCodeChange = viewModel::onBankCodeChange,
                label = stringResource(R.string.bank_account),
                prefixPlaceholder = stringResource(R.string.bank_account_prefix_placeholder),
                numberPlaceholder = stringResource(R.string.bank_account_number_placeholder),
                bankCodePlaceholder = stringResource(R.string.bank_code_placeholder),
                helper = stringResource(R.string.bank_account_helper),
                enabled = !saving,
            )
            Spacer(Modifier.height(Spacing.XS))
            CleansiaTextField(
                value = form.iban,
                onValueChange = viewModel::onIbanChange,
                label = stringResource(R.string.iban),
                helper = stringResource(R.string.iban_helper),
                enabled = !saving,
                transparentContainer = true,
            )
            Spacer(Modifier.height(Spacing.XS))
            CleansiaTextField(
                value = form.swift,
                onValueChange = viewModel::onSwiftChange,
                label = stringResource(R.string.swift_code),
                helper = stringResource(R.string.swift_code_helper),
                enabled = !saving,
                transparentContainer = true,
            )
            Spacer(Modifier.height(Spacing.XS))
            CleansiaTextField(
                value = form.bankName,
                onValueChange = viewModel::onBankNameChange,
                label = stringResource(R.string.bank_name),
                enabled = !saving,
                transparentContainer = true,
            )
            Spacer(Modifier.height(Spacing.XS))
            CleansiaTextField(
                value = form.holderName,
                onValueChange = viewModel::onHolderNameChange,
                label = stringResource(R.string.bank_account_holder),
                helper = stringResource(R.string.bank_account_holder_helper),
                enabled = !saving,
                transparentContainer = true,
            )
        }
        Spacer(Modifier.height(Spacing.L))
        // Bank is the last step in the chain — Save returns to the lock,
        // so "Save and continue" would lie about there being more. Use
        // the plain "Save" label even in onboarding mode.
        CleansiaPrimaryButton(
            text = stringResource(R.string.save),
            onClick = { viewModel.save() },
            loading = saving,
            enabled = form.canSubmit && !saving,
        )
    }
}
