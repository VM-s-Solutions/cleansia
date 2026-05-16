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

private fun GenNotificationPreferencesDto?.toAppDto(): NotificationPreferencesPayload =
    NotificationPreferencesPayload(
        orderUpdates = this?.orderUpdates ?: true,
        cleanerOnTheWay = this?.cleanerOnTheWay ?: true,
        orderCompleted = this?.orderCompleted ?: true,
        orderCancelled = this?.orderCancelled ?: true,
        refundIssued = this?.refundIssued ?: true,
        membershipExpiring = this?.membershipExpiring ?: true,
        membershipCancelled = this?.membershipCancelled ?: true,
        tierUpgrade = this?.tierUpgrade ?: true,
        // Promo is opt-in — server-side default is false so the user has to
        // explicitly turn it on. Mirror that here.
        promo = this?.promo ?: false,
        disputeReply = this?.disputeReply ?: true,
        recurringScheduled = this?.recurringScheduled ?: true,
    )

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
    val orderUpdates: Boolean = true,
    val cleanerOnTheWay: Boolean = true,
    val orderCompleted: Boolean = true,
    val orderCancelled: Boolean = true,
    val refundIssued: Boolean = true,
    val membershipExpiring: Boolean = true,
    val membershipCancelled: Boolean = true,
    val tierUpgrade: Boolean = true,
    val promo: Boolean = false,
    val disputeReply: Boolean = true,
    val recurringScheduled: Boolean = true,
)
