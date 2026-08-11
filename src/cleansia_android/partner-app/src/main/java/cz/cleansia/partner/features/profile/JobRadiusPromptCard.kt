package cz.cleansia.partner.features.profile

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.compose.runtime.getValue
import cz.cleansia.core.ui.theme.Spacing
import cz.cleansia.partner.R
import cz.cleansia.partner.ui.theme.CleansiaPartnerTheme

/**
 * The one-time ask, sited on the dashboard because that is the only screen every cleaner opens and
 * the app has no other in-place prompt pattern. Both buttons are real answers — "keep every job" IS
 * the country-wide preference, not a "later" — so the card never returns either way.
 */
@Composable
fun JobRadiusPromptCard(
    onChooseRadius: () -> Unit,
    viewModel: JobRadiusPromptViewModel = hiltViewModel(),
) {
    val uiState by viewModel.uiState.collectAsStateWithLifecycle()

    if (uiState == JobRadiusPromptUiState.Visible) {
        JobRadiusPromptCardContent(
            onChooseRadius = {
                viewModel.onChooseRadius()
                onChooseRadius()
            },
            onKeepEveryJob = viewModel::onKeepEveryJob,
        )
    }
}

@Composable
fun JobRadiusPromptCardContent(
    onChooseRadius: () -> Unit,
    onKeepEveryJob: () -> Unit,
) {
    Column(
        modifier = Modifier
            .fillMaxWidth()
            .padding(horizontal = Spacing.M)
            .clip(RoundedCornerShape(18.dp))
            .background(MaterialTheme.colorScheme.primary.copy(alpha = 0.08f))
            .padding(Spacing.M),
    ) {
        Text(
            text = stringResource(R.string.job_radius_prompt_title),
            style = MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.Bold),
            color = MaterialTheme.colorScheme.onSurface,
        )
        Spacer(Modifier.height(Spacing.XS))
        Text(
            text = stringResource(R.string.job_radius_prompt_body),
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
        Spacer(Modifier.height(Spacing.S))
        Row(
            modifier = Modifier.fillMaxWidth(),
            horizontalArrangement = Arrangement.End,
        ) {
            TextButton(onClick = onKeepEveryJob) {
                Text(stringResource(R.string.job_radius_prompt_keep_all))
            }
            TextButton(onClick = onChooseRadius) {
                Text(
                    text = stringResource(R.string.job_radius_prompt_choose),
                    fontWeight = FontWeight.SemiBold,
                )
            }
        }
    }
}

@Preview
@Composable
private fun JobRadiusPromptCardPreview() {
    CleansiaPartnerTheme {
        JobRadiusPromptCardContent(onChooseRadius = {}, onKeepEveryJob = {})
    }
}
