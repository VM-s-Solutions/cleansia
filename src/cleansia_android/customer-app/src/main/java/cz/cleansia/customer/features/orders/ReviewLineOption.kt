package cz.cleansia.customer.features.orders

import cz.cleansia.customer.core.orders.OrderDetailDto

/**
 * One item of an order the customer can score separately.
 *
 * The identity is the same `(serviceId, packageId?)` pair a dispute line uses, so both name an
 * order's items the same way. A service bought on its own leaves [packageId] null; a service that
 * came inside a bundle carries both, because the same service can appear twice on one order.
 *
 * → `SubmitOrderReview.ReviewLineScore`
 */
data class ReviewLineOption(
    val serviceId: String,
    val packageId: String?,
    val label: String,
    /** The bundle it came in, so two rows sharing a name are distinguishable. Null when standalone. */
    val packageLabel: String?,
) {
    val key: String get() = reviewLineKey(serviceId, packageId)
}

/** Stable within one order: the server's own identity, flattened for use as a map key. */
fun reviewLineKey(serviceId: String, packageId: String?): String = "${packageId.orEmpty()}|$serviceId"

/**
 * Every item on the order that can be scored: the standalone services, then the services inside each
 * package.
 *
 * <p>Reads `includedServiceItems` and not `includedServices` — the latter is a list of NAMES and
 * cannot be sent back to the server. An item with no id is skipped rather than shown: a row the
 * customer can score but the server would reject is worse than no row.</p>
 */
fun buildReviewLineOptions(order: OrderDetailDto?): List<ReviewLineOption> {
    if (order == null) return emptyList()

    val options = mutableListOf<ReviewLineOption>()

    order.selectedServices.orEmpty().forEach { service ->
        val id = service.id ?: return@forEach
        options += ReviewLineOption(
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
            options += ReviewLineOption(
                serviceId = id,
                packageId = packageId,
                label = included.name.orEmpty(),
                packageLabel = packageLabel,
            )
        }
    }

    return options
}
