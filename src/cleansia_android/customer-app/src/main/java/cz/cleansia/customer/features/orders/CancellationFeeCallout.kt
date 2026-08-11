package cz.cleansia.customer.features.orders

import androidx.annotation.StringRes
import cz.cleansia.customer.R
import cz.cleansia.customer.core.orders.CancellationFeePreviewDto

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
)

/**
 * Null when the tier is missing or unknown to this build: the sheet then says it
 * could not check, which is the only honest answer. Nothing here reads
 * [CancellationFeePreviewDto.feeRate] or a clock — the ladder that used to live
 * in this feature disagreed with the one the customer was actually charged
 * against, and the tier exists so no client rebuilds it.
 */
fun cancellationFeeCallout(preview: CancellationFeePreviewDto): CancellationFeeCallout? =
    when (cancellationFeeTierFromValue(preview.tier)) {
        CancellationFeeTier.FreeNotAccepted -> preview.free(R.string.order_cancel_fee_not_accepted)
        CancellationFeeTier.FreeOopsWindow -> preview.free(R.string.order_cancel_fee_oops)
        CancellationFeeTier.FreeOutsideWindow -> preview.free(R.string.order_cancel_fee_outside_window)
        CancellationFeeTier.Partial -> preview.charged(
            R.string.order_cancel_fee_partial,
            CancellationFeeSeverity.Fee,
        )
        CancellationFeeTier.LastMinute -> preview.charged(
            R.string.order_cancel_fee_last_minute,
            CancellationFeeSeverity.LastMinute,
        )
        null -> null
    }

/**
 * Confirm goes live once a reason is picked and the quote has either arrived or
 * definitively failed. Only a quote still in flight holds the button back: a
 * preview outage must never stand between a customer and cancelling.
 */
fun cancelConfirmEnabled(
    previewState: CancellationPreviewUiState,
    hasReason: Boolean,
    isOtherReason: Boolean,
    notes: String,
    isSubmitting: Boolean,
): Boolean = hasReason &&
    // "Other" needs a description so support has something to work with.
    (!isOtherReason || notes.trim().length >= 3) &&
    !isSubmitting &&
    previewState !is CancellationPreviewUiState.Loading

private fun CancellationFeePreviewDto.free(@StringRes titleRes: Int) = CancellationFeeCallout(
    titleRes = titleRes,
    amountRes = R.string.order_cancel_fee_none,
    amounts = emptyList(),
    severity = CancellationFeeSeverity.Free,
    warnsExpressWaiverForfeited = expressWaiverForfeitedOnCancel,
)

private fun CancellationFeePreviewDto.charged(
    @StringRes titleRes: Int,
    severity: CancellationFeeSeverity,
) = CancellationFeeCallout(
    titleRes = titleRes,
    amountRes = R.string.order_cancel_fee_split,
    amounts = listOf(feeAmount, refundAmount),
    severity = severity,
    warnsExpressWaiverForfeited = expressWaiverForfeitedOnCancel,
)
