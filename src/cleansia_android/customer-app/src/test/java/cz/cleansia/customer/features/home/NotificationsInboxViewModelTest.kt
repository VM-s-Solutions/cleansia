package cz.cleansia.customer.features.home

import android.content.Context
import app.cash.turbine.test
import cz.cleansia.core.network.ApiError
import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.customer.R
import cz.cleansia.customer.core.notifications.NotificationFeedRepository
import cz.cleansia.customer.core.notifications.PagedNotificationsDto
import cz.cleansia.customer.core.notifications.UserNotificationDto
import cz.cleansia.customer.navigation.Routes
import cz.cleansia.customer.testing.MainDispatcherRule
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
class NotificationsInboxViewModelTest {

    @get:Rule
    val mainRule = MainDispatcherRule()

    private lateinit var repository: NotificationFeedRepository
    private lateinit var snackbar: SnackbarController
    private lateinit var appContext: Context

    private val serverMessage = "Server unavailable."

    private val unknownNewestRow = UserNotificationDto(
        id = "n-unknown",
        eventKey = "order.some_future_event",
        args = emptyMap(),
        createdOn = "2026-07-18T09:00:00+00:00",
        readOn = null,
    )
    private val completedRow = UserNotificationDto(
        id = "n-completed",
        eventKey = "order.completed",
        args = mapOf("orderId" to "ord-1", "orderNumber" to "A-1042"),
        createdOn = "2026-07-17T10:00:00+00:00",
        readOn = null,
    )
    private val readDisputeRow = UserNotificationDto(
        id = "n-dispute",
        eventKey = "dispute.reply",
        args = mapOf("disputeId" to "dsp-1"),
        createdOn = "2026-07-16T08:00:00+00:00",
        readOn = "2026-07-16T09:00:00+00:00",
    )

    @Before
    fun setUp() {
        repository = mockk(relaxUnitFun = true)
        snackbar = mockk(relaxed = true)
        appContext = mockk(relaxed = true)
        every { appContext.getString(R.string.notification_order_completed_title) } returns "All done!"
        every {
            appContext.getString(R.string.notification_order_completed_body, "A-1042")
        } returns "Your booking #A-1042 is complete."
        every { appContext.getString(R.string.notification_dispute_reply_title) } returns "Support replied"
        every { appContext.getString(R.string.notification_dispute_reply_body) } returns "New dispute message."
        every { appContext.getString(R.string.error_generic_server) } returns serverMessage
        every { appContext.packageName } returns "cz.cleansia.customer"
        val resources = mockk<android.content.res.Resources>(relaxed = true)
        every { appContext.resources } returns resources
        every { resources.getIdentifier(any(), any(), any()) } returns 0
        coEvery { repository.markRead(any()) } returns ApiResult.Success(Unit)
        coEvery { repository.markAllRead(any()) } returns ApiResult.Success(Unit)
        coEvery { repository.refreshUnreadCount() } returns ApiResult.Success(0)
    }

    private fun viewModel() = NotificationsInboxViewModel(repository, snackbar, appContext)

    private fun page(vararg rows: UserNotificationDto, total: Int = rows.size) =
        PagedNotificationsDto(pageNumber = 1, pageSize = 20, total = total, data = rows.toList())

    @Test
    fun `open transitions Loading to Loaded and hides unknown-key rows`() = runTest {
        coEvery { repository.getPage(1) } returns ApiResult.Success(page(unknownNewestRow, completedRow))

        val vm = viewModel()
        assertEquals(NotificationsInboxUiState.Loading, vm.state.value)

        vm.open()
        advanceUntilIdle()

        val loaded = vm.state.value as NotificationsInboxUiState.Loaded
        assertEquals(listOf("n-completed"), loaded.items.map { it.id })
        assertEquals("All done!", loaded.items.single().title)
        assertEquals("Your booking #A-1042 is complete.", loaded.items.single().body)
        assertTrue(loaded.items.single().unread)
        assertFalse(loaded.canLoadMore)
    }

    @Test
    fun `open fires the watermarked mark-all with the newest FETCHED createdOn and refreshes the badge`() = runTest {
        // Newest fetched row carries an unknown key — the watermark must still be
        // ITS createdOn (server truth), never now() and never the newest known row.
        coEvery { repository.getPage(1) } returns ApiResult.Success(page(unknownNewestRow, completedRow))

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

        val loaded = vm.state.value as NotificationsInboxUiState.Loaded
        assertTrue(loaded.items.isEmpty())
        coVerify(exactly = 0) { repository.markAllRead(any()) }
    }

    @Test
    fun `open http error transitions to Error and surfaces single snackbar`() = runTest {
        coEvery { repository.getPage(1) } returns ApiResult.Error(ApiError.Server(500, serverMessage))

        val vm = viewModel()
        vm.open()
        advanceUntilIdle()

        assertEquals(NotificationsInboxUiState.Error, vm.state.value)
        verify(exactly = 1) { snackbar.showError(serverMessage) }
        coVerify(exactly = 0) { repository.markAllRead(any()) }
    }

    @Test
    fun `open infrastructure error transitions to Error silently`() = runTest {
        coEvery { repository.getPage(1) } returns ApiResult.Error(ApiError.Network("offline"))

        val vm = viewModel()
        vm.open()
        advanceUntilIdle()

        assertEquals(NotificationsInboxUiState.Error, vm.state.value)
        verify(exactly = 0) { snackbar.showError(any<String>()) }
    }

    @Test
    fun `unread row tap optimistically clears the dot, decrements the badge, fires mark-read and emits the route`() = runTest {
        coEvery { repository.getPage(1) } returns ApiResult.Success(page(completedRow))

        val vm = viewModel()
        vm.open()
        advanceUntilIdle()
        val item = (vm.state.value as NotificationsInboxUiState.Loaded).items.single()

        vm.openRoute.test {
            vm.onRowClick(item)
            assertEquals(Routes.OrderDetail("ord-1"), awaitItem())
        }
        advanceUntilIdle()

        val loaded = vm.state.value as NotificationsInboxUiState.Loaded
        assertFalse(loaded.items.single().unread)
        verify(exactly = 1) { repository.decrementUnread() }
        coVerify(exactly = 1) { repository.markRead("n-completed") }
    }

    @Test
    fun `read row tap navigates without a second mark-read or badge decrement`() = runTest {
        coEvery { repository.getPage(1) } returns ApiResult.Success(page(readDisputeRow))

        val vm = viewModel()
        vm.open()
        advanceUntilIdle()
        val item = (vm.state.value as NotificationsInboxUiState.Loaded).items.single()

        vm.openRoute.test {
            vm.onRowClick(item)
            assertEquals(Routes.DisputeDetail("dsp-1"), awaitItem())
        }
        advanceUntilIdle()

        verify(exactly = 0) { repository.decrementUnread() }
        coVerify(exactly = 0) { repository.markRead(any()) }
    }

    @Test
    fun `row without a destination just marks read - no route emitted`() = runTest {
        val orphanRow = completedRow.copy(id = "n-orphan", args = mapOf("orderNumber" to "A-1042"))
        coEvery { repository.getPage(1) } returns ApiResult.Success(page(orphanRow))

        val vm = viewModel()
        vm.open()
        advanceUntilIdle()
        val item = (vm.state.value as NotificationsInboxUiState.Loaded).items.single()

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
            page(unknownNewestRow, completedRow, total = 3),
        )
        coEvery { repository.getPage(2) } returns ApiResult.Success(
            PagedNotificationsDto(pageNumber = 2, pageSize = 20, total = 3, data = listOf(readDisputeRow)),
        )

        val vm = viewModel()
        vm.open()
        advanceUntilIdle()
        assertTrue((vm.state.value as NotificationsInboxUiState.Loaded).canLoadMore)

        vm.loadMore()
        advanceUntilIdle()

        val loaded = vm.state.value as NotificationsInboxUiState.Loaded
        assertEquals(listOf("n-completed", "n-dispute"), loaded.items.map { it.id })
        assertFalse(loaded.canLoadMore)
        assertFalse(loaded.loadingMore)
        coVerify(exactly = 1) { repository.markAllRead(any()) }
    }

    @Test
    fun `loadMore http error keeps the list and surfaces snackbar`() = runTest {
        coEvery { repository.getPage(1) } returns ApiResult.Success(
            page(completedRow, total = 2),
        )
        coEvery { repository.getPage(2) } returns ApiResult.Error(ApiError.Server(500, serverMessage))

        val vm = viewModel()
        vm.open()
        advanceUntilIdle()

        vm.loadMore()
        advanceUntilIdle()

        val loaded = vm.state.value as NotificationsInboxUiState.Loaded
        assertEquals(listOf("n-completed"), loaded.items.map { it.id })
        assertFalse(loaded.loadingMore)
        verify(exactly = 1) { snackbar.showError(serverMessage) }
    }
}
