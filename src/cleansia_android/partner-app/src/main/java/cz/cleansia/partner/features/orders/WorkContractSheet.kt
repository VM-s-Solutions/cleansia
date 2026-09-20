package cz.cleansia.partner.features.orders

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxHeight
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.navigationBarsPadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.Info
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.ModalBottomSheet
import androidx.compose.material3.Text
import androidx.compose.material3.rememberModalBottomSheetState
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.pluralStringResource
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import cz.cleansia.core.format.formatOrderDateRange
import cz.cleansia.core.format.formatOrderDateTime
import cz.cleansia.core.format.formatOrderPrice
import cz.cleansia.core.ui.components.CleansiaPrimaryButton
import cz.cleansia.core.ui.components.HtmlContentView
import cz.cleansia.core.ui.state.ActionState
import cz.cleansia.core.ui.theme.Spacing
import cz.cleansia.partner.R
import cz.cleansia.partner.data.orders.WorkContract
import cz.cleansia.partner.data.orders.WorkContractAcceptanceFacts
import cz.cleansia.partner.data.orders.WorkContractJobFacts
import cz.cleansia.partner.ui.theme.CleansiaPartnerTheme
import java.util.Locale

/**
 * The contract for work, on one screen with the gesture that accepts it: the job's facts, the text,
 * and the swipe beneath. A take and a standalone acceptance differ only in what the swipe calls; a
 * read shows the stored facts and the accepted version with no swipe at all.
 *
 * Every open re-loads — the facts are the server's word at that moment, never the list row's.
 */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun WorkContractSheet(
    request: WorkContractRequest,
    onDismiss: () -> Unit,
    onOutcome: (WorkContractOutcome) -> Unit,
    viewModel: WorkContractSheetViewModel = hiltViewModel(),
) {
    val uiState by viewModel.uiState.collectAsStateWithLifecycle()
    val actionState by viewModel.actionState.collectAsStateWithLifecycle()
    val notice by viewModel.notice.collectAsStateWithLifecycle()
    val sheetState = rememberModalBottomSheetState(skipPartiallyExpanded = true)
    val submitting = actionState is ActionState.Submitting

    LaunchedEffect(viewModel, request) { viewModel.open(request) }
    LaunchedEffect(viewModel) { viewModel.outcome.collect(onOutcome) }

    ModalBottomSheet(
        onDismissRequest = { if (!submitting) onDismiss() },
        sheetState = sheetState,
        containerColor = MaterialTheme.colorScheme.surface,
    ) {
        WorkContractSheetContent(
            request = request,
            uiState = uiState,
            notice = notice,
            submitting = submitting,
            onAccept = viewModel::accept,
            onRetry = viewModel::retry,
            onClose = onDismiss,
        )
    }
}

@Composable
fun WorkContractSheetContent(
    request: WorkContractRequest,
    uiState: WorkContractUiState,
    notice: WorkContractNotice?,
    submitting: Boolean,
    onAccept: () -> Unit,
    onRetry: () -> Unit,
    onClose: () -> Unit,
) {
    Column(
        modifier = Modifier
            .fillMaxWidth()
            .fillMaxHeight()
            .padding(horizontal = Spacing.M)
            .navigationBarsPadding(),
    ) {
        Text(
            text = (uiState as? WorkContractUiState.Loaded)?.contract?.title
                ?: stringResource(R.string.work_contract_title),
            style = MaterialTheme.typography.titleLarge.copy(fontWeight = FontWeight.Bold),
            color = MaterialTheme.colorScheme.onSurface,
        )
        Spacer(Modifier.height(Spacing.S))

        when (uiState) {
            WorkContractUiState.Loading -> Box(
                modifier = Modifier.fillMaxSize(),
                contentAlignment = Alignment.Center,
            ) { CircularProgressIndicator() }

            WorkContractUiState.Error -> SheetMessage(
                text = stringResource(R.string.work_contract_load_error),
                ctaLabel = stringResource(R.string.retry),
                onCta = onRetry,
            )

            WorkContractUiState.Unavailable -> SheetMessage(
                text = stringResource(R.string.error_legal_document_not_found),
                ctaLabel = stringResource(R.string.close),
                onCta = onClose,
            )

            is WorkContractUiState.Loaded -> LoadedContract(
                request = request,
                contract = uiState.contract,
                notice = notice,
                submitting = submitting,
                onAccept = onAccept,
                onClose = onClose,
            )
        }
    }
}

@Composable
private fun LoadedContract(
    request: WorkContractRequest,
    contract: WorkContract,
    notice: WorkContractNotice?,
    submitting: Boolean,
    onAccept: () -> Unit,
    onClose: () -> Unit,
) {
    Column(modifier = Modifier.fillMaxSize()) {
        Text(
            text = stringResource(R.string.work_contract_version, contract.version),
            style = MaterialTheme.typography.labelMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
        Spacer(Modifier.height(Spacing.S))
        JobFacts(facts = contract.facts)
        contract.acceptance?.let { acceptance ->
            Spacer(Modifier.height(Spacing.XS))
            AcceptanceFacts(acceptance = acceptance, renderedLanguage = contract.language)
        }
        if (notice == WorkContractNotice.TextUpdated) {
            Spacer(Modifier.height(Spacing.S))
            NoticeRow(text = stringResource(R.string.error_contract_text_mismatch))
        }
        Spacer(Modifier.height(Spacing.S))
        HorizontalDivider(color = MaterialTheme.colorScheme.outlineVariant)
        HtmlContentView(
            html = contract.contentHtml,
            modifier = Modifier
                .fillMaxWidth()
                .weight(1f),
        )
        HorizontalDivider(color = MaterialTheme.colorScheme.outlineVariant)
        Spacer(Modifier.height(Spacing.S))
        when (request) {
            is WorkContractRequest.Take, is WorkContractRequest.Accept -> SlideToCommit(
                idleLabel = stringResource(R.string.work_contract_swipe_to_accept),
                busyLabel = stringResource(R.string.work_contract_accepting),
                onCommit = onAccept,
                isBusy = submitting,
            )
            is WorkContractRequest.Read -> CleansiaPrimaryButton(
                text = stringResource(R.string.close),
                onClick = onClose,
            )
        }
        Spacer(Modifier.height(Spacing.M))
    }
}

@Composable
private fun JobFacts(facts: WorkContractJobFacts) {
    Column(
        modifier = Modifier
            .fillMaxWidth()
            .background(MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.5f), RoundedCornerShape(12.dp))
            .padding(Spacing.S),
        verticalArrangement = Arrangement.spacedBy(Spacing.XXS),
    ) {
        Row(modifier = Modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically) {
            Text(
                text = facts.orderNumber?.takeIf { it.isNotBlank() }
                    ?.let { stringResource(R.string.work_contract_order_number, it) }
                    ?: formatOrderDateRange(facts.cleaningDateTimeUtc, facts.estimatedMinutes),
                style = MaterialTheme.typography.titleSmall.copy(fontWeight = FontWeight.SemiBold),
                color = MaterialTheme.colorScheme.onSurface,
                modifier = Modifier.weight(1f),
            )
            Text(
                text = formatOrderPrice(facts.totalPrice, facts.currencyCode),
                style = MaterialTheme.typography.titleSmall.copy(fontWeight = FontWeight.Bold),
                color = MaterialTheme.colorScheme.primary,
            )
        }
        if (!facts.orderNumber.isNullOrBlank()) {
            FactLine(formatOrderDateRange(facts.cleaningDateTimeUtc, facts.estimatedMinutes))
        }
        facts.locationApproximate?.takeIf { it.isNotBlank() }?.let { FactLine(it) }
        FactLine(
            listOfNotNull(
                facts.rooms.takeIf { it > 0 }?.let { pluralStringResource(R.plurals.scope_rooms, it, it) },
                facts.bathrooms.takeIf { it > 0 }?.let { pluralStringResource(R.plurals.scope_baths, it, it) },
            ).joinToString(" · "),
        )
        val scope = facts.packages + facts.services + facts.extraSlugs.map { nameForExtraSlug(it) }
        if (scope.isNotEmpty()) FactLine(scope.joinToString(", "))
    }
}

@Composable
private fun FactLine(text: String) {
    if (text.isBlank()) return
    Text(
        text = text,
        style = MaterialTheme.typography.bodySmall,
        color = MaterialTheme.colorScheme.onSurfaceVariant,
    )
}

@Composable
private fun AcceptanceFacts(acceptance: WorkContractAcceptanceFacts, renderedLanguage: String?) {
    Column(verticalArrangement = Arrangement.spacedBy(Spacing.XXS)) {
        Text(
            text = stringResource(
                R.string.work_contract_accepted_on,
                formatOrderDateTime(acceptance.acceptedOn),
                acceptance.documentVersion,
            ),
            style = MaterialTheme.typography.bodySmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
        val accepted = acceptance.acceptedLanguage?.takeIf { it.isNotBlank() }
        if (accepted != null && renderedLanguage != null && !accepted.equals(renderedLanguage, ignoreCase = true)) {
            Text(
                text = stringResource(
                    R.string.work_contract_accepted_in_language,
                    Locale.forLanguageTag(accepted).getDisplayLanguage(Locale.getDefault()),
                ),
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
        }
    }
}

@Composable
private fun NoticeRow(text: String) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .background(MaterialTheme.colorScheme.tertiaryContainer, RoundedCornerShape(12.dp))
            .padding(Spacing.S),
        verticalAlignment = Alignment.CenterVertically,
        horizontalArrangement = Arrangement.spacedBy(Spacing.XS),
    ) {
        Icon(
            imageVector = Icons.Outlined.Info,
            contentDescription = null,
            tint = MaterialTheme.colorScheme.onTertiaryContainer,
            modifier = Modifier.size(18.dp),
        )
        Text(
            text = text,
            style = MaterialTheme.typography.bodySmall.copy(fontWeight = FontWeight.SemiBold),
            color = MaterialTheme.colorScheme.onTertiaryContainer,
        )
    }
}

@Composable
private fun SheetMessage(text: String, ctaLabel: String, onCta: () -> Unit) {
    Column(
        modifier = Modifier
            .fillMaxWidth()
            .padding(vertical = Spacing.XL),
        horizontalAlignment = Alignment.CenterHorizontally,
        verticalArrangement = Arrangement.spacedBy(Spacing.M),
    ) {
        Text(
            text = text,
            style = MaterialTheme.typography.bodyLarge,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
        CleansiaPrimaryButton(text = ctaLabel, onClick = onCta)
        Spacer(Modifier.height(Spacing.M))
    }
}

@Preview
@Composable
private fun WorkContractSheetPreview() {
    CleansiaPartnerTheme {
        WorkContractSheetContent(
            request = WorkContractRequest.Take("order-1"),
            uiState = WorkContractUiState.Loaded(
                WorkContract(
                    legalDocumentTextId = "text-1",
                    version = "2026-09-20",
                    language = "en",
                    title = "Contract for Work",
                    contentHtml = "<p>This contract for work is concluded between the customer and the cleaner.</p>",
                    facts = WorkContractJobFacts(
                        orderNumber = "CL-2026-0042",
                        cleaningDateTimeUtc = "2026-08-12T09:00:00Z",
                        estimatedMinutes = 180,
                        totalPrice = 1850.0,
                        currencyCode = "CZK",
                        locationApproximate = "Praha 4 · 14000",
                        rooms = 3,
                        bathrooms = 1,
                        services = listOf("Standard cleaning"),
                        packages = emptyList(),
                        extraSlugs = listOf("inside-oven"),
                    ),
                    acceptance = null,
                ),
            ),
            notice = WorkContractNotice.TextUpdated,
            submitting = false,
            onAccept = {},
            onRetry = {},
            onClose = {},
        )
    }
}
