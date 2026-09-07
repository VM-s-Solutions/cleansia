package cz.cleansia.customer.features.disputes

import cz.cleansia.customer.core.orders.OrderDetailDto

/**
 * One item of an order a customer can point at when filing a dispute.
 *
 * The identity is the PAIR the server uses — `(serviceId, packageId?)`. A service bought on its own
 * leaves [packageId] null; a service that came inside a bundle carries both, because the same service
 * can appear twice on one order and an admin refunding one must not refund the other.
 *
 * → `CreateDispute.DisputeLineSelection`
 */
data class DisputeLineOption(
    val serviceId: String,
    val packageId: String?,
    /** What the customer reads. */
    val label: String,
    /** The bundle it came in, so two rows sharing a name are distinguishable. Null when standalone. */
    val packageLabel: String?,
) {
    /** Stable within one order: the server's own identity, flattened for use as a set key. */
    val key: String get() = "${packageId.orEmpty()}|$serviceId"
}

/**
 * Every item on the order that can be named: the standalone services, then the services inside each
 * package.
 *
 * <p>Reads `includedServiceItems` and not `includedServices` — the latter is a list of NAMES and
 * cannot be sent back to the server, so it can be printed but never selected. An item with no id is
 * skipped rather than shown: a row the customer can tick but the server would reject is worse than
 * no row.</p>
 */
fun buildDisputeLineOptions(order: OrderDetailDto?): List<DisputeLineOption> {
    if (order == null) return emptyList()

    val options = mutableListOf<DisputeLineOption>()

    order.selectedServices.orEmpty().forEach { service ->
        val id = service.id ?: return@forEach
        options += DisputeLineOption(
            serviceId = id,
            packageId = null,
            label = service.name.orEmpty(),
            packageLabel = null,
        )
    }

    order.selectedPackages.orEmpty().forEach { pkg ->
        val packageId = pkg.id ?: return@forEach
        val packageLabel = pkg.name.orEmpty()
        pkg.includedServiceItems.orEmpty().forEach { included ->
            val id = included.id ?: return@forEach
            options += DisputeLineOption(
                serviceId = id,
                packageId = packageId,
                label = included.name.orEmpty(),
                packageLabel = packageLabel,
            )
        }
    }

    return options
}
