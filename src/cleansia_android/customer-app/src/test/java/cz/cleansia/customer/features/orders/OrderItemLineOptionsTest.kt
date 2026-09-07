package cz.cleansia.customer.features.orders

import cz.cleansia.customer.core.orders.OrderDetailDto
import cz.cleansia.customer.core.orders.OrderPackageDetailsDto
import cz.cleansia.customer.core.orders.OrderPackageServiceRefDto
import cz.cleansia.customer.core.orders.OrderServiceDetailsDto
import cz.cleansia.customer.features.disputes.buildDisputeLineOptions
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * How an order's items become something a customer can point at — for a dispute ("this part was not
 * done properly") and for a review ("this part was excellent, that one was not").
 *
 * <p>Both build the same `(serviceId, packageId?)` identity the server uses, and both live here
 * because the one property that matters is shared: the SAME service can be on one order twice, bought
 * alone and inside a bundle, and the two must not collapse into one row. An admin refunding one of
 * them must not refund the other.</p>
 *
 * <p>The other property is quieter and just as load-bearing: an item with no id is skipped rather
 * than shown. A row the customer can tick but the server would reject as not-on-this-order is worse
 * than no row — it produces an error about a box they cannot un-tick.</p>
 */
class OrderItemLineOptionsTest {

    private fun service(id: String?, name: String) =
        OrderServiceDetailsDto(id = id, name = name)

    private fun pkg(id: String?, name: String, items: List<OrderPackageServiceRefDto>) =
        OrderPackageDetailsDto(id = id, name = name, includedServiceItems = items)

    private fun order(
        services: List<OrderServiceDetailsDto> = emptyList(),
        packages: List<OrderPackageDetailsDto> = emptyList(),
    ) = OrderDetailDto(
        id = "o-1",
        totalPrice = 0.0,
        originalSubtotal = 0.0,
        appliedDiscountSource = 0,
        selectedServices = services,
        selectedPackages = packages,
    )

    // ── the shared identity ───────────────────────────────────────────────

    @Test
    fun `a standalone service and the same service inside a package are two rows`() {
        val subject = order(
            services = listOf(service("svc-oven", "Oven clean")),
            packages = listOf(
                pkg("pkg-deep", "Deep Clean", listOf(OrderPackageServiceRefDto("svc-oven", "Oven clean"))),
            ),
        )

        val dispute = buildDisputeLineOptions(subject)
        val review = buildReviewLineOptions(subject)

        assertEquals(2, dispute.size)
        assertEquals(2, dispute.map { it.key }.toSet().size)
        assertEquals(2, review.size)
        assertEquals(2, review.map { it.key }.toSet().size)
        // And the two features agree on what those identities are.
        assertEquals(dispute.map { it.key }, review.map { it.key })
    }

    @Test
    fun `the same service in two different packages stays two rows`() {
        val subject = order(
            packages = listOf(
                pkg("pkg-deep", "Deep Clean", listOf(OrderPackageServiceRefDto("svc-oven", "Oven"))),
                pkg("pkg-move", "Move-out", listOf(OrderPackageServiceRefDto("svc-oven", "Oven"))),
            ),
        )

        assertEquals(2, buildDisputeLineOptions(subject).map { it.key }.toSet().size)
    }

    @Test
    fun `a standalone service carries no package`() {
        val row = buildDisputeLineOptions(order(services = listOf(service("svc-1", "Windows")))).single()

        assertNull(row.packageId)
        assertNull(row.packageLabel)
        assertEquals("Windows", row.label)
    }

    @Test
    fun `an in-package service names the bundle it came from`() {
        val row = buildReviewLineOptions(
            order(
                packages = listOf(
                    pkg("pkg-deep", "Deep Clean", listOf(OrderPackageServiceRefDto("svc-oven", "Oven"))),
                ),
            ),
        ).single()

        assertEquals("pkg-deep", row.packageId)
        assertEquals("Deep Clean", row.packageLabel)
        assertEquals("Oven", row.label)
    }

    // ── what must not be offered ──────────────────────────────────────────

    @Test
    fun `an item with no id is skipped, not shown`() {
        val subject = order(
            services = listOf(service(null, "Nameless"), service("svc-ok", "Fine")),
            packages = listOf(
                pkg("pkg-1", "Bundle", listOf(
                    OrderPackageServiceRefDto(null, "Nameless too"),
                    OrderPackageServiceRefDto("svc-in", "Also fine"),
                )),
            ),
        )

        assertEquals(listOf("svc-ok", "svc-in"), buildDisputeLineOptions(subject).map { it.serviceId })
        assertEquals(listOf("svc-ok", "svc-in"), buildReviewLineOptions(subject).map { it.serviceId })
    }

    @Test
    fun `a package with no id contributes nothing, even if its items have ids`() {
        val subject = order(
            packages = listOf(
                pkg(null, "Bundle", listOf(OrderPackageServiceRefDto("svc-in", "Oven"))),
            ),
        )

        assertTrue(buildDisputeLineOptions(subject).isEmpty())
    }

    @Test
    fun `no order at all answers empty, so the section hides itself`() {
        assertTrue(buildDisputeLineOptions(null).isEmpty())
        assertTrue(buildReviewLineOptions(null).isEmpty())
    }

    @Test
    fun `an order with nothing on it answers empty`() {
        assertTrue(buildDisputeLineOptions(order()).isEmpty())
        assertTrue(buildReviewLineOptions(order()).isEmpty())
    }

    /**
     * `includedServices` is a list of NAMES and cannot be sent back to the server. Reading it by
     * mistake would produce rows the customer can tick and the server always rejects.
     */
    @Test
    fun `the name-only includedServices list is never used to build rows`() {
        val subject = order(
            packages = listOf(
                OrderPackageDetailsDto(
                    id = "pkg-1",
                    name = "Bundle",
                    includedServices = listOf("Oven clean", "Windows"),
                    includedServiceItems = null,
                ),
            ),
        )

        assertTrue(buildDisputeLineOptions(subject).isEmpty())
        assertTrue(buildReviewLineOptions(subject).isEmpty())
    }
}
