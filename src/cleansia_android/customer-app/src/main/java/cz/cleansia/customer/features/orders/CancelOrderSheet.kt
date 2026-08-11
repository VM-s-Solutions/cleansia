package cz.cleansia.customer.features.orders

import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.navigationBarsPadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.CheckCircle
import androidx.compose.material.icons.outlined.Info
import androidx.compose.material.icons.outlined.Warning
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.ModalBottomSheet
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text
import androidx.compose.material3.TextFieldDefaults
import androidx.compose.material3.rememberModalBottomSheetState
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import cz.cleansia.customer.R
import cz.cleansia.core.format.formatOrderPrice
import cz.cleansia.core.ui.components.CleansiaTextLink
import cz.cleansia.customer.ui.theme.WarningStar

private const val MAX_REASON_LENGTH = 2000

/**
 * Predefined cancellation reasons. The localized label is shown to the user
 * in a chip; the [code] is included in the submitted payload as a stable
 * machine-readable prefix (e.g. "schedule_changed: I have a conflict") so
 * support can group reasons later. "Other" is the only option that requires
 * the free-text field — every other option accepts notes optionally.
 */
private enum class CancelReasonOption(val code: String, val labelRes: Int) {
    ScheduleChanged("schedule_changed", R.string.order_cancel_reason_schedule),
    BookedByMistake("booked_by_mistake", R.string.order_cancel_reason_mistake),
    PriceTooHigh("price_too_high", R.string.order_cancel_reason_price),
    FoundAlternative("found_alternative", R.string.order_cancel_reason_alternative),
    NoLongerNeeded("no_longer_needed", R.string.order_cancel_reason_not_needed),
    Other("other", R.string.order_cancel_reason_other),
}

/**
 * Modal bottom sheet for cancelling an order. The cancellation fee comes from
 * `GET /api/Order/CancellationPreview` and is rendered as the server sent it —
 * the sheet has no tier ladder of its own, because the two facts that decide the
 * fee (the caller's own free-cancellation window and whether a cleaner has been
 * pulled onto the job) live only on the server.
 *
 * UX rules:
 *  - No secondary confirmation dialog on top of the sheet — the sheet IS the
 *    confirmation.
 *  - Confirm is held back only while the quote is in flight. A quote that
 *    FAILS never blocks a cancellation — the card says so and the button stays
 *    live.
 *  - Clicking the primary button never closes the sheet directly; the VM
 *    observes the result and emits on a SharedFlow that the screen uses to
 *    drive the dismissal.
 *  - An optional reason is capped at 2000 chars client-side so we can't send
 *    a payload the backend will reject.
 *  - While submitting, the scrim/back gesture no-ops — we don't want a
 *    half-completed cancel to dismiss the only feedback surface.
 */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun CancelOrderSheet(
    previewState: CancellationPreviewUiState,
    onDismiss: () -> Unit,
    onConfirm: (reason: String?) -> Unit,
    onRetryPreview: () -> Unit,
    isSubmitting: Boolean = false,
    errorMessage: String? = null,
    onReasonChanged: () -> Unit = {},
) {
    val sheetState = rememberModalBottomSheetState(skipPartiallyExpanded = true)
    var selectedReason by remember { mutableStateOf<CancelReasonOption?>(null) }
    var notes by remember { mutableStateOf("") }

    val canSubmit = cancelConfirmEnabled(
        previewState = previewState,
        hasReason = selectedReason != null,
        isOtherReason = selectedReason == CancelReasonOption.Other,
        notes = notes,
        isSubmitting = isSubmitting,
    )

    ModalBottomSheet(
        onDismissRequest = { if (!isSubmitting) onDismiss() },
        sheetState = sheetState,
        containerColor = MaterialTheme.colorScheme.surface,
    ) {
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .verticalScroll(rememberScrollState())
                .padding(horizontal = 24.dp)
                .padding(bottom = 8.dp),
        ) {
            // Title
            Text(
                text = stringResource(R.string.order_cancel_title),
                style = MaterialTheme.typography.titleLarge.copy(fontWeight = FontWeight.Bold),
                color = MaterialTheme.colorScheme.onSurface,
            )
            Spacer(Modifier.height(12.dp))

            FeePreviewBlock(state = previewState, onRetry = onRetryPreview)
            Spacer(Modifier.height(16.dp))

            // Reason picker — tap a chip; tapping again deselects. "Other"
            // reveals the free-text area as required; the rest reveal it as
            // optional. Single-select (radio-style) — multi-reason cancels
            // are rare and the support workflow only acts on the primary one.
            Text(
                text = stringResource(R.string.order_cancel_reason_picker_label),
                style = MaterialTheme.typography.titleSmall.copy(fontWeight = FontWeight.SemiBold),
                color = MaterialTheme.colorScheme.onSurface,
            )
            Spacer(Modifier.height(8.dp))
            ReasonChipGrid(
                selected = selectedReason,
                enabled = !isSubmitting,
                onSelect = { picked ->
                    selectedReason = picked
                    if (!errorMessage.isNullOrBlank()) onReasonChanged()
                },
            )
            Spacer(Modifier.height(14.dp))

            // Notes — visible once a reason is picked. For "Other" it's required
            // (≥3 chars); otherwise it's an optional add-on. Cap at 2000 chars
            // client-side so the payload matches the backend validator.
            if (selectedReason != null) {
                val isOther = selectedReason == CancelReasonOption.Other
                OutlinedTextField(
                    value = notes,
                    onValueChange = { next ->
                        val clipped = if (next.length > MAX_REASON_LENGTH) {
                            next.substring(0, MAX_REASON_LENGTH)
                        } else {
                            next
                        }
                        notes = clipped
                        if (!errorMessage.isNullOrBlank()) onReasonChanged()
                    },
                    enabled = !isSubmitting,
                    label = {
                        Text(
                            stringResource(
                                if (isOther) R.string.order_cancel_notes_required_label
                                else R.string.order_cancel_notes_optional_label,
                            ),
                        )
                    },
                    placeholder = {
                        Text(
                            stringResource(
                                if (isOther) R.string.order_cancel_notes_other_placeholder
                                else R.string.order_cancel_notes_extra_placeholder,
                            ),
                        )
                    },
                    minLines = 3,
                    maxLines = 6,
                    shape = RoundedCornerShape(12.dp),
                    modifier = Modifier.fillMaxWidth(),
                    colors = TextFieldDefaults.colors(
                        focusedContainerColor = MaterialTheme.colorScheme.surface,
                        unfocusedContainerColor = MaterialTheme.colorScheme.surface,
                    ),
                )
            }

            // Inline error row (shown below the form if the submit failed).
            if (!errorMessage.isNullOrBlank()) {
                Spacer(Modifier.height(10.dp))
                Text(
                    text = errorMessage,
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.error,
                )
            }

            Spacer(Modifier.height(18.dp))

            // Footer buttons — "Keep" on top (secondary, reversible), "Confirm"
            // on bottom (destructive). The destructive affordance sits closest
            // to the thumb so the user has to deliberately reach for it.
            OutlinedButton(
                onClick = onDismiss,
                enabled = !isSubmitting,
                modifier = Modifier
                    .fillMaxWidth()
                    .height(48.dp),
                shape = CircleShape,
            ) {
                Text(
                    text = stringResource(R.string.order_cancel_keep),
                    style = MaterialTheme.typography.titleMedium,
                    color = MaterialTheme.colorScheme.onSurface,
                )
            }
            Spacer(Modifier.height(10.dp))
            Button(
                onClick = {
                    val reason = selectedReason
                    if (canSubmit && reason != null) {
                        // Submitted payload format: "code: notes" so support can
                        // sort by reason without parsing the localized label.
                        // Pure-code form when notes are blank.
                        val payload = buildString {
                            append(reason.code)
                            val trimmedNotes = notes.trim()
                            if (trimmedNotes.isNotEmpty()) append(": ").append(trimmedNotes)
                        }
                        onConfirm(payload)
                    }
                },
                enabled = canSubmit,
                modifier = Modifier
                    .fillMaxWidth()
                    .height(48.dp),
                shape = CircleShape,
                colors = ButtonDefaults.buttonColors(
                    containerColor = MaterialTheme.colorScheme.error,
                    contentColor = MaterialTheme.colorScheme.onError,
                    disabledContainerColor = MaterialTheme.colorScheme.error.copy(alpha = 0.4f),
                    disabledContentColor = MaterialTheme.colorScheme.onError.copy(alpha = 0.7f),
                ),
            ) {
                if (isSubmitting) {
                    CircularProgressIndicator(
                        modifier = Modifier.size(20.dp),
                        color = MaterialTheme.colorScheme.onError,
                        strokeWidth = 2.dp,
                    )
                } else {
                    Text(
                        text = stringResource(R.string.order_cancel_confirm),
                        style = MaterialTheme.typography.titleMedium,
                    )
                }
            }

            Spacer(Modifier.navigationBarsPadding())
            Spacer(Modifier.height(8.dp))
        }
    }
}

/**
 * Renders the six reason chips in a wrap-friendly flow layout. Each chip is
 * radio-style (single select). Tapping the selected chip again clears it.
 */
@OptIn(androidx.compose.foundation.layout.ExperimentalLayoutApi::class)
@Composable
private fun ReasonChipGrid(
    selected: CancelReasonOption?,
    enabled: Boolean,
    onSelect: (CancelReasonOption?) -> Unit,
) {
    androidx.compose.foundation.layout.FlowRow(
        modifier = Modifier.fillMaxWidth(),
        horizontalArrangement = Arrangement.spacedBy(8.dp),
        verticalArrangement = Arrangement.spacedBy(8.dp),
    ) {
        CancelReasonOption.entries.forEach { option ->
            ReasonChip(
                label = stringResource(option.labelRes),
                isSelected = selected == option,
                enabled = enabled,
                onClick = { onSelect(if (selected == option) null else option) },
            )
        }
    }
}

@Composable
private fun ReasonChip(
    label: String,
    isSelected: Boolean,
    enabled: Boolean,
    onClick: () -> Unit,
) {
    val border = if (isSelected) MaterialTheme.colorScheme.primary
                 else MaterialTheme.colorScheme.outlineVariant
    val bg = if (isSelected) MaterialTheme.colorScheme.primary.copy(alpha = 0.12f)
             else MaterialTheme.colorScheme.surface
    val labelColor = if (isSelected) MaterialTheme.colorScheme.primary
                     else MaterialTheme.colorScheme.onSurface
    Box(
        modifier = Modifier
            .clip(RoundedCornerShape(999.dp))
            .background(bg)
            .border(
                width = if (isSelected) 1.5.dp else 1.dp,
                color = border,
                shape = RoundedCornerShape(999.dp),
            )
            .clickable(enabled = enabled, onClick = onClick)
            .padding(horizontal = 14.dp, vertical = 8.dp),
    ) {
        Text(
            text = label,
            style = MaterialTheme.typography.bodyMedium.copy(
                fontWeight = if (isSelected) FontWeight.SemiBold else FontWeight.Normal,
            ),
            color = labelColor,
        )
    }
}

/* ── Fee preview ── */

/**
 * Renders the server's answer, or says it does not have one. There is no
 * client-side arm: the fee depends on the caller's own free-cancellation window
 * and on whether a cleaner has been pulled onto the job, neither of which this
 * app can see.
 */
@Composable
private fun FeePreviewBlock(
    state: CancellationPreviewUiState,
    onRetry: () -> Unit,
) {
    when (state) {
        CancellationPreviewUiState.Loading -> FeeCard(
            tint = MaterialTheme.colorScheme.onSurfaceVariant,
            icon = null,
            title = stringResource(R.string.order_cancel_fee_checking),
            subtitle = null,
            leading = {
                CircularProgressIndicator(
                    modifier = Modifier.size(18.dp),
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    strokeWidth = 2.dp,
                )
            },
        )
        CancellationPreviewUiState.Error -> UnavailableFeeCard(onRetry)
        is CancellationPreviewUiState.Loaded -> {
            val callout = cancellationFeeCallout(state.preview)
            if (callout == null) {
                UnavailableFeeCard(onRetry)
            } else {
                LoadedFeeCard(callout, state.preview.currencyCode)
            }
        }
    }
}

@Composable
private fun LoadedFeeCard(callout: CancellationFeeCallout, currencyCode: String?) {
    val tint = when (callout.severity) {
        CancellationFeeSeverity.Free -> MaterialTheme.colorScheme.primary
        CancellationFeeSeverity.Fee -> WarningStar
        CancellationFeeSeverity.LastMinute -> MaterialTheme.colorScheme.error
    }
    val icon = when (callout.severity) {
        CancellationFeeSeverity.Free -> Icons.Outlined.CheckCircle
        else -> Icons.Outlined.Warning
    }
    val amounts = callout.amounts
        .map { formatOrderPrice(it, currencyCode) }
        .toTypedArray()

    Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
        FeeCard(
            tint = tint,
            icon = icon,
            title = stringResource(callout.titleRes),
            subtitle = stringResource(callout.amountRes, *amounts),
        )
        if (callout.warnsExpressWaiverForfeited) {
            ExpressWaiverWarning()
        }
        Text(
            text = stringResource(R.string.order_cancel_fee_recheck_note),
            style = MaterialTheme.typography.bodySmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
    }
}

@Composable
private fun UnavailableFeeCard(onRetry: () -> Unit) {
    val tint = MaterialTheme.colorScheme.onSurfaceVariant
    Column(verticalArrangement = Arrangement.spacedBy(4.dp)) {
        FeeCard(
            tint = tint,
            icon = Icons.Outlined.Warning,
            title = stringResource(R.string.order_cancel_fee_neutral),
            subtitle = stringResource(R.string.order_cancel_fee_unavailable),
        )
        CleansiaTextLink(
            text = stringResource(R.string.order_cancel_fee_retry),
            onClick = onRetry,
        )
    }
}

/**
 * ADR-0035 AM-13 — the forfeiture is invisible exactly where the fee is zero, so
 * it is disclosed before the customer confirms rather than after.
 */
@Composable
private fun ExpressWaiverWarning() {
    Row(
        modifier = Modifier.fillMaxWidth(),
        verticalAlignment = Alignment.Top,
    ) {
        Icon(
            imageVector = Icons.Outlined.Info,
            contentDescription = null,
            tint = WarningStar,
            modifier = Modifier.size(18.dp),
        )
        Spacer(Modifier.width(8.dp))
        Text(
            text = stringResource(R.string.order_cancel_express_waiver_forfeit),
            style = MaterialTheme.typography.bodySmall,
            color = MaterialTheme.colorScheme.onSurface,
        )
    }
}

@Composable
private fun FeeCard(
    tint: Color,
    icon: ImageVector?,
    title: String,
    subtitle: String?,
    leading: @Composable (() -> Unit)? = null,
) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(14.dp))
            .background(tint.copy(alpha = 0.10f))
            .border(1.dp, tint.copy(alpha = 0.35f), RoundedCornerShape(14.dp))
            .padding(14.dp),
        verticalAlignment = Alignment.Top,
    ) {
        Box(
            modifier = Modifier.size(24.dp),
            contentAlignment = Alignment.Center,
        ) {
            when {
                leading != null -> leading()
                icon != null -> Icon(
                    imageVector = icon,
                    contentDescription = null,
                    tint = tint,
                    modifier = Modifier.size(22.dp),
                )
            }
        }
        Spacer(Modifier.width(12.dp))
        Column(
            modifier = Modifier.fillMaxWidth(),
            verticalArrangement = Arrangement.spacedBy(4.dp),
        ) {
            Text(
                text = title,
                style = MaterialTheme.typography.bodyMedium.copy(fontWeight = FontWeight.SemiBold),
                color = MaterialTheme.colorScheme.onSurface,
            )
            if (subtitle != null) {
                Text(
                    text = subtitle,
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
            }
        }
    }
}
