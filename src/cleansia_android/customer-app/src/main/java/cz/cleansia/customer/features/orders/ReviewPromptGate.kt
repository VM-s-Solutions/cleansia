package cz.cleansia.customer.features.orders

import cz.cleansia.customer.core.orders.OrderListItemDto

/**
 * Which completed order, if any, the customer should be asked to review on app open.
 *
 * A pure function over the warm list cache, hoisted out of the shell for the same reason
 * [CancelConfirmGate] is: a rule that decides whether to interrupt someone deserves to be tested
 * directly rather than through a composable that cannot be instrumented on this app (there is no
 * androidTest source set).
 *
 * **Server truth wins over the local flag.** `hasReview` comes from the order payload, so a review
 * left on another device suppresses the prompt here too; the DataStore flag only stops us asking
 * twice about an order the customer has not reviewed.
 */
object ReviewPromptGate {
    /** Fulfilment status 5 — Completed. The list carries the code, not the enum. */
    private const val COMPLETED = 5

    /**
     * The newest completed, unreviewed order, or null.
     *
     * Newest rather than oldest deliberately: the prompt lands right after a clean finishes, and being
     * asked about the freshest one is what the customer expects. An older unreviewed order is not
     * chased — a backlog of prompts is how this pattern becomes noise.
     */
    fun candidate(
        orders: List<OrderListItemDto>,
        alreadyPrompted: Set<String>,
    ): OrderListItemDto? = orders
        .filter { it.orderStatus?.value == COMPLETED }
        .filterNot { it.hasReview }
        .filterNot { it.id in alreadyPrompted }
        .maxByOrNull { it.cleaningDateTime.orEmpty() }
}
