package cz.cleansia.partner.features.profile

import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.width
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Slider
import androidx.compose.material3.Switch
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.tooling.preview.Preview
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import cz.cleansia.core.ui.components.CleansiaPrimaryButton
import cz.cleansia.core.ui.state.ActionState
import cz.cleansia.core.ui.theme.Spacing
import cz.cleansia.partner.R
import cz.cleansia.partner.ui.theme.CleansiaPartnerTheme
import kotlin.math.roundToInt

@Composable
fun JobRadiusScreen(
    onNavigateBack: () -> Unit,
    onSaved: () -> Unit,
    viewModel: JobRadiusViewModel = hiltViewModel(),
) {
    val uiState by viewModel.uiState.collectAsStateWithLifecycle()
    val saveState by viewModel.saveState.collectAsStateWithLifecycle()

    LaunchedEffect(viewModel) { viewModel.saved.collect { onSaved() } }

    JobRadiusScreenContent(
        uiState = uiState,
        saving = saveState is ActionState.Submitting,
        onNavigateBack = onNavigateBack,
        onRetry = viewModel::retry,
        onLimitEnabledChange = viewModel::onLimitEnabledChange,
        onRadiusChange = viewModel::onRadiusChange,
        onSave = viewModel::save,
    )
}

@Composable
fun JobRadiusScreenContent(
    uiState: JobRadiusUiState,
    saving: Boolean,
    onNavigateBack: () -> Unit,
    onRetry: () -> Unit,
    onLimitEnabledChange: (Boolean) -> Unit,
    onRadiusChange: (Int) -> Unit,
    onSave: () -> Unit,
) {
    SectionScaffold(
        title = stringResource(R.string.job_radius_title),
        isLoading = uiState is JobRadiusUiState.Loading,
        isError = uiState is JobRadiusUiState.Error,
        onRetry = onRetry,
        onNavigateBack = onNavigateBack,
    ) {
        val form = (uiState as? JobRadiusUiState.Loaded)?.form ?: JobRadiusForm()
        FormSectionCard(title = stringResource(R.string.job_radius_title)) {
            LimitToggleRow(
                enabled = form.limitEnabled,
                interactive = !saving,
                onChange = onLimitEnabledChange,
            )
            Spacer(Modifier.height(Spacing.S))
            if (form.limitEnabled) {
                RadiusSlider(
                    radiusKm = form.radiusKm,
                    interactive = !saving,
                    onRadiusChange = onRadiusChange,
                )
            } else {
                Text(
                    text = stringResource(R.string.job_radius_limit_off_hint),
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
            }
        }
        Spacer(Modifier.height(Spacing.M))
        Text(
            text = stringResource(R.string.job_radius_explainer),
            style = MaterialTheme.typography.bodySmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
        Spacer(Modifier.height(Spacing.L))
        CleansiaPrimaryButton(
            text = stringResource(R.string.save),
            onClick = onSave,
            loading = saving,
            enabled = uiState is JobRadiusUiState.Loaded && !saving,
        )
    }
}

@Composable
private fun LimitToggleRow(
    enabled: Boolean,
    interactive: Boolean,
    onChange: (Boolean) -> Unit,
) {
    Row(
        modifier = Modifier.fillMaxWidth(),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Text(
            text = stringResource(R.string.job_radius_limit_label),
            style = MaterialTheme.typography.bodyLarge,
            color = MaterialTheme.colorScheme.onSurface,
            modifier = Modifier.weight(1f),
        )
        Spacer(Modifier.width(Spacing.M))
        Switch(checked = enabled, onCheckedChange = onChange, enabled = interactive)
    }
}

@Composable
private fun RadiusSlider(
    radiusKm: Int,
    interactive: Boolean,
    onRadiusChange: (Int) -> Unit,
) {
    Column {
        Text(
            text = stringResource(R.string.job_radius_value, radiusKm),
            style = MaterialTheme.typography.titleLarge.copy(fontWeight = FontWeight.Bold),
            color = MaterialTheme.colorScheme.primary,
        )
        Slider(
            value = radiusKm.toFloat(),
            onValueChange = { onRadiusChange(it.roundToInt()) },
            valueRange = JobRadius.MIN_KM.toFloat()..JobRadius.MAX_KM.toFloat(),
            enabled = interactive,
        )
        Row(modifier = Modifier.fillMaxWidth()) {
            Text(
                text = stringResource(R.string.job_radius_value, JobRadius.MIN_KM),
                style = MaterialTheme.typography.labelSmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
                modifier = Modifier.weight(1f),
            )
            Text(
                text = stringResource(R.string.job_radius_value, JobRadius.MAX_KM),
                style = MaterialTheme.typography.labelSmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
        }
    }
}

@Preview
@Composable
private fun JobRadiusScreenLimitedPreview() {
    CleansiaPartnerTheme {
        JobRadiusScreenContent(
            uiState = JobRadiusUiState.Loaded(
                JobRadiusForm(employeeId = "emp-1", limitEnabled = true, radiusKm = 40),
            ),
            saving = false,
            onNavigateBack = {},
            onRetry = {},
            onLimitEnabledChange = {},
            onRadiusChange = {},
            onSave = {},
        )
    }
}

@Preview
@Composable
private fun JobRadiusScreenEveryJobPreview() {
    CleansiaPartnerTheme {
        JobRadiusScreenContent(
            uiState = JobRadiusUiState.Loaded(JobRadiusForm(employeeId = "emp-1")),
            saving = false,
            onNavigateBack = {},
            onRetry = {},
            onLimitEnabledChange = {},
            onRadiusChange = {},
            onSave = {},
        )
    }
}
