package cz.cleansia.partner.features.orders

import cz.cleansia.partner.api.model.Code
import cz.cleansia.partner.api.model.OrderStatus

/** Convert a backend `Code` (carries the integer value) to the typed enum. */
fun Code?.toOrderStatus(): OrderStatus? = this?.value?.let { v ->
    OrderStatus.values().firstOrNull { it.value == v }
}
