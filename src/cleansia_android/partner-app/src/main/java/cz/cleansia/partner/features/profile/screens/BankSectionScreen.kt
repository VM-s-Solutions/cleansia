package cz.cleansia.partner.features.profile.screens

import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.height
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.stringResource
import androidx.hilt.navigation.compose.hiltViewModel
import cz.cleansia.core.ui.components.CleansiaPrimaryButton
import cz.cleansia.core.ui.components.CleansiaTextField
import cz.cleansia.core.ui.theme.Spacing
import cz.cleansia.partner.R
import cz.cleansia.partner.features.orders.viewmodels.OnboardingChainViewModel
import cz.cleansia.partner.features.orders.viewmodels.ProfileSection
import cz.cleansia.partner.features.profile.components.FormSectionCard
import cz.cleansia.partner.features.profile.components.OnboardingChainHeader
import cz.cleansia.partner.features.profile.components.SectionScaffold
import cz.cleansia.partner.features.profile.viewmodels.BankSectionViewModel

@Composable
fun BankSectionScreen(
    onNavigateBack: () -> Unit,
    onSaved: () -> Unit,
    onboarding: Boolean = false,
    viewModel: BankSectionViewModel = hiltViewModel(),
    chainViewModel: OnboardingChainViewModel = hiltViewModel(),
) {
    val uiState by viewModel.uiState.collectAsState()
    val chainState by chainViewModel.state.collectAsState()

    LaunchedEffect(uiState.isSaved) { if (uiState.isSaved) onSaved() }

    SectionScaffold(
        title = stringResource(R.string.bank_details),
        isLoading = uiState.isLoading,
        onNavigateBack = onNavigateBack,
        headerSlot = if (!onboarding) null else ({
            OnboardingChainHeader(
                currentSection = ProfileSection.Bank,
                state = chainState,
            )
        }),
    ) {
        FormSectionCard(title = stringResource(R.string.bank_details)) {
            CleansiaTextField(
                value = uiState.iban,
                onValueChange = viewModel::onIbanChange,
                label = stringResource(R.string.iban),
                errorText = uiState.ibanError,
                enabled = !uiState.isSaving,
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
            loading = uiState.isSaving,
            enabled = uiState.iban.isNotBlank() && !uiState.isSaving,
        )
    }
}
