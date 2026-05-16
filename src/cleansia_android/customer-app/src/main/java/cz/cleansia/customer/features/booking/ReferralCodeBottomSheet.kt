package cz.cleansia.customer.features.booking

import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.imePadding
import androidx.compose.foundation.layout.navigationBarsPadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
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
import cz.cleansia.customer.core.referral.ReferralValidationError
import cz.cleansia.customer.ui.theme.ErrorText
import cz.cleansia.customer.ui.theme.SuccessText
import kotlinx.coroutines.launch

/**
 * Wolt-style modal bottom sheet for entering & applying a referral code.
 *
 * Mirrors [PromoCodeBottomSheet] in shape and lifecycle. The success message
 * is personalized when the backend returns the referrer's first name; falls
 * back to a neutral phrasing when the field is null/blank.
 *
 * Scrim + back-gesture dismissal are blocked while a validation request is in
 * flight; allowed in Idle / Valid / Invalid.
 */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun ReferralCodeBottomSheet(
    initialCode: String,
    onDismiss: () -> Unit,
    onValidate: suspend (code: String) -> ReferralCodeUiState,
    onApplied: (validatedCode: String, referrerFirstName: String?) -> Unit,
) {
    val sheetState = rememberModalBottomSheetState(skipPartiallyExpanded = true)
    val coroutineScope = rememberCoroutineScope()

    var codeInput by remember { mutableStateOf(initialCode.trim().uppercase()) }
    var localState by remember { mutableStateOf<ReferralCodeUiState>(ReferralCodeUiState.Idle) }
    val isSubmitting = localState is ReferralCodeUiState.Validating
    val isValid = localState is ReferralCodeUiState.Valid

    ModalBottomSheet(
        onDismissRequest = { if (!isSubmitting) onDismiss() },
        sheetState = sheetState,
        containerColor = MaterialTheme.colorScheme.surface,
    ) {
        // imePadding() lifts the sheet content above the soft keyboard.
        // Critically, do NOT wrap this Column in verticalScroll — the sheet
        // content is short (title + field + helper + 2 buttons) and a nested
        // scroll inside ModalBottomSheet collides with the sheet's drag-anchor
        // recalculation when the IME shows. On Compose BOM 2025.02.00 that
        // collision can lock the input pipeline (the symptom: tapping the
        // OutlinedTextField from the signup flow freezes the whole UI).
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .imePadding()
                .padding(horizontal = 24.dp)
                .padding(bottom = 8.dp),
        ) {
            Text(
                text = stringResource(R.string.booking_referral_code_dialog_title),
                style = MaterialTheme.typography.titleLarge.copy(fontWeight = FontWeight.Bold),
                color = MaterialTheme.colorScheme.onSurface,
                textAlign = TextAlign.Center,
                modifier = Modifier.fillMaxWidth(),
            )
            Spacer(Modifier.height(16.dp))

            val borderColor = when (localState) {
                is ReferralCodeUiState.Valid -> SuccessText
                is ReferralCodeUiState.Invalid -> ErrorText
                else -> MaterialTheme.colorScheme.outline
            }
            OutlinedTextField(
                value = codeInput,
                onValueChange = { next ->
                    if (localState !is ReferralCodeUiState.Idle && localState !is ReferralCodeUiState.Validating) {
                        localState = ReferralCodeUiState.Idle
                    }
                    codeInput = next.uppercase()
                },
                modifier = Modifier.fillMaxWidth(),
                enabled = !isSubmitting && !isValid,
                label = { Text(stringResource(R.string.booking_referral_code_dialog_title)) },
                singleLine = true,
                keyboardOptions = KeyboardOptions(
                    capitalization = KeyboardCapitalization.Characters,
                    keyboardType = KeyboardType.Ascii,
                    imeAction = ImeAction.Done,
                ),
                isError = localState is ReferralCodeUiState.Invalid,
                shape = RoundedCornerShape(12.dp),
                colors = OutlinedTextFieldDefaults.colors(
                    focusedBorderColor = borderColor,
                    unfocusedBorderColor = borderColor,
                    cursorColor = MaterialTheme.colorScheme.primary,
                ),
            )
            Spacer(Modifier.height(10.dp))

            ResultBlock(state = localState)

            Spacer(Modifier.height(20.dp))

            if (isValid) {
                val validState = localState as ReferralCodeUiState.Valid
                Button(
                    onClick = {
                        onApplied(codeInput.trim().uppercase(), validState.referrerFirstName)
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
                        text = stringResource(R.string.booking_referral_code_dialog_done),
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
                        text = stringResource(R.string.booking_referral_code_dialog_cancel),
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
                            localState = ReferralCodeUiState.Validating
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
                            text = stringResource(R.string.booking_referral_code_dialog_apply),
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

@Composable
private fun ResultBlock(state: ReferralCodeUiState) {
    when (state) {
        ReferralCodeUiState.Idle -> {
            Text(
                text = stringResource(R.string.booking_referral_code_dialog_helper),
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
        }
        ReferralCodeUiState.Validating -> {
            Row(verticalAlignment = Alignment.CenterVertically) {
                CircularProgressIndicator(
                    modifier = Modifier.size(16.dp),
                    strokeWidth = 2.dp,
                )
                Spacer(Modifier.width(8.dp))
                Text(
                    text = stringResource(R.string.referral_code_validating),
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
            }
        }
        is ReferralCodeUiState.Valid -> {
            val name = state.referrerFirstName?.takeIf { it.isNotBlank() }
            val message = if (name != null) {
                stringResource(R.string.booking_referral_code_dialog_success_named, name)
            } else {
                stringResource(R.string.booking_referral_code_dialog_success)
            }
            Row(verticalAlignment = Alignment.CenterVertically) {
                Icon(
                    imageVector = Icons.Outlined.CheckCircle,
                    contentDescription = null,
                    tint = SuccessText,
                    modifier = Modifier.size(18.dp),
                )
                Spacer(Modifier.width(8.dp))
                Text(
                    text = message,
                    style = MaterialTheme.typography.bodyMedium.copy(fontWeight = FontWeight.SemiBold),
                    color = SuccessText,
                )
            }
        }
        is ReferralCodeUiState.Invalid -> {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Icon(
                    imageVector = Icons.Outlined.Cancel,
                    contentDescription = null,
                    tint = ErrorText,
                    modifier = Modifier.size(18.dp),
                )
                Spacer(Modifier.width(8.dp))
                Text(
                    text = stringResource(referralErrorRes(state.error)),
                    style = MaterialTheme.typography.bodyMedium,
                    color = ErrorText,
                )
            }
        }
    }
}

/** Map the [ReferralValidationError] enum to a localized string resource. */
private fun referralErrorRes(error: ReferralValidationError?): Int = when (error) {
    ReferralValidationError.NotFound -> R.string.error_referral_not_found
    ReferralValidationError.SelfReferral -> R.string.error_referral_self_referral
    ReferralValidationError.AlreadyReferred -> R.string.error_referral_already_referred
    ReferralValidationError.Inactive -> R.string.error_referral_inactive
    null -> R.string.error_referral_generic
}
