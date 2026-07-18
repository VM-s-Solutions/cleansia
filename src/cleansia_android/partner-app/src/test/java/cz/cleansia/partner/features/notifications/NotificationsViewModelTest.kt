package cz.cleansia.partner.features.notifications

import android.content.Context
import app.cash.turbine.test
import cz.cleansia.core.network.ApiError
import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.partner.R
import cz.cleansia.partner.core.network.ApiErrorTranslator
import cz.cleansia.partner.core.notifications.NotificationFeedRepository
import cz.cleansia.partner.core.notifications.PagedNotificationsDto
import cz.cleansia.partner.core.notifications.UserNotificationDto
import cz.cleansia.partner.navigation.NavRoute
import cz.cleansia.partner.testing.MainDispatcherRule
import io.mockk.coEvery
import io.mockk.coVerify
import io.mockk.every
import io.mockk.mockk
import io.mockk.verify
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Rule
import org.junit.Test

@OptIn(ExperimentalCoroutinesApi::class)
class NotificationsViewModelTest {

    @get:Rule
    val mainRule = MainDispatcherRule()

    private lateinit var repository: NotificationFeedRepository
    private lateinit var snackbar: SnackbarController
    private lateinit var errorTranslator: ApiErrorTranslator
    private lateinit var appContext: Context

    private val translatedError = "Something went wrong."

    private val unknownNewestRow = UserNotificationDto(
        id = "n-unknown",
        eventKey = "order.some_future_event",
        args = emptyMap(),
        createdOn = "2026-07-18T09:00:00+00:00",
        readOn = null,
    )
    private val newJobsRow = UserNotificationDto(
        id = "n-newjobs",
        eventKey = "order.new_available",
        args = mapOf("count" to "3"),
        createdOn = "2026-07-17T10:00:00+00:00",
        readOn = null,
    )
    private val confirmedRow = UserNotificationDto(
        id = "n-confirmed",
        eventKey = "order.confirmed",
        args = mapOf("orderId" to "ord-1", "orderNumber" to "A-1042"),
        createdOn = "2026-07-16T08:00:00+00:00",
        readOn = null,
    )
    private val readConfirmedRow = UserNotificationDto(
        id = "n-read",
        eventKey = "order.confirmed",
        args = mapOf("orderId" to "ord-2", "orderNumber" to "A-2000"),
        createdOn = "2026-07-15T08:00:00+00:00",
        readOn = "2026-07-15T09:00:00+00:00",
    )

    @Before
    fun setUp() {
        repository = mockk(relaxUnitFun = true)
        snackbar = mockk(relaxed = true)
        errorTranslator = mockk()
        appContext = mockk(relaxed = true)
        every { appContext.getString(R.string.notification_new_jobs_title) } returns "New jobs available"
        every { appContext.getString(R.string.notification_new_jobs_body, 3) } returns "3 new jobs available near you."
        every { appContext.getString(R.string.notification_order_confirmed_title) } returns "Job confirmed"
        every { appContext.getString(R.string.notification_order_confirmed_body, "A-1042") } returns "Job #A-1042 is confirmed."
        every { appContext.getString(R.string.notification_order_confirmed_body, "A-2000") } returns "Job #A-2000 is confirmed."
        every { errorTranslator.translate(any()) } returns translatedError
        coEvery { repository.markRead(any()) } returns ApiResult.Success(Unit)
        coEvery { repository.markAllRead(any()) } returns ApiResult.Success(Unit)
        coEvery { repository.refreshUnreadCount() } returns ApiResult.Success(0)
    }

    private fun viewModel() = NotificationsViewModel(repository, snackbar, errorTranslator, appContext)

    private fun page(vararg rows: UserNotificationDto, total: Int = rows.size) =
        PagedNotificationsDto(pageNumber = 1, pageSize = 20, total = total, data = rows.toList())

    @Test
    fun `open transitions Loading to Loaded and hides unknown-key rows`() = runTest {
        coEvery { repository.getPage(1) } returns ApiResult.Success(page(unknownNewestRow, newJobsRow))

        val vm = viewModel()
        assertEquals(NotificationsUiState.Loading, vm.state.value)

        vm.open()
        advanceUntilIdle()

        val loaded = vm.state.value as NotificationsUiState.Loaded
        assertEquals(listOf("n-newjobs"), loaded.items.map { it.id })
        assertEquals("New jobs available", loaded.items.single().title)
        assertEquals("3 new jobs available near you.", loaded.items.single().body)
        assertTrue(loaded.items.single().unread)
        assertFalse(loaded.canLoadMore)
    }

    @Test
    fun `open fires the watermarked mark-all with the newest FETCHED createdOn and refreshes the badge`() = runTest {
        // Newest fetched row carries an unknown key — the watermark must still be
        // ITS createdOn (server truth), never now() and never the newest known row.
        coEvery { repository.getPage(1) } returns ApiResult.Success(page(unknownNewestRow, newJobsRow))

        val vm = viewModel()
        vm.open()
        advanceUntilIdle()

        coVerify(exactly = 1) { repository.markAllRead("2026-07-18T09:00:00+00:00") }
        coVerify(exactly = 1) { repository.refreshUnreadCount() }
    }

    @Test
    fun `open with zero rows shows empty Loaded and skips mark-all`() = runTest {
        coEvery { repository.getPage(1) } returns ApiResult.Success(page())

        val vm = viewModel()
        vm.open()
        advanceUntilIdle()

        val loaded = vm.state.value as NotificationsUiState.Loaded
        assertTrue(loaded.items.isEmpty())
        coVerify(exactly = 0) { repository.markAllRead(any()) }
    }

    @Test
    fun `open http error transitions to Error and surfaces single snackbar`() = runTest {
        coEvery { repository.getPage(1) } returns ApiResult.Error(ApiError.Server(500, "boom"))

        val vm = viewModel()
        vm.open()
        advanceUntilIdle()

        assertEquals(NotificationsUiState.Error, vm.state.value)
        verify(exactly = 1) { snackbar.showError(translatedError) }
        coVerify(exactly = 0) { repository.markAllRead(any()) }
    }

    @Test
    fun `open network error transitions to Error and surfaces snackbar`() = runTest {
        coEvery { repository.getPage(1) } returns ApiResult.Error(ApiError.Network("offline"))

        val vm = viewModel()
        vm.open()
        advanceUntilIdle()

        assertEquals(NotificationsUiState.Error, vm.state.value)
        verify(exactly = 1) { snackbar.showError(translatedError) }
    }

    @Test
    fun `unread row tap optimistically clears the dot, decrements the badge, fires mark-read and emits the route`() = runTest {
        coEvery { repository.getPage(1) } returns ApiResult.Success(page(confirmedRow))

        val vm = viewModel()
        vm.open()
        advanceUntilIdle()
        val item = (vm.state.value as NotificationsUiState.Loaded).items.single()

        vm.openRoute.test {
            vm.onRowClick(item)
            assertEquals(NavRoute.OrderDetail("ord-1"), awaitItem())
        }
        advanceUntilIdle()

        val loaded = vm.state.value as NotificationsUiState.Loaded
        assertFalse(loaded.items.single().unread)
        verify(exactly = 1) { repository.decrementUnread() }
        coVerify(exactly = 1) { repository.markRead("n-confirmed") }
    }

    @Test
    fun `new-jobs digest tap navigates to Main - deep-link parity preserved`() = runTest {
        coEvery { repository.getPage(1) } returns ApiResult.Success(page(newJobsRow))

        val vm = viewModel()
        vm.open()
        advanceUntilIdle()
        val item = (vm.state.value as NotificationsUiState.Loaded).items.single()

        vm.openRoute.test {
            vm.onRowClick(item)
            assertEquals(NavRoute.Main, awaitItem())
        }
        advanceUntilIdle()

        coVerify(exactly = 1) { repository.markRead("n-newjobs") }
        verify(exactly = 1) { repository.decrementUnread() }
    }

    @Test
    fun `read row tap navigates without a second mark-read or badge decrement`() = runTest {
        coEvery { repository.getPage(1) } returns ApiResult.Success(page(readConfirmedRow))

        val vm = viewModel()
        vm.open()
        advanceUntilIdle()
        val item = (vm.state.value as NotificationsUiState.Loaded).items.single()

        vm.openRoute.test {
            vm.onRowClick(item)
            assertEquals(NavRoute.OrderDetail("ord-2"), awaitItem())
        }
        advanceUntilIdle()

        verify(exactly = 0) { repository.decrementUnread() }
        coVerify(exactly = 0) { repository.markRead(any()) }
    }

    @Test
    fun `row without a destination just marks read - no route emitted`() = runTest {
        val orphanRow = confirmedRow.copy(id = "n-orphan", args = mapOf("orderNumber" to "A-1042"))
        coEvery { repository.getPage(1) } returns ApiResult.Success(page(orphanRow))

        val vm = viewModel()
        vm.open()
        advanceUntilIdle()
        val item = (vm.state.value as NotificationsUiState.Loaded).items.single()

        vm.openRoute.test {
            vm.onRowClick(item)
            advanceUntilIdle()
            expectNoEvents()
        }

        coVerify(exactly = 1) { repository.markRead("n-orphan") }
        verify(exactly = 1) { repository.decrementUnread() }
    }

    @Test
    fun `loadMore appends the next page and never re-fires mark-all`() = runTest {
        coEvery { repository.getPage(1) } returns ApiResult.Success(
            page(unknownNewestRow, newJobsRow, total = 3),
        )
        coEvery { repository.getPage(2) } returns ApiResult.Success(
            PagedNotificationsDto(pageNumber = 2, pageSize = 20, total = 3, data = listOf(confirmedRow)),
        )

        val vm = viewModel()
        vm.open()
        advanceUntilIdle()
        assertTrue((vm.state.value as NotificationsUiState.Loaded).canLoadMore)

        vm.loadMore()
        advanceUntilIdle()

        val loaded = vm.state.value as NotificationsUiState.Loaded
        assertEquals(listOf("n-newjobs", "n-confirmed"), loaded.items.map { it.id })
        assertFalse(loaded.canLoadMore)
        assertFalse(loaded.loadingMore)
        coVerify(exactly = 1) { repository.markAllRead(any()) }
    }

    @Test
    fun `loadMore http error keeps the list and surfaces snackbar`() = runTest {
        coEvery { repository.getPage(1) } returns ApiResult.Success(
            page(newJobsRow, total = 2),
        )
        coEvery { repository.getPage(2) } returns ApiResult.Error(ApiError.Server(500, "boom"))

        val vm = viewModel()
        vm.open()
        advanceUntilIdle()

        vm.loadMore()
        advanceUntilIdle()

        val loaded = vm.state.value as NotificationsUiState.Loaded
        assertEquals(listOf("n-newjobs"), loaded.items.map { it.id })
        assertFalse(loaded.loadingMore)
        verify(exactly = 1) { snackbar.showError(translatedError) }
    }
}
