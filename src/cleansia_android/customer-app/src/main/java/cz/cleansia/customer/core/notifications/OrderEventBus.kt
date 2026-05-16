package cz.cleansia.customer.core.notifications

import javax.inject.Inject
import javax.inject.Singleton
import kotlinx.coroutines.flow.MutableSharedFlow
import kotlinx.coroutines.flow.SharedFlow
import kotlinx.coroutines.flow.asSharedFlow

/**
 * Process-wide bus for order push events. The FCM service emits whenever a
 * `order.*` payload arrives so screens that show order state can refetch
 * immediately instead of waiting for the next poll tick. Replaces the bulk
 * of [OrderDetailViewModel]'s 30-second polling — the timer stays as a
 * safety net for missed pushes (FCM rate-limits, app backgrounded too long).
 *
 * `extraBufferCapacity = 8` so a burst of events (e.g. confirmed → on the
 * way fired close together) doesn't drop on a slow collector. Collectors
 * filter by orderId themselves.
 */
@Singleton
class OrderEventBus @Inject constructor() {

    private val _events = MutableSharedFlow<OrderEvent>(
        replay = 0,
        extraBufferCapacity = 8,
    )

    val events: SharedFlow<OrderEvent> = _events.asSharedFlow()

    /** Emit from the FCM message handler. Non-suspending; tryEmit is safe here. */
    fun emit(event: OrderEvent) {
        _events.tryEmit(event)
    }
}

/**
 * Subset of an FCM `order.*` payload that the detail VM cares about. The
 * full payload (event_key, orderNumber, etc.) lives on the notification
 * itself; the bus only needs the orderId to route refetches.
 */
data class OrderEvent(
    val orderId: String,
    /** The raw `event_key` (e.g. "order.completed") for callers that want to react differently. */
    val eventKey: String,
)
