package cz.cleansia.customer.features.orders

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.outlined.ArrowBack
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.material3.TopAppBar
import androidx.compose.material3.TopAppBarDefaults
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import cz.cleansia.core.format.formatOrderDateRange
import cz.cleansia.core.format.formatOrderDateTime
import cz.cleansia.core.format.formatOrderPrice
import cz.cleansia.core.ui.components.CleansiaErrorState
import cz.cleansia.core.ui.components.HtmlContentView
import cz.cleansia.core.ui.theme.Poppins
import cz.cleansia.core.ui.theme.Spacing
import cz.cleansia.customer.R
import cz.cleansia.customer.core.orders.WorkContractAcceptanceDetailsDto
import cz.cleansia.customer.core.orders.WorkContractDto
import cz.cleansia.customer.core.orders.WorkContractFactsDto
import cz.cleansia.customer.ui.theme.CleansiaTheme
import java.util.Locale

/**
 * The contract for work a cleaner accepted for one seat of the customer's order: the job's facts as
 * they were frozen at the acceptance, the acceptance itself, and the accepted document's text in
 * the customer's language — in-app, because the page is authenticated and a deep link into it from
 * a signed-in app would be a second sign-in for a paragraph of text.
 */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun WorkContractScreen(
    onBack: () -> Unit,
    viewModel: WorkContractViewModel = hiltViewModel(),
) {
    val state by viewModel.state.collectAsStateWithLifecycle()
    WorkContractScreenContent(state = state, onBack = onBack, onRetry = viewModel::refresh)
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun WorkContractScreenContent(
    state: WorkContractUiState,
    onBack: () -> Unit,
    onRetry: () -> Unit,
) {
    Scaffold(
        containerColor = MaterialTheme.colorScheme.background,
        topBar = {
            TopAppBar(
                title = {
                    Text(
                        stringResource(R.string.work_contract_title),
                        style = MaterialTheme.typography.titleMedium.copy(
                            fontFamily = Poppins,
                            fontWeight = FontWeight.SemiBold,
                        ),
                    )
                },
                navigationIcon = {
                    IconButton(onClick = onBack) {
                        Icon(
                            Icons.AutoMirrored.Outlined.ArrowBack,
                            contentDescription = stringResource(R.string.common_back),
                        )
                    }
                },
                colors = TopAppBarDefaults.topAppBarColors(
                    containerColor = MaterialTheme.colorScheme.surface,
                ),
            )
        },
    ) { padding ->
        Box(
            Modifier
                .fillMaxSize()
                .padding(padding),
        ) {
            when (state) {
                WorkContractUiState.Loading -> Box(Modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
                    CircularProgressIndicator(color = MaterialTheme.colorScheme.primary)
                }
                WorkContractUiState.Error -> CleansiaErrorState(
                    title = stringResource(R.string.work_contract_title),
                    message = stringResource(R.string.work_contract_load_error),
                    backLabel = stringResource(R.string.common_back),
                    retryLabel = stringResource(R.string.order_detail_error_retry),
                    onRetry = onRetry,
                    onBack = onBack,
                )
                is WorkContractUiState.Loaded -> LoadedContract(contract = state.contract)
            }
        }
    }
}

@Composable
private fun LoadedContract(contract: WorkContractDto) {
    Column(
        modifier = Modifier
            .fillMaxSize()
            .padding(horizontal = Spacing.M),
    ) {
        Spacer(Modifier.height(Spacing.S))
        Text(
            text = contract.title ?: stringResource(R.string.work_contract_title),
            style = MaterialTheme.typography.titleLarge.copy(fontFamily = Poppins, fontWeight = FontWeight.Bold),
            color = MaterialTheme.colorScheme.onBackground,
        )
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
        Spacer(Modifier.height(Spacing.S))
        HorizontalDivider(color = MaterialTheme.colorScheme.outlineVariant)
        HtmlContentView(
            html = contract.contentHtml,
            modifier = Modifier
                .fillMaxWidth()
                .weight(1f),
        )
    }
}

/** The job as it was frozen on the acceptance row — never the live order, which moves on afterwards. */
@Composable
private fun JobFacts(facts: WorkContractFactsDto) {
    Column(
        modifier = Modifier
            .fillMaxWidth()
            .background(MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.5f), RoundedCornerShape(12.dp))
            .padding(Spacing.S),
        verticalArrangement = Arrangement.spacedBy(Spacing.XXS),
    ) {
        Text(
            text = stringResource(R.string.work_contract_facts_title),
            style = MaterialTheme.typography.titleSmall.copy(fontWeight = FontWeight.SemiBold),
            color = MaterialTheme.colorScheme.onSurface,
        )
        facts.orderNumber?.takeIf { it.isNotBlank() }?.let {
            FactRow(label = stringResource(R.string.work_contract_order_number), value = it)
        }
        FactRow(
            label = stringResource(R.string.work_contract_window),
            value = formatOrderDateRange(facts.cleaningDateTimeUtc, facts.estimatedMinutes),
        )
        FactRow(
            label = stringResource(R.string.work_contract_price),
            value = formatOrderPrice(facts.totalPrice, facts.currencyCode),
        )
        facts.locationApproximate?.takeIf { it.isNotBlank() }?.let {
            FactRow(label = stringResource(R.string.work_contract_location), value = it)
        }
        FactRow(
            label = stringResource(R.string.order_detail_rooms),
            value = roomsAndBathrooms(facts.rooms, facts.bathrooms),
        )
        if (facts.services.isNotEmpty()) {
            FactRow(label = stringResource(R.string.order_detail_services_header), value = facts.services.joinToString(", "))
        }
        if (facts.packages.isNotEmpty()) {
            FactRow(label = stringResource(R.string.order_detail_packages_header), value = facts.packages.joinToString(", "))
        }
        if (facts.extraSlugs.isNotEmpty()) {
            FactRow(
                label = stringResource(R.string.order_detail_extras),
                value = facts.extraSlugs.joinToString(", ") { prettifyExtraKey(it) },
            )
        }
    }
}

@Composable
private fun FactRow(label: String, value: String) {
    Row(modifier = Modifier.fillMaxWidth(), verticalAlignment = Alignment.Top) {
        Text(
            text = label,
            style = MaterialTheme.typography.bodySmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
            modifier = Modifier.weight(1f),
        )
        Spacer(Modifier.padding(horizontal = Spacing.XS))
        Text(
            text = value,
            style = MaterialTheme.typography.bodySmall.copy(fontWeight = FontWeight.SemiBold),
            color = MaterialTheme.colorScheme.onSurface,
            modifier = Modifier.weight(2f),
        )
    }
}

@Composable
private fun AcceptanceFacts(acceptance: WorkContractAcceptanceDetailsDto, renderedLanguage: String?) {
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

@Preview
@Composable
private fun WorkContractScreenPreview() {
    CleansiaTheme {
        WorkContractScreenContent(
            state = WorkContractUiState.Loaded(
                WorkContractDto(
                    legalDocumentTextId = "text-1",
                    version = "2026-09-20",
                    language = "en",
                    title = "Contract for Work",
                    contentHtml = "<p>This contract for work is concluded between the customer and the cleaner.</p>",
                    facts = WorkContractFactsDto(
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
                    acceptance = WorkContractAcceptanceDetailsDto(
                        acceptedOn = "2026-08-10T18:40:00Z",
                        documentVersion = "2026-09-20",
                        acceptedLanguage = "cs",
                    ),
                ),
            ),
            onBack = {},
            onRetry = {},
        )
    }
}
