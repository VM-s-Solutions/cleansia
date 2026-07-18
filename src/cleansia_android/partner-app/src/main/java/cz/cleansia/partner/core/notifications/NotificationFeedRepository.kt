package cz.cleansia.partner.core.notifications

import cz.cleansia.core.auth.SessionScopedCache
import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.network.safeApiCall
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.serialization.json.Json
import javax.inject.Inject
import javax.inject.Singleton

/**
 * Server-backed notifications feed (paged list, unread badge, read state) —
 * the single source of truth that replaced the interim Room store. Holds the
 * per-user unread count as a hot flow so the dashboard bell, the feed screen
 * and the FCM receive path share one badge source — which makes it a
 * [SessionScopedCache] member (wiped on sign-out/forced-401/deletion).
 */
@Singleton
class NotificationFeedRepository @Inject constructor(
    private val api: NotificationFeedApi,
    private val json: Json,
) : SessionScopedCache {

    private val _unreadCount = MutableStateFlow(0)
    val unreadCount: StateFlow<Int> = _unreadCount.asStateFlow()

    suspend fun getPage(pageNumber: Int, pageSize: Int = PAGE_SIZE): ApiResult<PagedNotificationsDto> =
        when (val result = safeApiCall(json) { api.paged(pageNumber, pageSize) }) {
            is ApiResult.Success ->
                ApiResult.Success(result.data as? PagedNotificationsDto ?: PagedNotificationsDto())
            is ApiResult.Error -> result
        }

    suspend fun refreshUnreadCount(): ApiResult<Int> =
        when (val result = safeApiCall(json) { api.unreadCount() }) {
            is ApiResult.Success -> {
                val count = (result.data as? UnreadNotificationCountDto)?.count ?: 0
                _unreadCount.value = count
                ApiResult.Success(count)
            }
            is ApiResult.Error -> result
        }

    /** Idempotent server-side — the first ReadOn wins. */
    suspend fun markRead(id: String): ApiResult<Unit> =
        safeApiCall(json) { api.markRead(MarkNotificationReadRequest(id)) }.map { }

    /**
     * Watermarked mark-all: only rows with `CreatedOn <= upToCreatedOn` are
     * marked, so a row that arrives after the caller's fetch stays unread.
     */
    suspend fun markAllRead(upToCreatedOn: String): ApiResult<Unit> =
        safeApiCall(json) { api.markAllRead(MarkAllNotificationsReadRequest(upToCreatedOn)) }.map { }

    /** Local badge bump on FCM receipt — the server row already exists, no refetch needed. */
    fun onPushReceived() {
        _unreadCount.update { it + 1 }
    }

    /** Optimistic badge drop when a row is marked read from the feed. */
    fun decrementUnread() {
        _unreadCount.update { (it - 1).coerceAtLeast(0) }
    }

    override suspend fun clear() {
        _unreadCount.value = 0
    }

    companion object {
        const val PAGE_SIZE = 20
    }
}
