package cz.cleansia.customer.core.notifications

import kotlinx.serialization.Serializable
import retrofit2.Response
import retrofit2.http.Body
import retrofit2.http.GET
import retrofit2.http.POST
import retrofit2.http.Query

/**
 * Hand-written Retrofit interface for the notifications feed endpoints —
 * same precedent as [cz.cleansia.customer.core.devices.DeviceManagementApi]:
 * the generated client lags the spec, so the wire shapes live here. The
 * `audience` field on the mark commands is enriched server-side from the
 * calling host, so requests omit it.
 */
interface NotificationFeedApi {

    @GET("api/Notification/Paged")
    suspend fun paged(
        @Query("pageNumber") pageNumber: Int,
        @Query("pageSize") pageSize: Int,
    ): Response<PagedNotificationsDto>

    @GET("api/Notification/UnreadCount")
    suspend fun unreadCount(): Response<UnreadNotificationCountDto>

    @POST("api/Notification/MarkRead")
    suspend fun markRead(
        @Body body: MarkNotificationReadRequest,
    ): Response<MarkNotificationReadResponse>

    @POST("api/Notification/MarkAllRead")
    suspend fun markAllRead(
        @Body body: MarkAllNotificationsReadRequest,
    ): Response<MarkAllNotificationsReadResponse>
}

@Serializable
data class UserNotificationDto(
    val id: String,
    val eventKey: String,
    /** The push Args dictionary verbatim — templates substitute from it client-side. */
    val args: Map<String, String> = emptyMap(),
    /** ISO-8601 UTC, e.g. "2026-07-18T09:00:00+00:00". */
    val createdOn: String,
    /** Null = unread at fetch time. */
    val readOn: String? = null,
)

@Serializable
data class PagedNotificationsDto(
    val pageNumber: Int = 1,
    val pageSize: Int = 0,
    val total: Int = 0,
    val data: List<UserNotificationDto> = emptyList(),
)

@Serializable
data class UnreadNotificationCountDto(
    val count: Int = 0,
)

@Serializable
data class MarkNotificationReadRequest(
    val id: String,
)

@Serializable
data class MarkNotificationReadResponse(
    val id: String? = null,
    val readOn: String? = null,
)

@Serializable
data class MarkAllNotificationsReadRequest(
    val upToCreatedOn: String,
)

@Serializable
data class MarkAllNotificationsReadResponse(
    val markedCount: Int = 0,
)
