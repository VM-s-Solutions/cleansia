package cz.cleansia.partner.features.dashboard

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.partner.api.model.AvailableJobsPreviewResponse
import cz.cleansia.partner.api.model.DashboardStatsDto
import cz.cleansia.partner.api.model.OrderListItem
import cz.cleansia.partner.core.auth.UserProfileStore
import cz.cleansia.partner.core.network.ApiErrorTranslator
import cz.cleansia.partner.core.notifications.NotificationFeedRepository
import cz.cleansia.partner.data.dashboard.DashboardRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.SharingStarted
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.combine
import kotlinx.coroutines.flow.stateIn
import kotlinx.coroutines.launch
import javax.inject.Inject

/**
 * State the redesigned dashboard reads, projected from the singleton
 * [DashboardRepository] cache so swiping back to the tab shows data
 * instantly instead of a spinner; only the genuine first load (no cached
 * stats while a refresh is in flight) shows the [Loading] spinner.
 *
 * [Loaded] carries a nullable stats so the cards render with their
 * own zero/placeholder fallbacks in the brief pre-load and error-cleared
 * moments, matching the cache projection. [isUserRefreshing] drives the
 * pull-to-refresh suds indicator; background refreshes (init / ON_RESUME /
 * post-mutation) never set it, so they render silently.
 */
sealed interface DashboardUiState {
    val isUserRefreshing: Boolean

    data class Loading(override val isUserRefreshing: Boolean) : DashboardUiState

    data class Loaded(
        val stats: DashboardStatsDto?,
        val upcoming: List<OrderListItem>,
        val availableJobsPreview: AvailableJobsPreviewResponse?,
        override val isUserRefreshing: Boolean,
    ) : DashboardUiState
}

private enum class RefreshSource { INIT, RESUME, USER_PULL }

@HiltViewModel
class DashboardViewModel @Inject constructor(
    private val dashboardRepository: DashboardRepository,
    private val userProfileStore: UserProfileStore,
    private val snackbar: SnackbarController,
    private val errorTranslator: ApiErrorTranslator,
    private val notificationFeedRepository: NotificationFeedRepository,
) : ViewModel() {

    /** Server unread count driving the bell badge; refetched on entry/resume,
     * bumped locally by the FCM receive path, cleared when the feed opens. */
    val unreadNotifications: StateFlow<Int> = notificationFeedRepository.unreadCount

    // firstName comes from the cached user profile, not the dashboard
    // network call — so the greeting renders immediately, independent
    // of the stats refresh.
    private val _firstName = MutableStateFlow<String?>(null)
    val firstName: StateFlow<String?> = _firstName

    // Local routing flag so we can tell user-pull refreshes apart from
    // background ones. The repository tracks a single `refreshing` bit;
    // here we lift that into isUserRefreshing. Set BEFORE we hand off to
    // the repository, cleared in the finally of [load].
    private val _userPullInFlight = MutableStateFlow(false)

    val uiState: StateFlow<DashboardUiState> =
        combine(
            dashboardRepository.snapshot,
            _userPullInFlight,
        ) { snap, userPulling ->
            val isUserRefreshing = snap.refreshing && userPulling
            if (snap.stats == null && snap.refreshing) {
                DashboardUiState.Loading(isUserRefreshing = isUserRefreshing)
            } else {
                DashboardUiState.Loaded(
                    stats = snap.stats,
                    upcoming = snap.upcoming,
                    availableJobsPreview = snap.availableJobsPreview,
                    isUserRefreshing = isUserRefreshing,
                )
            }
        }.stateIn(
            scope = viewModelScope,
            started = SharingStarted.WhileSubscribed(5_000),
            initialValue = DashboardUiState.Loaded(
                stats = null,
                upcoming = emptyList(),
                availableJobsPreview = null,
                isUserRefreshing = false,
            ),
        )

    init {
        viewModelScope.launch { _firstName.value = userProfileStore.current()?.firstName }
        load(RefreshSource.INIT)
        refreshNotificationBadge()
    }

    /** Pull-to-refresh — always hits the network, surfaces as isUserRefreshing. */
    fun refresh() = load(RefreshSource.USER_PULL)

    fun onResume() {
        load(RefreshSource.RESUME)
        refreshNotificationBadge()
    }

    /** Silent on failure — the badge is ambient chrome that self-heals on the
     * next resume and on every feed open. */
    private fun refreshNotificationBadge() {
        viewModelScope.launch { notificationFeedRepository.refreshUnreadCount() }
    }

    private fun load(source: RefreshSource) {
        viewModelScope.launch {
            val isUserPull = source == RefreshSource.USER_PULL
            if (isUserPull) _userPullInFlight.value = true
            try {
                val employeeId = userProfileStore.current()?.employeeId
                val error = dashboardRepository.refresh(employeeId, force = isUserPull)
                if (error != null) {
                    snackbar.showError(errorTranslator.translate(error))
                }
            } finally {
                if (isUserPull) _userPullInFlight.value = false
            }
        }
    }
}
