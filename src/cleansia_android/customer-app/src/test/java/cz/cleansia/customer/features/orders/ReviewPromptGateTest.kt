package cz.cleansia.customer.features.orders

import cz.cleansia.customer.core.orders.OrderListItemDto
import cz.cleansia.customer.core.orders.ReviewTag
import cz.cleansia.customer.core.user.CodeDto
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * The rule that decides whether to interrupt a customer on app open, tested directly.
 *
 * There is no androidTest source set on this app, so a rule left inside the composable would be
 * untestable — which is why it is a pure function. Same shape as `CancelConfirmGateTest`.
 */
class ReviewPromptGateTest {

    private fun order(
        id: String,
        statusValue: Int = 5,
        hasReview: Boolean = false,
        cleaningDateTime: String? = "2026-08-20T09:00:00Z",
    ) = OrderListItemDto(
        id = id,
        // The gate reads four fields; the rest are required by the DTO and irrelevant here.
        totalPrice = 0.0,
        originalSubtotal = 0.0,
        appliedDiscountSource = 0,
        orderStatus = CodeDto(type = "OrderStatus", name = "Completed", value = statusValue),
        hasReview = hasReview,
        cleaningDateTime = cleaningDateTime,
    )

    @Test
    fun `a completed unreviewed order is the candidate`() {
        val candidate = ReviewPromptGate.candidate(listOf(order("ord-1")), emptySet())

        assertEquals("ord-1", candidate?.id)
    }

    @Test
    fun `an order the server says is already reviewed is never offered`() {
        assertNull(ReviewPromptGate.candidate(listOf(order("ord-1", hasReview = true)), emptySet()))
    }

    @Test
    fun `an order already prompted for is not offered again`() {
        assertNull(ReviewPromptGate.candidate(listOf(order("ord-1")), setOf("ord-1")))
    }

    @Test
    fun `orders that are not completed are never offered`() {
        // 0 New, 2 Confirmed, 3 OnTheWay, 4 InProgress, 6 Cancelled — none of them finished work.
        listOf(0, 2, 3, 4, 6).forEach { status ->
            assertNull(
                "status $status must not be offered for review",
                ReviewPromptGate.candidate(listOf(order("ord-$status", statusValue = status)), emptySet()),
            )
        }
    }

    @Test
    fun `the newest completed order wins, not the oldest`() {
        val candidate = ReviewPromptGate.candidate(
            listOf(
                order("old", cleaningDateTime = "2026-07-01T09:00:00Z"),
                order("new", cleaningDateTime = "2026-08-20T09:00:00Z"),
                order("middle", cleaningDateTime = "2026-08-01T09:00:00Z"),
            ),
            emptySet(),
        )

        assertEquals("new", candidate?.id)
    }

    @Test
    fun `an empty list asks nothing`() {
        assertNull(ReviewPromptGate.candidate(emptyList(), emptySet()))
    }
}

/**
 * The chip set is a function of the rating, and its wire values are the contract three clients decode.
 */
class ReviewTagTest {

    @Test
    fun `low ratings offer only negative tags`() {
        (1..3).forEach { rating ->
            val offered = ReviewTag.forRating(rating)
            assertTrue("rating $rating offered nothing", offered.isNotEmpty())
            assertTrue("rating $rating offered a positive tag", offered.none { it.isPositive })
        }
    }

    @Test
    fun `high ratings offer only positive tags`() {
        (4..5).forEach { rating ->
            val offered = ReviewTag.forRating(rating)
            assertTrue("rating $rating offered nothing", offered.isNotEmpty())
            assertTrue("rating $rating offered a negative tag", offered.all { it.isPositive })
        }
    }

    @Test
    fun `a rating outside one to five offers nothing`() {
        listOf(0, 6, -1).forEach { assertTrue(ReviewTag.forRating(it).isEmpty()) }
    }

    @Test
    fun `every tag sits in exactly one polarity band`() {
        val positive = ReviewTag.forRating(5)
        val negative = ReviewTag.forRating(1)

        assertEquals(ReviewTag.entries.size, positive.size + negative.size)
        assertTrue(positive.intersect(negative.toSet()).isEmpty())
    }

    /**
     * The integers ARE the wire contract — the backend enum, the OpenAPI spec and this list must
     * agree, and a renumbering silently reinterprets every stored review.
     */
    @Test
    fun `wire codes are frozen`() {
        assertEquals(
            listOf(1, 2, 3, 4, 5, 6, 7, 11, 12, 13, 14, 15, 16, 17, 18),
            ReviewTag.entries.map { it.code },
        )
    }

    @Test
    fun `an unknown code resolves to null rather than throwing`() {
        assertNull(ReviewTag.fromCode(999))
    }
}
