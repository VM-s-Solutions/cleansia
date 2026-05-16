package cz.cleansia.customer.features.orders

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.navigationBarsPadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Star
import androidx.compose.material.icons.outlined.StarBorder
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.ModalBottomSheet
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text
import androidx.compose.material3.TextFieldDefaults
import androidx.compose.material3.rememberModalBottomSheetState
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import cz.cleansia.customer.R
import cz.cleansia.customer.core.orders.OrderReviewDto
import cz.cleansia.customer.ui.theme.WarningStar

private const val MAX_COMMENT_LENGTH = 2000

/**
 * Modal bottom sheet for submitting (or editing) a review on a completed order.
 * Mirrors the Phase 2 CancelOrderSheet conventions:
 *  - The sheet owns its local UI state (rating + comment); the VM owns the
 *    submit state / error / one-shot success signal.
 *  - Clicking Submit never closes the sheet directly — the VM's SharedFlow
 *    flip drives dismissal from the screen.
 *  - Scrim / back-gesture dismissal no-ops while submitting so we don't lose
 *    the only feedback surface mid-request.
 *
 * Unlike cancel (destructive), this is a positive primary action — the filled
 * button uses primary tint, not error tint.
 *
 * Pass [existingReview] non-null to switch the sheet into edit mode: stars
 * and comment are pre-filled, the title and primary-button label change. The
 * `onConfirm` contract is identical for both modes — backend is upsert.
 */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun SubmitReviewSheet(
    onDismiss: () -> Unit,
    onConfirm: (rating: Int, comment: String?) -> Unit,
    isSubmitting: Boolean = false,
    errorMessage: String? = null,
    existingReview: OrderReviewDto? = null,
) {
    val isEdit = existingReview != null
    val sheetState = rememberModalBottomSheetState(skipPartiallyExpanded = true)
    // Seed local state from the existing review on first composition. Keying
    // remember on the review id makes us re-seed if the sheet is reopened with
    // a different review (defensive — Wave 3 only edits one review at a time).
    var rating by remember(existingReview?.id) {
        mutableIntStateOf(existingReview?.rating ?: 0)
    }
    var comment by remember(existingReview?.id) {
        mutableStateOf(existingReview?.comment.orEmpty())
    }

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
            // Title — switches between "Rate your cleaning" and "Edit your review"
            // based on whether an existing review was supplied.
            Text(
                text = stringResource(
                    if (isEdit) R.string.order_review_edit_title
                    else R.string.order_review_sheet_title,
                ),
                style = MaterialTheme.typography.titleLarge.copy(fontWeight = FontWeight.Bold),
                color = MaterialTheme.colorScheme.onSurface,
            )
            Spacer(Modifier.height(14.dp))

            // Star row — centered, tappable. Each IconButton gives us the 40dp
            // hit target automatically.
            StarRow(
                rating = rating,
                enabled = !isSubmitting,
                onRatingChange = { rating = it },
            )
            Spacer(Modifier.height(4.dp))

            // Rating description — changes with current rating.
            Text(
                text = stringResource(ratingDescriptionRes(rating)),
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
                textAlign = TextAlign.Center,
                modifier = Modifier.fillMaxWidth(),
            )
            Spacer(Modifier.height(16.dp))

            // Comment textarea — capped at 2000 chars client-side to match the
            // backend validator. Optional field.
            OutlinedTextField(
                value = comment,
                onValueChange = { next ->
                    comment = if (next.length > MAX_COMMENT_LENGTH) {
                        next.substring(0, MAX_COMMENT_LENGTH)
                    } else {
                        next
                    }
                },
                enabled = !isSubmitting,
                label = { Text(stringResource(R.string.order_review_comment_label)) },
                placeholder = { Text(stringResource(R.string.order_review_comment_placeholder)) },
                minLines = 2,
                maxLines = 6,
                shape = RoundedCornerShape(12.dp),
                modifier = Modifier.fillMaxWidth(),
                colors = TextFieldDefaults.colors(
                    focusedContainerColor = MaterialTheme.colorScheme.surface,
                    unfocusedContainerColor = MaterialTheme.colorScheme.surface,
                ),
            )

            // Inline error row (shown below the textarea if the submit failed).
            if (!errorMessage.isNullOrBlank()) {
                Spacer(Modifier.height(10.dp))
                Text(
                    text = errorMessage,
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.error,
                )
            }

            Spacer(Modifier.height(20.dp))

            // Footer buttons — Cancel (secondary, outlined) above Submit
            // (primary, filled). Matches the CancelOrderSheet stack order but
            // inverts the tint since this is a positive action.
            OutlinedButton(
                onClick = onDismiss,
                enabled = !isSubmitting,
                modifier = Modifier
                    .fillMaxWidth()
                    .height(48.dp),
                shape = CircleShape,
            ) {
                Text(
                    text = stringResource(R.string.order_review_cancel),
                    style = MaterialTheme.typography.titleMedium,
                    color = MaterialTheme.colorScheme.onSurface,
                )
            }
            Spacer(Modifier.height(10.dp))
            val canSubmit = rating in 1..5 && !isSubmitting
            Button(
                onClick = {
                    if (canSubmit) {
                        onConfirm(rating, comment.trim().ifBlank { null })
                    }
                },
                enabled = canSubmit,
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
                        text = stringResource(
                            if (isEdit) R.string.order_review_save
                            else R.string.order_review_submit,
                        ),
                        style = MaterialTheme.typography.titleMedium,
                    )
                }
            }

            Spacer(Modifier.navigationBarsPadding())
            Spacer(Modifier.height(8.dp))
        }
    }
}

/* ── Star row ── */

/**
 * Centered row of 5 tappable star icons. Filled + amber up to and including
 * [rating]; outlined + muted beyond it. IconButton provides the 40dp hit
 * target out of the box.
 */
@Composable
private fun StarRow(
    rating: Int,
    enabled: Boolean,
    onRatingChange: (Int) -> Unit,
) {
    Row(
        modifier = Modifier.fillMaxWidth(),
        horizontalArrangement = Arrangement.Center,
        verticalAlignment = Alignment.CenterVertically,
    ) {
        for (star in 1..5) {
            val isFilled = rating >= star
            IconButton(
                onClick = { onRatingChange(star) },
                enabled = enabled,
            ) {
                Icon(
                    imageVector = if (isFilled) Icons.Filled.Star else Icons.Outlined.StarBorder,
                    contentDescription = stringResource(R.string.order_review_star_content_desc, star),
                    tint = if (isFilled) WarningStar else MaterialTheme.colorScheme.outlineVariant,
                    modifier = Modifier.size(32.dp),
                )
            }
        }
    }
}

/** Maps the current star count to its descriptor string resource. */
private fun ratingDescriptionRes(rating: Int): Int = when (rating) {
    1 -> R.string.order_review_rating_1
    2 -> R.string.order_review_rating_2
    3 -> R.string.order_review_rating_3
    4 -> R.string.order_review_rating_4
    5 -> R.string.order_review_rating_5
    else -> R.string.order_review_rating_hint
}
