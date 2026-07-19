package cz.cleansia.partner.features.notifications

import android.content.Context
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.partner.core.network.ApiErrorTranslator
import cz.cleansia.partner.core.notifications.NotificationDeepLink
import cz.cleansia.partner.core.notifications.NotificationFeedRepository
import cz.cleansia.partner.core.notifications.NotificationTemplates
import cz.cleansia.partner.core.notifications.UserNotificationDto
import cz.cleansia.partner.navigation.NavRoute
import dagger.hilt.android.lifecycle.HiltViewModel
import dagger.hilt.android.qualifiers.ApplicationContext
import kotlinx.coroutines.flow.MutableSharedFlow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.SharedFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asSharedFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch
import javax.inject.Inject

sealed interface NotificationsUiState {
    data object Loading : NotificationsUiState
    data object Error : NotificationsUiState
    data class Loaded(
        val items: List<NotificationFeedItem>,
        val canLoadMore: Boolean,
        val loadingMore: Boolean = false,
    ) : NotificationsUiState
}

/** A feed row with its template already rendered in the device locale. */
data class NotificationFeedItem(
    val id: String,
    val eventKey: String,
    val title: String,
    val body: String,
    val createdOn: String,
    /** Fetched-as-unread; kept for the viewing session, cleared only by a tap. */
    val unread: Boolean,
    val args: Map<String, String>,
)

/**
 * Backs the notifications feed off the server inbox ([NotificationFeedRepository]).
 * Fetches page 1 on every open, fires the watermarked mark-all so the bell
 * badge clears, appends pages on scroll, and resolves row taps through the same
 * [NotificationDeepLink] the system-notification tap uses.
 */
@HiltViewModel
class NotificationsViewModel @Inject constructor(
    private val repository: NotificationFeedRepository,
    private val snackbar: SnackbarController,
    private val errorTranslator: ApiErrorTranslator,
    @ApplicationContext private val appContext: Context,
) : ViewModel() {

    private val _state = MutableStateFlow<NotificationsUiState>(NotificationsUiState.Loading)
    val state: StateFlow<NotificationsUiState> = _state.asStateFlow()

    private val _openRoute = MutableSharedFlow<NavRoute>(extraBufferCapacity = 1)
    val openRoute: SharedFlow<NavRoute> = _openRoute.asSharedFlow()

    private var pageNumber = 0
    private var fetchedCount = 0
    private var total = 0

    /** Called on every screen open (and on retry) — always refetches page 1. */
    fun open() {
        viewModelScope.launch {
            _state.value = NotificationsUiState.Loading
            pageNumber = 0
            fetchedCount = 0
            total = 0
            when (val result = repository.getPage(pageNumber = 1)) {
                is ApiResult.Success -> {
                    val page = result.data
                    pageNumber = 1
                    fetchedCount = page.data.size
                    total = page.total
                    _state.value = NotificationsUiState.Loaded(
                        items = page.data.mapNotNull { it.toFeedItem() },
                        canLoadMore = fetchedCount < total,
                    )
                    markFetchedSeen(page.data)
                }
                is ApiResult.Error -> {
                    snackbar.showError(errorTranslator.translate(result.error))
                    _state.value = NotificationsUiState.Error
                }
            }
        }
    }

    fun loadMore() {
        val current = _state.value as? NotificationsUiState.Loaded ?: return
        if (!current.canLoadMore || current.loadingMore) return
        _state.value = current.copy(loadingMore = true)
        viewModelScope.launch {
            when (val result = repository.getPage(pageNumber = pageNumber + 1)) {
                is ApiResult.Success -> {
                    val page = result.data
                    pageNumber += 1
                    fetchedCount += page.data.size
                    total = page.total
                    val latest = _state.value as? NotificationsUiState.Loaded ?: return@launch
                    _state.value = latest.copy(
                        items = latest.items + page.data.mapNotNull { it.toFeedItem() },
                        canLoadMore = page.data.isNotEmpty() && fetchedCount < total,
                        loadingMore = false,
                    )
                }
                is ApiResult.Error -> {
                    snackbar.showError(errorTranslator.translate(result.error))
                    val latest = _state.value as? NotificationsUiState.Loaded ?: return@launch
                    _state.value = latest.copy(loadingMore = false)
                }
            }
        }
    }

    fun onRowClick(item: NotificationFeedItem) {
        if (item.unread) {
            (_state.value as? NotificationsUiState.Loaded)?.let { loaded ->
                _state.value = loaded.copy(
                    items = loaded.items.map { if (it.id == item.id) it.copy(unread = false) else it },
                )
            }
            repository.decrementUnread()
            viewModelScope.launch { repository.markRead(item.id) }
        }
        NotificationDeepLink.resolve(item.eventKey, item.args["orderId"], item.args["invoiceId"])
            ?.let { _openRoute.tryEmit(it) }
    }

    /**
     * The watermarked open-marks-seen: `upToCreatedOn` is the newest FETCHED
     * row's createdOn (the server returns CreatedOn desc), so a row that
     * arrives after this fetch stays unread. Rendered unread dots keep their
     * fetched state for the viewing session; only the badge refreshes.
     */
    private suspend fun markFetchedSeen(fetched: List<UserNotificationDto>) {
        val watermark = fetched.firstOrNull()?.createdOn ?: return
        if (repository.markAllRead(upToCreatedOn = watermark).isSuccess) {
            repository.refreshUnreadCount()
        }
    }

    private fun UserNotificationDto.toFeedItem(): NotificationFeedItem? {
        val template = NotificationTemplates.templateFor(eventKey) ?: return null
        return NotificationFeedItem(
            id = id,
            eventKey = eventKey,
            title = appContext.getString(template.titleRes),
            body = NotificationTemplates.formatBody(appContext, eventKey, template.bodyRes, args),
            createdOn = createdOn,
            unread = readOn == null,
            args = args,
        )
    }
}
