package cz.cleansia.partner.features.dashboard

import app.cash.turbine.test
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.partner.api.model.DashboardStatsDto
import cz.cleansia.partner.core.auth.UserProfileData
import cz.cleansia.partner.core.auth.UserProfileStore
import cz.cleansia.core.network.ApiError
import cz.cleansia.core.network.ApiResult
import cz.cleansia.partner.core.network.ApiErrorTranslator
import cz.cleansia.partner.core.notifications.NotificationFeedRepository
import cz.cleansia.partner.data.dashboard.DashboardRepository
import cz.cleansia.partner.data.dashboard.DashboardSnapshot
import cz.cleansia.partner.features.dashboard.DashboardUiState
import cz.cleansia.partner.features.dashboard.DashboardViewModel
import cz.cleansia.partner.testing.MainDispatcherRule
import io.mockk.coEvery
import io.mockk.every
import io.mockk.mockk
import io.mockk.verify
import kotlinx.coroutines.CompletableDeferred
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Rule
import org.junit.Test

@OptIn(ExperimentalCoroutinesApi::class)
class DashboardViewModelTest {

    @get:Rule
    val mainRule = MainDispatcherRule()

    private lateinit var dashboardRepository: DashboardRepository
    private lateinit var userProfileStore: UserProfileStore
    private lateinit var snackbar: SnackbarController
    private lateinit var errorTranslator: ApiErrorTranslator
    private lateinit var notificationFeedRepository: NotificationFeedRepository

    private val snapshotFlow = MutableStateFlow(DashboardSnapshot())
    private val stats = mockk<DashboardStatsDto>()

    private val profile = UserProfileData(
        userId = "user-1",
        email = "cleaner@cleansia.cz",
        employeeId = "emp-1",
        isEmailConfirmed = true,
        hasAdminAccess = false,
        firstName = "Jana",
        lastName = "Nováková",
        role = "Employee",
    )

    @Before
    fun setUp() {
        dashboardRepository = mockk(relaxed = true)
        userProfileStore = mockk()
        snackbar = mockk(relaxed = true)
        errorTranslator = mockk()
        notificationFeedRepository = mockk(relaxUnitFun = true)
        every { dashboardRepository.snapshot } returns snapshotFlow
        every { notificationFeedRepository.unreadCount } returns MutableStateFlow(0)
        coEvery { notificationFeedRepository.refreshUnreadCount() } returns ApiResult.Success(0)
        every { errorTranslator.translate(any()) } returns "translated error"
        coEvery { userProfileStore.current() } returns profile
        coEvery { dashboardRepository.refresh(any(), any()) } returns null
    }

    private fun viewModel() =
        DashboardViewModel(dashboardRepository, userProfileStore, snackbar, errorTranslator, notificationFeedRepository)

    @Test
    fun `refreshing with no stats yields Loading then Loaded once data arrives`() = runTest {
        val vm = viewModel()
        vm.uiState.test {
            // initial projection
            awaitItem()

            snapshotFlow.value = DashboardSnapshot(refreshing = true)
            advanceUntilIdle()
            assertTrue(awaitItem() is DashboardUiState.Loading)

            snapshotFlow.value = DashboardSnapshot(stats = stats, refreshing = false, loaded = true)
            advanceUntilIdle()
            val loaded = awaitItem()
            assertTrue(loaded is DashboardUiState.Loaded)
            assertEquals(stats, (loaded as DashboardUiState.Loaded).stats)

            cancelAndIgnoreRemainingEvents()
        }
    }

    @Test
    fun `firstName projects from cached profile independently of stats`() = runTest {
        val vm = viewModel()
        advanceUntilIdle()
        assertEquals("Jana", vm.firstName.value)
    }

    @Test
    fun `user pull surfaces isUserRefreshing on the state`() = runTest {
        // isUserRefreshing is transient — true only WHILE the user-pull refresh is in
        // flight (the load() finally clears the flag once refresh() returns). Hold the
        // repository call suspended so we can observe the in-flight state, then release.
        val inFlight = CompletableDeferred<Unit>()
        coEvery { dashboardRepository.refresh(any(), any()) } coAnswers {
            inFlight.await()
            null
        }

        val vm = viewModel()
        // Collect uiState so the WhileSubscribed sharing is active — otherwise the
        // combine upstream (which folds _userPullInFlight into isUserRefreshing) never
        // runs. The real UI always has a collector; the sibling tests use Turbine too.
        vm.uiState.test {
            awaitItem() // initial projection

            vm.refresh()
            snapshotFlow.value = DashboardSnapshot(stats = stats, refreshing = true, loaded = true)
            advanceUntilIdle()

            // The pull is still in flight (refresh() is suspended), so the flag is set.
            val state = expectMostRecentItem()
            assertTrue(state is DashboardUiState.Loaded)
            assertTrue((state as DashboardUiState.Loaded).isUserRefreshing)

            inFlight.complete(Unit)
            cancelAndIgnoreRemainingEvents()
        }
    }

    @Test
    fun `refresh error surfaces snackbar`() = runTest {
        coEvery { dashboardRepository.refresh(any(), any()) } returns ApiError.Network("down")

        val vm = viewModel()
        vm.refresh()
        advanceUntilIdle()

        verify { snackbar.showError("translated error") }
    }
}
