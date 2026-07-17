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
            )
        }),
    ) {
        val form = (uiState as? BankSectionUiState.Loaded)?.form ?: BankForm()
        FormSectionCard(title = stringResource(R.string.bank_details)) {
            CleansiaTextField(
                value = form.iban,
                onValueChange = viewModel::onIbanChange,
                label = stringResource(R.string.iban),
                errorText = form.ibanError,
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
            enabled = form.iban.isNotBlank() && !saving,
        )
    }
}
