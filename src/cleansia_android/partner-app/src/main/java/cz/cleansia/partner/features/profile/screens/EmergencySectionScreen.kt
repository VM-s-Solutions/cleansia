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
import cz.cleansia.core.ui.components.CleansiaPhoneInput
import cz.cleansia.core.ui.components.CleansiaPrimaryButton
import cz.cleansia.core.ui.components.CleansiaTextField
import cz.cleansia.core.ui.theme.Spacing
import cz.cleansia.partner.R
import cz.cleansia.partner.features.profile.components.FormSectionCard
import cz.cleansia.partner.features.profile.components.SectionScaffold
import cz.cleansia.partner.features.profile.viewmodels.EmergencySectionViewModel

@Composable
fun EmergencySectionScreen(
    onNavigateBack: () -> Unit,
    onSaved: () -> Unit,
    viewModel: EmergencySectionViewModel = hiltViewModel(),
) {
    val uiState by viewModel.uiState.collectAsState()

    LaunchedEffect(uiState.isSaved) { if (uiState.isSaved) onSaved() }

    SectionScaffold(
        title = stringResource(R.string.emergency_contact),
        isLoading = uiState.isLoading,
        onNavigateBack = onNavigateBack,
    ) {
        FormSectionCard(title = stringResource(R.string.emergency_contact)) {
            CleansiaTextField(
                value = uiState.name,
                onValueChange = viewModel::onNameChange,
                label = stringResource(R.string.emergency_name),
                errorText = uiState.nameError,
                enabled = !uiState.isSaving,
                transparentContainer = true,
            )
            Spacer(Modifier.height(Spacing.XS))
            CleansiaPhoneInput(
                value = uiState.phone,
                onValueChange = viewModel::onPhoneChange,
                label = stringResource(R.string.emergency_phone),
                errorText = uiState.phoneError,
                enabled = !uiState.isSaving,
                transparentContainer = true,
            )
        }
        Spacer(Modifier.height(Spacing.L))
        CleansiaPrimaryButton(
            text = stringResource(R.string.save),
            onClick = { viewModel.save() },
            loading = uiState.isSaving,
            enabled = uiState.name.isNotBlank() && uiState.phone.isNotBlank() && !uiState.isSaving,
        )
    }
}

