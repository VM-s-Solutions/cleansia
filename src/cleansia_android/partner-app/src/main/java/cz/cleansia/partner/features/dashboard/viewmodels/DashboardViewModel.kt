package cz.cleansia.partner.features.dashboard.viewmodels

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.partner.api.model.AvailableJobsPreviewResponse
import cz.cleansia.partner.api.model.DashboardStatsDto
import cz.cleansia.partner.api.model.OrderListItem
import cz.cleansia.partner.core.auth.UserProfileStore
import cz.cleansia.partner.core.network.ApiErrorTranslator
import cz.cleansia.partner.core.notifications.db.NotificationDao
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
 * State the redesigned dashboard reads. Screen derives next-job + today's
 * jobs from [upcoming]. Backed by the singleton [DashboardRepository]
 * cache so swiping back to the tab shows data instantly instead of a
 * spinner; only the genuine first load shows a spinner.
 *
 * The two refresh flags exist so the UI can render different affordances
 * depending on *who* triggered the network round-trip:
 *  - [isUserRefreshing]: the user pulled-to-refresh — show the chunky
 *    suds indicator at the top.
 *  - [isBackgroundRefreshing]: init / ON_RESUME / post-mutation silent-
 *    stale refresh — render silently; the data swap is its own feedback.
 * Never both at once. The screen combines the two flags with `stats == null`
 * to decide between the full-page initial spinner and the no-op silent path.
 */
data class DashboardUiState(
    val isUserRefreshing: Boolean = false,
    val isBackgroundRefreshing: Boolean = false,
    val firstName: String? = null,
    val stats: DashboardStatsDto? = null,
    val upcoming: List<OrderListItem> = emptyList(),
    /** Top unclaimed jobs + total potential earnings. Null until first load. */
    val availableJobsPreview: AvailableJobsPreviewResponse? = null,
)

/**
 * Distinguishes who triggered a [DashboardViewModel] load. Routes through
 * the two-flag UI state above so the screen renders the right affordance.
 */
private enum class RefreshSource { INIT, RESUME, USER_PULL }

@HiltViewModel
class DashboardViewModel @Inject constructor(
    private val dashboardRepository: DashboardRepository,
    private val userProfileStore: UserProfileStore,
    private val snackbar: SnackbarController,
    private val errorTranslator: ApiErrorTranslator,
    notificationDao: NotificationDao,
) : ViewModel() {

    /** Unread push count driving the bell badge. Cleared when the feed opens. */
    val unreadNotifications: StateFlow<Int> = notificationDao.observeUnreadCount()
        .stateIn(
            scope = viewModelScope,
            started = SharingStarted.WhileSubscribed(5_000),
            initialValue = 0,
        )

    // firstName comes from the cached user profile, not the dashboard
    // network call — so the greeting renders immediately, independent
    // of the stats refresh.
    private val _firstName = MutableStateFlow<String?>(null)

    // Local routing flag so we can tell user-pull refreshes apart from
    // background ones. The repository tracks a single `refreshing` bit;
    // here we lift that into the two-flag UI state. Set BEFORE we hand
    // off to the repository (so the flag is live the moment the snapshot
    // flips to refreshing=true), cleared in the finally of [load].
    private val _userPullInFlight = MutableStateFlow(false)

    // UI state is a projection of the repository's cached snapshot
    // combined with the (locally cached) first name and the user-pull
    // marker. Because the snapshot is singleton-scoped, it survives tab
    // swipes and VM recreation — the screen sees cached data immediately
    // and only a spinner on the genuine first load (stats == null).
    val uiState: StateFlow<DashboardUiState> =
        combine(
            dashboardRepository.snapshot,
            _firstName,
            _userPullInFlight,
        ) { snap, firstName, userPulling ->
            val refreshing = snap.refreshing
            DashboardUiState(
                // Split projection: route the in-flight refresh to exactly
                // one of the two flags based on who started it. Never both.
                // PullToRefreshBox.isRefreshing binds to isUserRefreshing
                // ONLY, so background refreshes don't summon the chunky
                // suds indicator — the silent-stale contract.
                isUserRefreshing = refreshing && userPulling,
                isBackgroundRefreshing = refreshing && !userPulling,
                firstName = firstName,
                stats = snap.stats,
                upcoming = snap.upcoming,
                availableJobsPreview = snap.availableJobsPreview,
            )
        }.stateIn(
            scope = viewModelScope,
            started = SharingStarted.WhileSubscribed(5_000),
            initialValue = DashboardUiState(),
        )

    init {
        viewModelScope.launch { _firstName.value = userProfileStore.current()?.firstName }
        // INIT path: silent-stale. Loads on genuine cold start, no-ops
        // when the cache is already warm (VM recreated while the singleton
        // repo still holds fresh data within the staleness window).
        load(RefreshSource.INIT)
    }

    /** Pull-to-refresh — always hits the network, surfaces as [isUserRefreshing]. */
    fun refresh() = load(RefreshSource.USER_PULL)

    /**
     * Lifecycle ON_RESUME hook. Routes through the staleness-gated path
     * so returning to the tab against a warm cache is a no-op (no spinner,
     * no redundant network call); only a stale cache triggers a silent
     * background refresh.
     */
    fun onResume() = load(RefreshSource.RESUME)

    private fun load(source: RefreshSource) {
        viewModelScope.launch {
            val isUserPull = source == RefreshSource.USER_PULL
            // Set BEFORE the repo flips refreshing=true so the combine()
            // projection sees the marker on the very first emission.
            if (isUserPull) _userPullInFlight.value = true
            try {
                val employeeId = userProfileStore.current()?.employeeId
                val error = dashboardRepository.refresh(employeeId, force = isUserPull)
                if (error != null) {
                    snackbar.showError(errorTranslator.translate(error))
                }
            } finally {
                // Clear AFTER the snapshot's refreshing=false emission has
                // propagated. Since we own this flag the order is fine —
                // the combine() reads both flows and the next emission of
                // either flips us back to a clean (false, false) state.
                if (isUserPull) _userPullInFlight.value = false
            }
        }
    }
}
