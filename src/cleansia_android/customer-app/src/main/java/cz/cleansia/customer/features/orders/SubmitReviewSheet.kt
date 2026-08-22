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
import androidx.compose.ui.res.pluralStringResource
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import cz.cleansia.customer.R
import cz.cleansia.core.ui.components.CleansiaChip
import cz.cleansia.customer.core.orders.OrderReviewDto
import cz.cleansia.customer.core.orders.ReviewTag
import cz.cleansia.customer.ui.theme.WarningStar

// 1000, because that is what SubmitOrderReview.Validator and OrderReview.Comment's [MaxLength]
// actually enforce. This said 2000 under a comment claiming it matched the backend, so a customer
// who typed 1500 characters had every one of them accepted by the field and the whole review
// refused after submit, with the generic common.max_length key. Web was already correct.
internal const val REVIEW_COMMENT_MAX_LENGTH = 1000

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
    onConfirm: (rating: Int, comment: String?, tags: List<ReviewTag>) -> Unit,
    isSubmitting: Boolean = false,
    errorMessage: String? = null,
    existingReview: OrderReviewDto? = null,
    titleRes: Int? = null,
    dismissLabelRes: Int = R.string.order_review_cancel,
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
    var selectedTags by remember(existingReview?.id) {
        mutableStateOf(existingReview?.tags.orEmpty().toSet())
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
                    titleRes ?: if (isEdit) R.string.order_review_edit_title
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
                onRatingChange = { next ->
                    // Crossing the 4-star boundary swaps the whole chip set, so anything already
                    // picked belongs to the other polarity and would be refused by the server.
                    // Dropped here rather than at submit, so the customer SEES it happen.
                    if (ReviewTag.forRating(next) != ReviewTag.forRating(rating)) {
                        selectedTags = emptySet()
                    }
                    rating = next
                },
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

            // Chips appear only once a rating exists, because the set they offer IS a function of it:
            // 1-3 asks what went wrong, 4-5 what went well. Changing the rating across the boundary
            // clears the selection rather than carrying it — the server refuses a mismatched tag, and
            // silently dropping one would store a review the customer did not give.
            val offered = ReviewTag.forRating(rating)
            if (offered.isNotEmpty()) {
                Text(
                    text = stringResource(
                        if (rating >= ReviewTag.POSITIVE_RATING_FLOOR) {
                            R.string.order_review_tags_positive_prompt
                        } else {
                            R.string.order_review_tags_negative_prompt
                        },
                    ),
                    style = MaterialTheme.typography.titleSmall,
                    color = MaterialTheme.colorScheme.onSurface,
                    textAlign = TextAlign.Center,
                    modifier = Modifier.fillMaxWidth(),
                )
                Spacer(Modifier.height(10.dp))
                ReviewTagChips(
                    offered = offered,
                    selected = selectedTags,
                    enabled = !isSubmitting,
                    onToggle = { tag ->
                        selectedTags = when {
                            tag in selectedTags -> selectedTags - tag
                            // The cap is the server's; the sheet stops offering rather than letting the
                            // customer pick a fifth and be refused after submit.
                            selectedTags.size >= ReviewTag.MAX_TAGS -> selectedTags
                            else -> selectedTags + tag
                        }
                    },
                )
                Spacer(Modifier.height(16.dp))
            }

            // Comment textarea — capped at 2000 chars client-side to match the
            // backend validator. Optional field.
            OutlinedTextField(
                value = comment,
                onValueChange = { next ->
                    comment = if (next.length > REVIEW_COMMENT_MAX_LENGTH) {
                        next.substring(0, REVIEW_COMMENT_MAX_LENGTH)
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
                    text = stringResource(dismissLabelRes),
                    style = MaterialTheme.typography.titleMedium,
                    color = MaterialTheme.colorScheme.onSurface,
                )
            }
            Spacer(Modifier.height(10.dp))
            val canSubmit = rating in 1..5 && !isSubmitting
            Button(
                onClick = {
                    if (canSubmit) {
                        onConfirm(
                            rating,
                            comment.trim().ifBlank { null },
                            // Ordered by wire value so two identical selections submit identically,
                            // whatever order the chips were tapped in.
                            selectedTags.sortedBy { it.code },
                        )
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
/**
 * The offered chips, wrapped. Multi-select under [ReviewTag.MAX_TAGS] — the caller owns that policy,
 * which is why [CleansiaChip] does not.
 */
@OptIn(androidx.compose.foundation.layout.ExperimentalLayoutApi::class)
@Composable
private fun ReviewTagChips(
    offered: List<ReviewTag>,
    selected: Set<ReviewTag>,
    enabled: Boolean,
    onToggle: (ReviewTag) -> Unit,
) {
    androidx.compose.foundation.layout.FlowRow(
        modifier = Modifier.fillMaxWidth(),
        horizontalArrangement = Arrangement.spacedBy(8.dp),
        verticalArrangement = Arrangement.spacedBy(8.dp),
    ) {
        offered.forEach { tag ->
            val isSelected = tag in selected
            CleansiaChip(
                label = stringResource(tag.labelRes()),
                isSelected = isSelected,
                // At the cap the unselected chips go inert rather than disappearing: a chip row that
                // reflows as you tap moves the targets under the customer's finger.
                enabled = enabled && (isSelected || selected.size < ReviewTag.MAX_TAGS),
                onClick = { onToggle(tag) },
            )
        }
    }
}

/** Chip copy. Exhaustive on purpose — a new tag is a compile error until it has a label. */
private fun ReviewTag.labelRes(): Int = when (this) {
    ReviewTag.OnTime -> R.string.order_review_tag_on_time
    ReviewTag.Thorough -> R.string.order_review_tag_thorough
    ReviewTag.Friendly -> R.string.order_review_tag_friendly
    ReviewTag.CarefulWithBelongings -> R.string.order_review_tag_careful
    ReviewTag.ExtrasDoneWell -> R.string.order_review_tag_extras_done_well
    ReviewTag.FollowedInstructions -> R.string.order_review_tag_followed_instructions
    ReviewTag.GreatPhotos -> R.string.order_review_tag_great_photos
    ReviewTag.ArrivedLate -> R.string.order_review_tag_arrived_late
    ReviewTag.MissedAreas -> R.string.order_review_tag_missed_areas
    ReviewTag.FeltRushed -> R.string.order_review_tag_felt_rushed
    ReviewTag.ExtraNotDone -> R.string.order_review_tag_extra_not_done
    ReviewTag.DidNotFollowInstructions -> R.string.order_review_tag_ignored_instructions
    ReviewTag.Unprofessional -> R.string.order_review_tag_unprofessional
    ReviewTag.SmellOrProducts -> R.string.order_review_tag_smell
    ReviewTag.CrewSmallerThanBooked -> R.string.order_review_tag_small_crew
}

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
                    contentDescription = pluralStringResource(R.plurals.order_review_star_content_desc, star, star),
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
