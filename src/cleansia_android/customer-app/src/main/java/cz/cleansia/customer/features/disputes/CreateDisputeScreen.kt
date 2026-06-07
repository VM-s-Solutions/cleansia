package cz.cleansia.customer.features.disputes

import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.WindowInsets
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.navigationBars
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.windowInsetsPadding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.outlined.ArrowBack
import androidx.compose.material.icons.outlined.ArrowDropDown
import androidx.compose.material.icons.outlined.Warning
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.DropdownMenu
import androidx.compose.material3.DropdownMenuItem
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.TextFieldDefaults
import androidx.compose.material3.TopAppBar
import androidx.compose.material3.TopAppBarDefaults
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import cz.cleansia.customer.R

/**
 * CreateDisputeScreen — "Report an issue" full-screen form. Reached from two
 * places:
 *  1. OrderDetail footer → opens with an `orderId` query arg (happy path)
 *  2. DisputesList FAB   → opens without an orderId, screen renders an
 *     inline error state explaining the feature must be launched from an
 *     order's detail (Wave 2 has no order picker).
 *
 * Form fields: reason dropdown (7 enum values, 1-indexed) + description
 * textarea bounded by [DisputeFormConstants] (counter shown). Submit is gated on both.
 */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun CreateDisputeScreen(
    onBack: () -> Unit = {},
    onCreated: (disputeId: String) -> Unit = {},
    viewModel: CreateDisputeViewModel = hiltViewModel(),
) {
    val submitting by viewModel.submitting.collectAsStateWithLifecycle()
    val error by viewModel.error.collectAsStateWithLifecycle()

    var reasonValue by remember { mutableStateOf<Int?>(null) }
    var description by remember { mutableStateOf("") }

    // Navigate out on success. SharedFlow one-shot — won't replay on
    // recomposition.
    LaunchedEffect(viewModel) {
        viewModel.createdDisputeId.collect { id -> onCreated(id) }
    }

    Scaffold(
        containerColor = MaterialTheme.colorScheme.background,
        topBar = {
            TopAppBar(
                title = {
                    Text(
                        stringResource(R.string.dispute_create_title),
                        style = MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.SemiBold),
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
        bottomBar = {
            SubmitFooter(
                enabled = reasonValue != null &&
                    description.length in DisputeFormConstants.DESCRIPTION_MIN_LENGTH..DisputeFormConstants.DESCRIPTION_MAX_LENGTH &&
                    viewModel.orderId != null &&
                    !submitting,
                submitting = submitting,
                onClick = {
                    val r = reasonValue ?: return@SubmitFooter
                    viewModel.submit(r, description)
                },
            )
        },
    ) { padding ->
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(padding)
                .verticalScroll(rememberScrollState())
                .padding(horizontal = 20.dp, vertical = 12.dp),
        ) {
            // ── Order context / missing-order banner ──
            val contextOrderId = viewModel.orderId
            if (contextOrderId == null) {
                MissingOrderBanner()
                Spacer(Modifier.height(16.dp))
            } else {
                OrderContextCard(orderId = contextOrderId)
                Spacer(Modifier.height(16.dp))
            }

            // ── Reason ──
            Text(
                text = stringResource(R.string.dispute_create_reason_label),
                style = MaterialTheme.typography.labelLarge.copy(fontWeight = FontWeight.SemiBold),
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
            Spacer(Modifier.height(6.dp))
            ReasonDropdown(
                selectedValue = reasonValue,
                enabled = !submitting && viewModel.orderId != null,
                onSelect = {
                    reasonValue = it
                    if (!error.isNullOrBlank()) viewModel.clearError()
                },
            )
            Spacer(Modifier.height(16.dp))

            // ── Description ──
            Text(
                text = stringResource(R.string.dispute_create_description_label),
                style = MaterialTheme.typography.labelLarge.copy(fontWeight = FontWeight.SemiBold),
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
            Spacer(Modifier.height(6.dp))
            OutlinedTextField(
                value = description,
                onValueChange = { next ->
                    val clipped = if (next.length > DisputeFormConstants.DESCRIPTION_MAX_LENGTH) {
                        next.substring(0, DisputeFormConstants.DESCRIPTION_MAX_LENGTH)
                    } else {
                        next
                    }
                    description = clipped
                    if (!error.isNullOrBlank()) viewModel.clearError()
                },
                enabled = !submitting && viewModel.orderId != null,
                placeholder = { Text(stringResource(R.string.dispute_create_description_placeholder)) },
                minLines = 4,
                maxLines = 8,
                shape = RoundedCornerShape(12.dp),
                modifier = Modifier.fillMaxWidth(),
                colors = TextFieldDefaults.colors(
                    focusedContainerColor = MaterialTheme.colorScheme.surface,
                    unfocusedContainerColor = MaterialTheme.colorScheme.surface,
                ),
            )
            Spacer(Modifier.height(4.dp))
            Text(
                text = stringResource(R.string.dispute_create_char_count, description.length),
                style = MaterialTheme.typography.labelSmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
                modifier = Modifier.fillMaxWidth(),
                textAlign = TextAlign.End,
            )

            // ── Inline error ──
            val errorText = error
            if (!errorText.isNullOrBlank()) {
                Spacer(Modifier.height(12.dp))
                Text(
                    text = errorText,
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.error,
                )
            }

            Spacer(Modifier.height(24.dp))
        }
    }
}

/* ── Order context card ── */

@Composable
private fun OrderContextCard(orderId: String) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(14.dp))
            .background(MaterialTheme.colorScheme.primaryContainer.copy(alpha = 0.4f))
            .padding(horizontal = 14.dp, vertical = 12.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Column(Modifier.fillMaxWidth()) {
            Text(
                stringResource(R.string.dispute_create_order_label),
                style = MaterialTheme.typography.labelSmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
            Spacer(Modifier.height(2.dp))
            // We only have the orderId here, not the display number — shown
            // as-is. The user navigated from that order's detail so they
            // already know the context; this card just confirms it.
            Text(
                text = orderId,
                style = MaterialTheme.typography.bodyMedium.copy(fontWeight = FontWeight.SemiBold),
                color = MaterialTheme.colorScheme.onSurface,
            )
        }
    }
}

@Composable
private fun MissingOrderBanner() {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(14.dp))
            .background(MaterialTheme.colorScheme.errorContainer.copy(alpha = 0.5f))
            .border(
                1.dp,
                MaterialTheme.colorScheme.error.copy(alpha = 0.35f),
                RoundedCornerShape(14.dp),
            )
            .padding(14.dp),
        verticalAlignment = Alignment.Top,
    ) {
        Icon(
            Icons.Outlined.Warning,
            contentDescription = null,
            tint = MaterialTheme.colorScheme.error,
            modifier = Modifier.size(22.dp),
        )
        Spacer(Modifier.size(12.dp))
        Text(
            text = stringResource(R.string.dispute_create_missing_order),
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurface,
        )
    }
}

/* ── Reason dropdown ──
 *
 * A lightweight custom dropdown: tappable field + DropdownMenu anchored to
 * it. We deliberately avoid `ExposedDropdownMenuBox` — its API has drifted
 * between Material3 versions and its anchor requirements can surprise callers
 * around readOnly text fields. A manual anchor gives us predictable behavior
 * across Compose BOMs.
 */

private data class ReasonOption(val value: Int, val labelRes: Int)

private val REASON_OPTIONS = listOf(
    ReasonOption(1, R.string.dispute_reason_quality_issue),
    ReasonOption(2, R.string.dispute_reason_service_not_provided),
    ReasonOption(3, R.string.dispute_reason_service_incomplete),
    ReasonOption(4, R.string.dispute_reason_damaged_property),
    ReasonOption(5, R.string.dispute_reason_unauthorized_charge),
    ReasonOption(6, R.string.dispute_reason_incorrect_amount),
    ReasonOption(7, R.string.dispute_reason_other),
)

@Composable
private fun ReasonDropdown(
    selectedValue: Int?,
    enabled: Boolean,
    onSelect: (Int) -> Unit,
) {
    var expanded by remember { mutableStateOf(false) }
    val selectedLabel = selectedValue?.let { v ->
        REASON_OPTIONS.firstOrNull { it.value == v }?.let { stringResource(it.labelRes) }
    } ?: ""

    Box {
        OutlinedTextField(
            value = selectedLabel,
            onValueChange = { /* read-only via UI — edits go through onSelect */ },
            readOnly = true,
            enabled = enabled,
            placeholder = { Text(stringResource(R.string.dispute_create_reason_label)) },
            trailingIcon = {
                IconButton(onClick = { if (enabled) expanded = !expanded }) {
                    Icon(Icons.Outlined.ArrowDropDown, contentDescription = null)
                }
            },
            shape = RoundedCornerShape(12.dp),
            modifier = Modifier
                .fillMaxWidth()
                .clickable(enabled = enabled) { expanded = !expanded },
            colors = TextFieldDefaults.colors(
                focusedContainerColor = MaterialTheme.colorScheme.surface,
                unfocusedContainerColor = MaterialTheme.colorScheme.surface,
                disabledContainerColor = MaterialTheme.colorScheme.surface,
                // Make the read-only field look editable even though the text
                // field itself swallows taps — the clickable wrapper above
                // handles opening the menu.
                disabledTextColor = MaterialTheme.colorScheme.onSurface,
            ),
        )

        DropdownMenu(
            expanded = expanded,
            onDismissRequest = { expanded = false },
            modifier = Modifier.fillMaxWidth(fraction = 0.95f),
        ) {
            REASON_OPTIONS.forEach { option ->
                DropdownMenuItem(
                    text = { Text(stringResource(option.labelRes)) },
                    onClick = {
                        onSelect(option.value)
                        expanded = false
                    },
                )
            }
        }
    }
}

/* ── Submit footer ── */

@Composable
private fun SubmitFooter(
    enabled: Boolean,
    submitting: Boolean,
    onClick: () -> Unit,
) {
    Surface(
        color = MaterialTheme.colorScheme.surface,
        tonalElevation = 0.dp,
        modifier = Modifier.fillMaxWidth(),
    ) {
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .windowInsetsPadding(WindowInsets.navigationBars)
                .padding(horizontal = 16.dp, vertical = 12.dp),
        ) {
            Button(
                onClick = onClick,
                enabled = enabled,
                modifier = Modifier
                    .fillMaxWidth()
                    .height(48.dp),
                shape = CircleShape,
                colors = ButtonDefaults.buttonColors(
                    containerColor = MaterialTheme.colorScheme.primary,
                    contentColor = MaterialTheme.colorScheme.onPrimary,
                    disabledContainerColor = MaterialTheme.colorScheme.primary.copy(alpha = 0.4f),
                    disabledContentColor = MaterialTheme.colorScheme.onPrimary.copy(alpha = 0.7f),
                ),
            ) {
                if (submitting) {
                    CircularProgressIndicator(
                        modifier = Modifier.size(20.dp),
                        color = MaterialTheme.colorScheme.onPrimary,
                        strokeWidth = 2.dp,
                    )
                } else {
                    Text(
                        text = stringResource(R.string.dispute_create_submit),
                        style = MaterialTheme.typography.titleMedium,
                    )
                }
            }
        }
    }
}
