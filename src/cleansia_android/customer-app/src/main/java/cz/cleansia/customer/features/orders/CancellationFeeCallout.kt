package cz.cleansia.customer.features.orders

import androidx.annotation.StringRes
import cz.cleansia.customer.R
import cz.cleansia.customer.core.orders.CancellationFeePreviewDto

/** Mirrors `CancelOrder.Validator`'s `RuleFor(x => x.Reason).MaximumLength(500)`. */
const val CANCEL_REASON_MAX_LENGTH = 500

/**
 * How many characters the free-text notes may hold once the reason code and its `": "` joiner
 * are in front of them, so the submitted payload never exceeds [CANCEL_REASON_MAX_LENGTH].
 */
fun cancelNotesLimit(reasonCode: String?): Int =
    reasonCode?.let { CANCEL_REASON_MAX_LENGTH - it.length - 2 }?.coerceAtLeast(0) ?: CANCEL_REASON_MAX_LENGTH

/** How loudly the fee card is drawn. Carried as a token so a test can name it. */
enum class CancellationFeeSeverity { Free, Fee, LastMinute }

/**
 * Everything the fee card says, resolved from the server's tier and the server's
 * amounts. [amounts] are raw and unformatted — the composable owns the currency.
 */
data class CancellationFeeCallout(
    @StringRes val titleRes: Int,
    @StringRes val amountRes: Int,
    val amounts: List<Double>,
    val severity: CancellationFeeSeverity,
    val warnsExpressWaiverForfeited: Boolean,
    /** The server's grace for this customer, stated on every tier; null states none. */
    val graceMinutes: Int?,
)

/**
 * Null when the tier is missing or unknown to this build: the sheet then says it
 * could not check, which is the only honest answer. Nothing here reads
 * [CancellationFeePreviewDto.feeRate] or a clock — the ladder that used to live
 * in this feature disagreed with the one the customer was actually charged
 * against, and the tier exists so no client rebuilds it.
 */
fun cancellationFeeCallout(
    preview: CancellationFeePreviewDto,
    refundIsEstimate: Boolean = false,
): CancellationFeeCallout? =
    when (cancellationFeeTierFromValue(preview.tier)) {
        CancellationFeeTier.FreeNotAccepted -> preview.free(R.string.order_cancel_fee_not_accepted)
        CancellationFeeTier.FreeOopsWindow -> preview.free(R.string.order_cancel_fee_oops)
        CancellationFeeTier.FreeOutsideWindow -> preview.free(R.string.order_cancel_fee_outside_window)
        CancellationFeeTier.Partial -> preview.charged(
            R.string.order_cancel_fee_partial,
            CancellationFeeSeverity.Fee,
            refundIsEstimate,
        )
        CancellationFeeTier.LastMinute -> preview.charged(
            R.string.order_cancel_fee_last_minute,
            CancellationFeeSeverity.LastMinute,
            refundIsEstimate,
        )
        null -> null
    }

/**
 * Confirm goes live once a reason is picked and the quote has either arrived or
 * definitively failed. Only a quote still in flight holds the button back: a
 * preview outage must never stand between a signed-in customer and cancelling.
 * Guest callers opt into a valid quote before confirming.
 */
fun cancelConfirmEnabled(
    previewState: CancellationPreviewUiState,
    hasReason: Boolean,
    isOtherReason: Boolean,
    notes: String,
    isSubmitting: Boolean,
    requireValidPreview: Boolean = false,
): Boolean = hasReason &&
    // "Other" needs a description so support has something to work with.
    (!isOtherReason || notes.trim().length >= 3) &&
    !isSubmitting &&
    previewState !is CancellationPreviewUiState.Loading &&
    (!requireValidPreview || (previewState is CancellationPreviewUiState.Loaded &&
        cancellationFeeCallout(previewState.preview) != null &&
        !previewState.preview.currencyCode.isNullOrBlank()))

private fun CancellationFeePreviewDto.free(@StringRes titleRes: Int) = CancellationFeeCallout(
    titleRes = titleRes,
    amountRes = R.string.order_cancel_fee_none,
    amounts = emptyList(),
    severity = CancellationFeeSeverity.Free,
    warnsExpressWaiverForfeited = expressWaiverForfeitedOnCancel,
    graceMinutes = oopsWindowMinutes,
)

private fun CancellationFeePreviewDto.charged(
    @StringRes titleRes: Int,
    severity: CancellationFeeSeverity,
    refundIsEstimate: Boolean,
) = CancellationFeeCallout(
    titleRes = titleRes,
    amountRes = if (refundIsEstimate) R.string.guest_order_fee_estimate else R.string.order_cancel_fee_split,
    amounts = listOf(feeAmount, refundAmount),
    severity = severity,
    warnsExpressWaiverForfeited = expressWaiverForfeitedOnCancel,
    graceMinutes = oopsWindowMinutes,
)
