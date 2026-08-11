package cz.cleansia.customer.core.notifications

import cz.cleansia.customer.api.client.NotificationPreferencesApi as GenNotificationPreferencesApi
import cz.cleansia.customer.api.model.NotificationPreferencesDto as GenNotificationPreferencesDto
import cz.cleansia.customer.api.model.UpdateNotificationPreferencesCommand as GenUpdateNotificationPreferencesCommand
import kotlinx.serialization.Serializable
import retrofit2.Response

/**
 * Adapter over the OpenAPI-generated [GenNotificationPreferencesApi]. Mirrors
 * the backend `NotificationPreferencesController` 1:1 — GetMine lazy-creates
 * server-side, Update is replace-all (PUT semantics).
 *
 * The hand-written [NotificationPreferencesPayload] keeps non-null Booleans
 * with stable defaults (Promo defaults to false; the rest to true) so the
 * preferences screen and toggle reads don't have to thread `Boolean?` types.
 */
class NotificationPreferencesApi(
    private val notificationPreferencesApi: GenNotificationPreferencesApi,
) {
    suspend fun getMine(): Response<NotificationPreferencesPayload> {
        val raw = notificationPreferencesApi.notificationPreferencesGetMine()
        return raw.mapBody { it.toAppDto() }
    }

    suspend fun update(body: NotificationPreferencesPayload): Response<NotificationPreferencesPayload> {
        val raw = notificationPreferencesApi.notificationPreferencesUpdate(
            updateNotificationPreferencesCommand = body.toWire(),
        )
        return raw.mapBody { it.toAppDto() }
    }
}

private inline fun <T, R : Any> Response<T>.mapBody(transform: (T?) -> R?): Response<R> =
    if (isSuccessful) Response.success(transform(body()), raw())
    else @Suppress("UNCHECKED_CAST") (this as Response<R>)

/**
 * Every toggle is refused, and this surface is the one where a coerced value does not stay a display
 * bug: `update` is a replace-all PUT of the whole payload, so the next toggle the customer touches
 * writes back all eleven — the client's invention becomes the server's record. A defaulted `promo`
 * of `false` silently unsubscribes someone who opted in, and a defaulted `true` on any of the other
 * ten re-subscribes someone who opted out, which is a consent decision the app has no standing to
 * make on their behalf.
 *
 * A null body is refused rather than being read as a complete set of server-side defaults: the
 * backend lazy-creates the row on read, so "no preferences exist" is not a state this endpoint has.
 */
private fun GenNotificationPreferencesDto?.toAppDto(): NotificationPreferencesPayload? {
    if (this == null) return null
    return NotificationPreferencesPayload(
        orderUpdates = orderUpdates ?: return null,
        cleanerOnTheWay = cleanerOnTheWay ?: return null,
        orderCompleted = orderCompleted ?: return null,
        orderCancelled = orderCancelled ?: return null,
        refundIssued = refundIssued ?: return null,
        membershipExpiring = membershipExpiring ?: return null,
        membershipCancelled = membershipCancelled ?: return null,
        tierUpgrade = tierUpgrade ?: return null,
        promo = promo ?: return null,
        disputeReply = disputeReply ?: return null,
        recurringScheduled = recurringScheduled ?: return null,
    )
}

private fun NotificationPreferencesPayload.toWire(): GenUpdateNotificationPreferencesCommand =
    GenUpdateNotificationPreferencesCommand(
        orderUpdates = orderUpdates,
        cleanerOnTheWay = cleanerOnTheWay,
        orderCompleted = orderCompleted,
        orderCancelled = orderCancelled,
        refundIssued = refundIssued,
        membershipExpiring = membershipExpiring,
        membershipCancelled = membershipCancelled,
        tierUpgrade = tierUpgrade,
        promo = promo,
        disputeReply = disputeReply,
        recurringScheduled = recurringScheduled,
    )

/**
 * Wire shape mirrors `NotificationPreferencesDto` server-side. Same field
 * names — kept here (not moved to the generated client) because the dozen
 * Boolean toggles each map 1:1 onto a switch in the preferences screen, so
 * loose Booleans cost the UI more friction than the wire savings would buy.
 */
@Serializable
data class NotificationPreferencesPayload(
    val orderUpdates: Boolean,
    val cleanerOnTheWay: Boolean,
    val orderCompleted: Boolean,
    val orderCancelled: Boolean,
    val refundIssued: Boolean,
    val membershipExpiring: Boolean,
    val membershipCancelled: Boolean,
    val tierUpgrade: Boolean,
    val promo: Boolean,
    val disputeReply: Boolean,
    val recurringScheduled: Boolean,
)
