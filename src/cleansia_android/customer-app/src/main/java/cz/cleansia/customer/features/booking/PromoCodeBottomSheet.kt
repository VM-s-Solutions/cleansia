package cz.cleansia.customer.features.booking

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
import androidx.compose.material.icons.outlined.Cancel
import androidx.compose.material.icons.outlined.CheckCircle
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.ModalBottomSheet
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.OutlinedTextFieldDefaults
import androidx.compose.material3.Text
import androidx.compose.material3.rememberModalBottomSheetState
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.KeyboardCapitalization
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.compose.foundation.text.KeyboardOptions
import cz.cleansia.customer.R
import cz.cleansia.customer.core.promo.PromoCodeError
import cz.cleansia.customer.ui.theme.ErrorText
import cz.cleansia.customer.ui.theme.SuccessText
import kotlinx.coroutines.launch

/**
 * Wolt-style modal bottom sheet for entering & applying a promo code.
 *
 * The sheet owns local input + validation state; the backend call is fired
 * exactly once per Apply tap via [onValidate]. On a successful validation the
 * footer swaps Cancel + Apply for a single Done button which forwards the
 * applied code/discount via [onApplied] before dismissal.
 *
 * Scrim + back-gesture dismissal are blocked while a validation request is
 * in flight (Validating) — we don't want to lose the only feedback surface
 * mid-request. Idle / Valid / Invalid all allow dismissal.
 */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun PromoCodeBottomSheet(
    initialCode: String,
    onDismiss: () -> Unit,
    onValidate: suspend (code: String) -> PromoCodeUiState,
    onApplied: (validatedCode: String, discountAmount: Double) -> Unit,
) {
    val sheetState = rememberModalBottomSheetState(skipPartiallyExpanded = true)
    val coroutineScope = rememberCoroutineScope()

    var codeInput by remember { mutableStateOf(initialCode.trim().uppercase()) }
    var localState by remember { mutableStateOf<PromoCodeUiState>(PromoCodeUiState.Idle) }
    val isSubmitting = localState is PromoCodeUiState.Validating
    val isValid = localState is PromoCodeUiState.Valid

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
            // Title — centered Wolt-style.
            Text(
                text = stringResource(R.string.booking_promo_code_dialog_title),
                style = MaterialTheme.typography.titleLarge.copy(fontWeight = FontWeight.Bold),
                color = MaterialTheme.colorScheme.onSurface,
                textAlign = TextAlign.Center,
                modifier = Modifier.fillMaxWidth(),
            )
            Spacer(Modifier.height(16.dp))

            // Input — uppercase via both keyboard hint and visual filter; no placeholder.
            val borderColor = when (localState) {
                is PromoCodeUiState.Valid -> SuccessText
                is PromoCodeUiState.Invalid -> ErrorText
                else -> MaterialTheme.colorScheme.outline
            }
            OutlinedTextField(
                value = codeInput,
                onValueChange = { next ->
                    // Reset any prior result the moment the user edits the code.
                    if (localState !is PromoCodeUiState.Idle && localState !is PromoCodeUiState.Validating) {
                        localState = PromoCodeUiState.Idle
                    }
                    codeInput = next.uppercase()
                },
                modifier = Modifier.fillMaxWidth(),
                enabled = !isSubmitting && !isValid,
                label = { Text(stringResource(R.string.booking_promo_code_dialog_title)) },
                singleLine = true,
                keyboardOptions = KeyboardOptions(
                    capitalization = KeyboardCapitalization.Characters,
                    keyboardType = KeyboardType.Ascii,
                    imeAction = ImeAction.Done,
                ),
                isError = localState is PromoCodeUiState.Invalid,
                shape = RoundedCornerShape(12.dp),
                colors = OutlinedTextFieldDefaults.colors(
                    focusedBorderColor = borderColor,
                    unfocusedBorderColor = borderColor,
                    cursorColor = MaterialTheme.colorScheme.primary,
                ),
            )
            Spacer(Modifier.height(10.dp))

            // Helper text OR result message — mutually exclusive.
            ResultBlock(state = localState)

            Spacer(Modifier.height(20.dp))

            // Footer — swaps to Done after a successful validation.
            if (isValid) {
                val validState = localState as PromoCodeUiState.Valid
                Button(
                    onClick = {
                        onApplied(codeInput.trim().uppercase(), validState.discountAmount)
                        onDismiss()
                    },
                    modifier = Modifier
                        .fillMaxWidth()
                        .height(48.dp),
                    shape = CircleShape,
                    colors = ButtonDefaults.buttonColors(
                        containerColor = MaterialTheme.colorScheme.primary,
                        contentColor = MaterialTheme.colorScheme.onPrimary,
                    ),
                ) {
                    Text(
                        text = stringResource(R.string.booking_promo_code_dialog_done),
                        style = MaterialTheme.typography.titleMedium,
                    )
                }
            } else {
                OutlinedButton(
                    onClick = onDismiss,
                    enabled = !isSubmitting,
                    modifier = Modifier
                        .fillMaxWidth()
                        .height(48.dp),
                    shape = CircleShape,
                ) {
                    Text(
                        text = stringResource(R.string.booking_promo_code_dialog_cancel),
                        style = MaterialTheme.typography.titleMedium,
                        color = MaterialTheme.colorScheme.onSurface,
                    )
                }
                Spacer(Modifier.height(10.dp))
                val canApply = codeInput.isNotBlank() && !isSubmitting
                Button(
                    onClick = {
                        if (!canApply) return@Button
                        coroutineScope.launch {
                            localState = PromoCodeUiState.Validating
                            localState = onValidate(codeInput)
                        }
                    },
                    enabled = canApply,
                    modifier = Modifier
                        .fillMaxWidth()
                        .height(48.dp),
                    shape = CircleShape,
                    colors = ButtonDefaults.buttonColors(
                        containerColor = MaterialTheme.colorScheme.primary,
                        contentColor = MaterialTheme.colorScheme.onPrimary,
                        disabledContainerColor = MaterialTheme.colorScheme.primary.copy(alpha = 0.4f),
                        disabledContentColor = MaterialTheme.colorScheme.onPrimary.copy(alpha = 0.8f),
                    ),
                ) {
                    if (isSubmitting) {
                        CircularProgressIndicator(
                            modifier = Modifier.size(20.dp),
                            color = MaterialTheme.colorScheme.onPrimary,
                            strokeWidth = 2.dp,
                        )
                    } else {
                        Text(
                            text = stringResource(R.string.booking_promo_code_dialog_apply),
                            style = MaterialTheme.typography.titleMedium,
                        )
                    }
                }
            }

            Spacer(Modifier.navigationBarsPadding())
            Spacer(Modifier.height(8.dp))
        }
    }
}

/**
 * Renders the text under the input. Idle → helper text. Validating → spinner +
 * "Checking…". Valid → green check + success line with the discount amount.
 * Invalid → red X + the localized error string.
 */
@Composable
private fun ResultBlock(state: PromoCodeUiState) {
    when (state) {
        PromoCodeUiState.Idle -> {
            Text(
                text = stringResource(R.string.booking_promo_code_dialog_helper),
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
        }
        PromoCodeUiState.Validating -> {
            Row(verticalAlignment = Alignment.CenterVertically) {
                CircularProgressIndicator(
                    modifier = Modifier.size(16.dp),
                    strokeWidth = 2.dp,
                )
                Spacer(Modifier.width(8.dp))
                Text(
                    text = stringResource(R.string.booking_promo_code_validating),
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
            }
        }
        is PromoCodeUiState.Valid -> {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Icon(
                    imageVector = Icons.Outlined.CheckCircle,
                    contentDescription = null,
                    tint = SuccessText,
                    modifier = Modifier.size(18.dp),
                )
                Spacer(Modifier.width(8.dp))
                Text(
                    text = stringResource(
                        R.string.booking_promo_code_dialog_success,
                        "${state.discountAmount.toInt()} CZK",
                    ),
                    style = MaterialTheme.typography.bodyMedium.copy(fontWeight = FontWeight.SemiBold),
                    color = SuccessText,
                )
            }
        }
        is PromoCodeUiState.Invalid -> {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Icon(
                    imageVector = Icons.Outlined.Cancel,
                    contentDescription = null,
                    tint = ErrorText,
                    modifier = Modifier.size(18.dp),
                )
                Spacer(Modifier.width(8.dp))
                Text(
                    text = stringResource(promoErrorRes(state.error)),
                    style = MaterialTheme.typography.bodyMedium,
                    color = ErrorText,
                )
            }
        }
    }
}

/** Map the [PromoCodeError] enum to a localized string resource. */
private fun promoErrorRes(error: PromoCodeError?): Int = when (error) {
    PromoCodeError.NotFound -> R.string.booking_promo_code_error_not_found
    PromoCodeError.Inactive -> R.string.booking_promo_code_error_inactive
    PromoCodeError.Expired -> R.string.booking_promo_code_error_expired
    PromoCodeError.NotYetValid -> R.string.booking_promo_code_error_not_yet_valid
    PromoCodeError.GlobalLimitReached -> R.string.booking_promo_code_error_global_limit
    PromoCodeError.PerUserLimitReached -> R.string.booking_promo_code_error_used
    PromoCodeError.BelowMinimumOrderAmount -> R.string.booking_promo_code_error_min_order
    PromoCodeError.CurrencyMismatch -> R.string.booking_promo_code_error_currency
    null -> R.string.booking_promo_code_error_generic
}
