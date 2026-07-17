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
import cz.cleansia.core.ui.components.CleansiaPhoneInput
import cz.cleansia.core.ui.components.CleansiaPrimaryButton
import cz.cleansia.core.ui.components.CleansiaTextField
import cz.cleansia.core.ui.state.ActionState
import cz.cleansia.core.ui.theme.Spacing
import cz.cleansia.partner.R

@Composable
fun EmergencySectionScreen(
    onNavigateBack: () -> Unit,
    onSaved: () -> Unit,
    viewModel: EmergencySectionViewModel = hiltViewModel(),
) {
    val uiState by viewModel.uiState.collectAsStateWithLifecycle()
    val saveState by viewModel.saveState.collectAsStateWithLifecycle()
    val saving = saveState is ActionState.Submitting

    LaunchedEffect(viewModel) { viewModel.saved.collect { onSaved() } }

    SectionScaffold(
        title = stringResource(R.string.emergency_contact),
        isLoading = uiState is EmergencySectionUiState.Loading,
        isError = uiState is EmergencySectionUiState.Error,
        onRetry = viewModel::retry,
        onNavigateBack = onNavigateBack,
    ) {
        val form = (uiState as? EmergencySectionUiState.Loaded)?.form ?: EmergencyForm()
        FormSectionCard(title = stringResource(R.string.emergency_contact)) {
            CleansiaTextField(
                value = form.name,
                onValueChange = viewModel::onNameChange,
                label = stringResource(R.string.emergency_name),
                errorText = form.nameError,
                enabled = !saving,
                transparentContainer = true,
            )
            Spacer(Modifier.height(Spacing.XS))
            CleansiaPhoneInput(
                value = form.phone,
                onValueChange = viewModel::onPhoneChange,
                label = stringResource(R.string.emergency_phone),
                errorText = form.phoneError,
                enabled = !saving,
                transparentContainer = true,
            )
        }
        Spacer(Modifier.height(Spacing.L))
        CleansiaPrimaryButton(
            text = stringResource(R.string.save),
            onClick = { viewModel.save() },
            loading = saving,
            enabled = form.name.isNotBlank() && form.phone.isNotBlank() && !saving,
        )
    }
}
