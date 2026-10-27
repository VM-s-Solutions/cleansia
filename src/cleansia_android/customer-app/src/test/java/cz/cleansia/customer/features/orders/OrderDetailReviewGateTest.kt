package cz.cleansia.customer.features.orders

import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class OrderDetailReviewGateTest {
    @Test
    fun `a completed own unrated order offers a rating`() {
        assertTrue(customerCanLeaveReview(5, isGuestBooking = false, hasReview = false))
    }

    @Test
    fun `every other status withholds the rating action`() {
        listOf(null, 0, 1, 2, 3, 4, 6, 99).forEach { status ->
            assertFalse("status $status", customerCanLeaveReview(status, isGuestBooking = false, hasReview = false))
        }
    }

    @Test
    fun `a guest booking never offers a rating in any status`() {
        (0..6).forEach { status ->
            assertFalse("status $status", customerCanLeaveReview(status, isGuestBooking = true, hasReview = false))
        }
    }

    @Test
    fun `a saved review removes the pending rating action in any status`() {
        (0..6).forEach { status ->
            assertFalse("status $status", customerCanLeaveReview(status, isGuestBooking = false, hasReview = true))
        }
    }
}
