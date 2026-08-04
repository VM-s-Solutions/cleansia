package cz.cleansia.customer.features.orders

import cz.cleansia.customer.core.orders.CancellationFeePreviewDto
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * A fee-preview outage must never block a cancellation — the guarantee that
 * makes "stop guessing the fee" safe to ship. Only a quote still in flight
 * holds the confirm button.
 */
class CancelConfirmGateTest {

    private val loaded = CancellationPreviewUiState.Loaded(
        CancellationFeePreviewDto(orderId = "order-1", tier = 3, feeAmount = 250.0, refundAmount = 750.0),
    )

    private fun enabled(
        previewState: CancellationPreviewUiState,
        hasReason: Boolean = true,
        isOtherReason: Boolean = false,
        notes: String = "",
        isSubmitting: Boolean = false,
    ) = cancelConfirmEnabled(previewState, hasReason, isOtherReason, notes, isSubmitting)

    @Test
    fun `a failed preview still lets the customer cancel`() {
        assertTrue(enabled(CancellationPreviewUiState.Error))
    }

    @Test
    fun `a quote in flight holds the button`() {
        assertFalse(enabled(CancellationPreviewUiState.Loading))
    }

    @Test
    fun `a delivered quote lets the customer cancel`() {
        assertTrue(enabled(loaded))
    }

    @Test
    fun `no reason picked, no confirm`() {
        assertFalse(enabled(loaded, hasReason = false))
    }

    @Test
    fun `Other needs a description`() {
        assertFalse(enabled(loaded, isOtherReason = true, notes = "  x "))
        assertTrue(enabled(loaded, isOtherReason = true, notes = "  gate broke "))
    }

    @Test
    fun `a submit in flight holds the button`() {
        assertFalse(enabled(loaded, isSubmitting = true))
        assertFalse(enabled(CancellationPreviewUiState.Error, isSubmitting = true))
    }
}
