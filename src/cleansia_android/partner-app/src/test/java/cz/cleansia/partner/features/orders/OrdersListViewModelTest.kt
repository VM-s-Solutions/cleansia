package cz.cleansia.partner.features.orders

import cz.cleansia.core.network.ApiError
import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.partner.api.model.Code
import cz.cleansia.partner.api.model.AssignedEmployeeDto
import cz.cleansia.partner.api.model.GetOrderPhotosOrderPhotoDto
import cz.cleansia.partner.api.model.GetOrderPhotosResponse
import cz.cleansia.partner.api.model.OrderItem
import cz.cleansia.partner.api.model.PhotoType
import cz.cleansia.partner.data.orders.BoardRows
import cz.cleansia.partner.api.model.OrderListItem
import cz.cleansia.partner.api.model.OrderStatus
import cz.cleansia.partner.api.model.PagedDataOfOrderListItem
import cz.cleansia.partner.core.auth.EmployeeIdResolver
import cz.cleansia.partner.core.network.ApiErrorTranslator
import cz.cleansia.partner.data.orders.OrdersMutation
import cz.cleansia.partner.data.orders.OrdersPane
import cz.cleansia.partner.data.orders.OrdersRepository
import cz.cleansia.partner.data.orders.PendingOffer
import cz.cleansia.partner.data.payroll.CashHeld
import cz.cleansia.partner.data.payroll.PeriodPayRepository
import cz.cleansia.partner.testing.MainDispatcherRule
import io.mockk.coEvery
import io.mockk.coVerify
import io.mockk.every
import io.mockk.mockk
import io.mockk.verify
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.flow.MutableSharedFlow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Rule
import org.junit.Test
import java.time.Instant
import java.time.LocalDate
import java.time.ZoneId
import java.util.Locale

/**
 * The board's fetch set, per-pane rows, paging, the reserved join, the take's board-side handling and the
 * removal of the in-progress banner (spec §9.5, §10, D3, D9, D11-D13, D17). Every `getPaged` call is recorded
 * and answered by which pane its arguments describe, so a test reads as the requests the board sent.
 */
@OptIn(ExperimentalCoroutinesApi::class)
class OrdersListViewModelTest {

    @get:Rule
    val mainRule = MainDispatcherRule()

    private lateinit var ordersRepository: OrdersRepository
    private lateinit var employeeIdResolver: EmployeeIdResolver
    private lateinit var errorTranslator: ApiErrorTranslator
    private lateinit var snackbar: SnackbarController
    private lateinit var periodPay: PeriodPayRepository
    private val offers = MutableStateFlow<List<PendingOffer>>(emptyList())
    private val availableTabRequests = MutableSharedFlow<Unit>()

    private data class Call(
        val statuses: List<OrderStatus>?,
        val employeeId: String?,
        val from: String?,
        val to: String?,
        val sortField: String,
        val ascending: Boolean,
        val offset: Int,
        val limit: Int,
        val pane: OrdersPane?,
        val hasSpots: Boolean?,
        val exclude: String?,
        val orderId: String?,
    ) {
        val kind: String
            get() = when {
                orderId != null && employeeId != null -> "taken"
                orderId != null -> "reserved"
                hasSpots == true -> "available"
                statuses == listOf(OrderStatus._5) -> "history"
                else -> "active"
            }
    }

    private val calls = mutableListOf<Call>()
    private var respond: (Call) -> ApiResult<PagedDataOfOrderListItem> = { page(emptyList(), 0) }

    private fun page(rows: List<OrderListItem>, total: Int) = ApiResult.Success(PagedDataOfOrderListItem(total = total, data = rows))

    private fun order(id: String, status: Int = 0, start: String = "2030-01-02T09:00:00Z") =
        OrderListItem(id = id, orderStatus = Code(value = status), cleaningDateTime = start, estimatedTime = 120, estimatedCleanerPay = 500.0)

    @Before
    fun setUp() {
        ordersRepository = mockk(relaxUnitFun = true)
        employeeIdResolver = mockk()
        errorTranslator = mockk()
        snackbar = mockk(relaxed = true)
        periodPay = mockk()
        every { errorTranslator.translate(any()) } returns "translated error"
        coEvery { employeeIdResolver.resolve() } returns "employee-1"
        every { ordersRepository.isPaneStale(any()) } returns false
        every { ordersRepository.pendingOffers } returns offers
        every { ordersRepository.availableTabRequests } returns availableTabRequests
        every { ordersRepository.arePendingOffersStale() } returns false
        coEvery { ordersRepository.refreshPendingOffers() } returns ApiResult.Success(emptyList())
        coEvery { periodPay.getCashHeld() } returns ApiResult.Success(emptyList())
        coEvery { ordersRepository.getById(any()) } returns ApiResult.Error(ApiError.Network("down"))
        coEvery { ordersRepository.getPhotos(any()) } returns ApiResult.Error(ApiError.Network("down"))
        coEvery {
            ordersRepository.getPaged(any(), any(), any(), any(), any(), any(), any(), any(), any(), any(), any(), any(), any())
        } answers {
            val call = Call(
                statuses = arg(0), employeeId = arg(2), from = arg(3), to = arg(4), sortField = arg(5), ascending = arg(6),
                offset = arg(7), limit = arg(8), pane = arg(9), hasSpots = arg(10), exclude = arg(11), orderId = arg(12),
            )
            calls += call
            respond(call)
        }
    }

    private fun viewModel() = OrdersListViewModel(ordersRepository, employeeIdResolver, errorTranslator, snackbar, periodPay)

    private fun calls(kind: String) = calls.filter { it.kind == kind }

    @Test
    fun `a job detail's ask for the open jobs shows the Available tab`() = runTest {
        val vm = viewModel()
        advanceUntilIdle()
        vm.selectTab(OrdersTab.MyActive)

        availableTabRequests.emit(Unit)
        advanceUntilIdle()

        assertEquals(OrdersTab.Available, vm.uiState.value.tab)
    }

    // ── entry ──────────────────────────────────────────────────────────

    @Test
    fun `entry loads Available page 1, Active and cash held, scoped to the cleaner`() = runTest {
        viewModel()
        advanceUntilIdle()

        val available = calls("available").single()
        assertEquals(listOf(OrderStatus._0, OrderStatus._2, OrderStatus._3, OrderStatus._4), available.statuses)
        assertEquals("employee-1", available.exclude)
        assertNull(available.employeeId)
        assertEquals("cleaningDateTime", available.sortField)
        assertTrue(available.ascending)
        assertEquals(0, available.offset)
        assertEquals(100, available.limit)
        val active = calls("active").single()
        assertEquals(listOf(OrderStatus._2, OrderStatus._3, OrderStatus._4), active.statuses)
        assertEquals("employee-1", active.employeeId)
        assertNull(active.exclude)
        assertTrue(calls("history").isEmpty())
        coVerify(exactly = 1) { periodPay.getCashHeld() }
    }

    @Test
    fun `a resume on loaded fresh panes does not refetch`() = runTest {
        val vm = viewModel()
        advanceUntilIdle()
        calls.clear()

        vm.onResume()
        advanceUntilIdle()

        assertTrue(calls.toString(), calls.isEmpty())
    }

    @Test
    fun `switching tabs keeps each pane's rows`() = runTest {
        respond = {
            when (it.kind) {
                "available" -> page(listOf(order("a")), 1)
                "history" -> page(listOf(order("h", status = 5)), 1)
                else -> page(emptyList(), 0)
            }
        }
        val vm = viewModel()
        advanceUntilIdle()

        vm.selectTab(OrdersTab.MyCompleted)
        vm.selectTab(OrdersTab.Available)
        advanceUntilIdle()

        assertEquals(listOf("a"), vm.uiState.value.pane(OrdersTab.Available).rows.map { it.id })
        assertEquals(1, calls("history").size)
    }

    // ── paging ─────────────────────────────────────────────────────────

    @Test
    fun `Active pages to completion and stops at the server total`() = runTest {
        val all = (1..150).map { order("a$it", status = 2) }
        respond = { if (it.kind == "active") page(all.drop(it.offset).take(it.limit), 150) else page(emptyList(), 0) }
        val vm = viewModel()
        advanceUntilIdle()

        assertEquals(listOf(0, 100), calls("active").map { it.offset })
        assertEquals(150, vm.uiState.value.pane(OrdersTab.MyActive).rows.size)
        assertTrue(vm.uiState.value.pane(OrdersTab.MyActive).complete)
    }

    @Test
    fun `paging to completion stops on an empty page`() = runTest {
        respond = { if (it.kind == "active" && it.offset == 0) page(listOf(order("a", status = 2)), 5) else page(emptyList(), 5) }
        val vm = viewModel()
        advanceUntilIdle()

        assertEquals(2, calls("active").size)
        assertEquals(1, vm.uiState.value.pane(OrdersTab.MyActive).rows.size)
    }

    /**
     * A History of 101 rows where a job completes between pages: everything shifts down one, so page 2 repeats a
     * row and page 3 repeats the last one. A page that adds nothing ends the loop; it used to resend forever.
     */
    @Test
    fun `paging to completion stops when a page adds no new row`() = runTest {
        val first = (0 until 100).map { order("h$it", status = 5) }
        respond = {
            when {
                it.kind != "history" -> page(emptyList(), 0)
                calls("history").size > 10 -> page(emptyList(), 102) // ends the endless loop so the test can fail
                it.offset == 0 -> page(first, 102)
                it.offset == 100 -> page(listOf(order("h99", status = 5), order("h100", status = 5)), 102)
                else -> page(listOf(order("h100", status = 5)), 102)
            }
        }
        val vm = viewModel()
        advanceUntilIdle()

        vm.selectTab(OrdersTab.MyCompleted)
        advanceUntilIdle()

        assertEquals(listOf(0, 100, 101), calls("history").map { it.offset })
        val pane = vm.uiState.value.pane(OrdersTab.MyCompleted)
        assertEquals(101, pane.rows.size)
        assertFalse(pane.isLoading)
    }

    @Test
    fun `load more appends the next page and drops ids already held`() = runTest {
        respond = {
            when {
                it.kind == "available" && it.offset == 0 -> page(listOf(order("a"), order("b")), 4)
                it.kind == "available" -> page(listOf(order("b"), order("c")), 4)
                else -> page(emptyList(), 0)
            }
        }
        val vm = viewModel()
        advanceUntilIdle()

        vm.loadMore()
        advanceUntilIdle()

        assertEquals(2, calls("available").last().offset)
        val pane = vm.uiState.value.pane(OrdersTab.Available)
        assertEquals(listOf("a", "b", "c"), pane.rows.map { it.id })
        assertEquals(2, pane.appendedFrom)
    }

    @Test
    fun `a failed load more keeps the loaded rows and says so in the footer`() = runTest {
        respond = {
            when {
                it.kind == "available" && it.offset == 0 -> page(listOf(order("a")), 3)
                it.kind == "available" -> ApiResult.Error(ApiError.Network("down"))
                else -> page(emptyList(), 0)
            }
        }
        val vm = viewModel()
        advanceUntilIdle()

        vm.loadMore()
        advanceUntilIdle()

        val pane = vm.uiState.value.pane(OrdersTab.Available)
        assertEquals(listOf("a"), pane.rows.map { it.id })
        assertTrue(pane.pagingFailed)
        assertFalse(pane.isPaging)
    }

    // ── errors ─────────────────────────────────────────────────────────

    @Test
    fun `a failed first load is the error state and raises no snackbar`() = runTest {
        respond = { ApiResult.Error(ApiError.Network("down")) }
        val vm = viewModel()
        advanceUntilIdle()

        val pane = vm.uiState.value.pane(OrdersTab.Available)
        assertTrue(pane.failed)
        assertFalse(pane.hasLoaded)
        assertTrue(pane.showsError())
        verify(exactly = 0) { snackbar.showError(any<String>()) }
    }

    @Test
    fun `a failed refresh over a pane that loaded empty is the error state, not the empty one`() = runTest {
        var fail = false
        respond = { if (fail) ApiResult.Error(ApiError.Network("down")) else page(emptyList(), 0) }
        val vm = viewModel()
        advanceUntilIdle()
        assertFalse(vm.uiState.value.pane(OrdersTab.Available).showsError())

        fail = true
        vm.onRefresh()
        vm.selectTab(OrdersTab.MyActive)
        vm.onRefresh()
        advanceUntilIdle()

        listOf(OrdersTab.Available, OrdersTab.MyActive).forEach { tab ->
            val pane = vm.uiState.value.pane(tab)
            assertTrue("$tab", pane.hasLoaded)
            assertTrue("$tab: no rows held and the refresh failed — not \"no jobs\"", pane.showsError())
        }
        assertFalse(
            "a reserved card still on show is rows held: the stale strip, not the error",
            vm.uiState.value.pane(OrdersTab.Available).showsError(heldElsewhere = true),
        )
    }

    @Test
    fun `a failed refresh over held rows keeps them and marks the pane stale`() = runTest {
        var fail = false
        respond = { if (fail) ApiResult.Error(ApiError.Network("down")) else page(listOf(order("a")), 1) }
        val vm = viewModel()
        advanceUntilIdle()

        fail = true
        vm.onRefresh()
        advanceUntilIdle()

        val pane = vm.uiState.value.pane(OrdersTab.Available)
        assertEquals(listOf("a"), pane.rows.map { it.id })
        assertTrue(pane.failed)
        assertFalse("rows held: the stale strip, not the error state", pane.showsError())
        assertNotNull(pane.loadedAt)
        assertFalse(vm.uiState.value.isUserRefreshing)
        verify(exactly = 0) { snackbar.showError(any<String>()) }
    }

    /** Never unscoped: without the cleaner's id no list request goes out at all. */
    @Test
    fun `an unreadable own id sends no list request and shows the error state on every pane`() = runTest {
        coEvery { employeeIdResolver.resolve() } returns " "
        val vm = viewModel()
        advanceUntilIdle()
        vm.selectTab(OrdersTab.MyCompleted)
        advanceUntilIdle()

        assertTrue(calls.toString(), calls.isEmpty())
        OrdersTab.entries.forEach { tab -> assertTrue("$tab", vm.uiState.value.pane(tab).failed) }
        assertTrue(calls.none { it.employeeId == null && it.exclude == null })
    }

    // ── the banner is gone ─────────────────────────────────────────────

    @Test
    fun `someone else's in-progress job on Available never becomes Active content`() = runTest {
        respond = { if (it.kind == "available") page(listOf(order("theirs", status = 4)), 1) else page(emptyList(), 0) }
        val vm = viewModel()
        advanceUntilIdle()

        val active = vm.uiState.value.pane(OrdersTab.MyActive).rows
        assertTrue(active.isEmpty())
        assertNull(OrdersBoardRules.heroPick(active))
    }

    @Test
    fun `the cleaner's own in-progress job stays on Active and is the hero`() = runTest {
        val mine = order("mine", status = 4)
        respond = { if (it.kind == "active") page(listOf(order("c", status = 2), mine), 2) else page(emptyList(), 0) }
        val vm = viewModel()
        advanceUntilIdle()

        assertEquals(mine, OrdersBoardRules.heroPick(vm.uiState.value.pane(OrdersTab.MyActive).rows))
    }

    // ── reserved join ──────────────────────────────────────────────────

    private fun offer(id: String) = PendingOffer(
        id = id, displayOrderNumber = null, cleaningDateTime = "2030-01-02T09:00:00Z", estimatedTime = 120,
        respondByUtc = Instant.now().plusSeconds(3600).toString(), customerAddressApproximate = null,
        rooms = 1, bathrooms = 1, currencyCode = "CZK", estimatedCleanerPay = 999.0,
    )

    @Test
    fun `an offer whose row is on the page needs no extra read`() = runTest {
        respond = { if (it.kind == "available") page(listOf(order("held")), 1) else page(emptyList(), 0) }
        offers.value = listOf(offer("held"))
        val vm = viewModel()
        advanceUntilIdle()

        assertTrue(calls("reserved").isEmpty())
        assertEquals(1, vm.uiState.value.pane(OrdersTab.Available).total)
    }

    @Test
    fun `an off-page offer is read once by id with the Available query, and not found means no card`() = runTest {
        respond = {
            when (it.kind) {
                "available" -> page(listOf(order("x")), 140)
                "reserved" -> if (it.orderId == "far") page(listOf(order("far")), 1) else page(emptyList(), 0)
                else -> page(emptyList(), 0)
            }
        }
        offers.value = listOf(offer("far"), offer("gone"))
        val vm = viewModel()
        advanceUntilIdle()

        val reads = calls("reserved")
        assertEquals(setOf("far", "gone"), reads.map { it.orderId }.toSet())
        reads.forEach { read ->
            assertEquals(1, read.limit)
            assertEquals(true, read.hasSpots)
            assertEquals("employee-1", read.exclude)
            assertEquals(listOf(OrderStatus._0, OrderStatus._2, OrderStatus._3, OrderStatus._4), read.statuses)
        }
        assertEquals(setOf("far"), vm.uiState.value.reservedRows.keys)
        assertEquals("the badge still counts the server total", 140, vm.uiState.value.pane(OrdersTab.Available).total)

        offers.value = offers.value + offer("x")
        advanceUntilIdle()
        assertEquals("each offer is looked up once, and one on the page never", 2, calls("reserved").size)
    }

    @Test
    fun `an off-page reserved row is read again on each Available reload, and one no longer found drops its card`() = runTest {
        var farPay: Double? = 500.0
        respond = {
            when (it.kind) {
                "available" -> page(listOf(order("x")), 140)
                "reserved" -> farPay?.let { pay -> page(listOf(order("far").copy(estimatedCleanerPay = pay)), 1) } ?: page(emptyList(), 0)
                else -> page(emptyList(), 0)
            }
        }
        offers.value = listOf(offer("far"))
        val vm = viewModel()
        advanceUntilIdle()
        assertEquals(500.0, vm.uiState.value.reservedRows["far"]?.estimatedCleanerPay)

        farPay = 650.0
        vm.onRefresh()
        advanceUntilIdle()
        assertEquals(2, calls("reserved").size)
        assertEquals("the held row is read again, not kept for the life of the screen", 650.0, vm.uiState.value.reservedRows["far"]?.estimatedCleanerPay)

        farPay = null
        vm.onRefresh()
        advanceUntilIdle()
        assertNull("not found any more: no card", vm.uiState.value.reservedRows["far"])
    }

    // ── cash held ──────────────────────────────────────────────────────

    @Test
    fun `cash held shows its first hidden row and a failed read shows nothing`() = runTest {
        val hidden = CashHeld("CZK", 3000.40, 3000.0, cashJobsHidden = true)
        coEvery { periodPay.getCashHeld() } returns ApiResult.Success(listOf(CashHeld("EUR", 1.0, 100.0, false), hidden))
        val vm = viewModel()
        advanceUntilIdle()
        assertEquals(hidden, vm.uiState.value.cashHidden)

        coEvery { periodPay.getCashHeld() } returns ApiResult.Error(ApiError.Network("down"))
        vm.onRefresh()
        advanceUntilIdle()
        assertNull(vm.uiState.value.cashHidden)
    }

    // ── the take ───────────────────────────────────────────────────────

    @Test
    fun `Take opens the sheet with the row as its board context and takes nothing itself`() = runTest {
        val row = order("o1")
        respond = { if (it.kind == "available") page(listOf(row), 1) else page(emptyList(), 0) }
        val vm = viewModel()
        advanceUntilIdle()

        vm.takeOrderInline("o1")

        assertEquals(WorkContractRequest.Take("o1", BoardTake(row)), vm.uiState.value.contractRequest)
        coVerify(exactly = 0) { ordersRepository.takeOrder(any(), any()) }
    }

    @Test
    fun `a take keeps the sheet up, drops the row, refetches, refreshes offers and reads the taken row`() = runTest {
        val row = order("o1")
        val mine = order("o1", status = 2).copy(customerAddress = "Vinohradská 46, Praha, 12000")
        var availableReads = 0
        respond = {
            when (it.kind) {
                "available" -> if (++availableReads == 1) page(listOf(order("o0"), row), 2) else page(listOf(order("o0")), 1)
                "taken" -> page(listOf(mine), 1)
                else -> page(emptyList(), 0)
            }
        }
        val vm = viewModel()
        advanceUntilIdle()
        vm.takeOrderInline("o1")
        calls.clear()

        vm.onWorkContractOutcome(WorkContractOutcome.Taken(WorkContractRequest.Take("o1", BoardTake(row))))
        assertEquals(listOf("o0"), vm.uiState.value.pane(OrdersTab.Available).rows.map { it.id })
        advanceUntilIdle()

        val state = vm.uiState.value
        assertNotNull("the sheet stays for its taken state", state.contractRequest)
        assertTrue(state.takeTaken)
        assertEquals(1, state.takenIndex)
        assertEquals(mine, state.takenRow)
        verify(exactly = 1) { ordersRepository.invalidatePanesFor(OrdersMutation.TakeOrder) }
        coVerify(exactly = 1) { ordersRepository.refreshPendingOffers() }
        assertEquals(1, calls("available").size)
        assertEquals(1, calls("active").size)
        val taken = calls("taken").single()
        assertEquals("o1", taken.orderId)
        assertEquals("employee-1", taken.employeeId)
        assertEquals(listOf(OrderStatus._2, OrderStatus._3, OrderStatus._4), taken.statuses)
        assertEquals(1, taken.limit)
        coVerify(exactly = 0) { ordersRepository.getById(any()) }
    }

    /** §6.7: focus goes to the card that took the taken row's place, counted among the cards on show. */
    @Test
    fun `after a take, focus goes to the card that took its place among the cards on show`() = runTest {
        respond = {
            if (it.kind == "available") page(listOf(order("res"), order("o1"), order("o2"), order("o3")), 4) else page(emptyList(), 0)
        }
        offers.value = listOf(offer("res"))
        val vm = viewModel()
        advanceUntilIdle()
        vm.takeOrderInline("o1")

        vm.onWorkContractOutcome(WorkContractOutcome.Taken(WorkContractRequest.Take("o1")))

        assertEquals("o1 was the first job card: res is a reserved card above them", 0, vm.uiState.value.takenIndex)
    }

    @Test
    fun `a refused take keeps the sheet up with the reason and raises no snackbar`() = runTest {
        val vm = viewModel()
        advanceUntilIdle()
        vm.takeOrderInline("o1")
        calls.clear()

        vm.onWorkContractOutcome(
            WorkContractOutcome.Refused(WorkContractRequest.Take("o1"), ApiError.BadRequest("taken", errorKey = "order.no_available_spots")),
        )
        advanceUntilIdle()

        val state = vm.uiState.value
        assertNotNull(state.contractRequest)
        assertEquals("translated error", state.takeRefusal)
        assertFalse(state.takeTaken)
        verify(exactly = 0) { snackbar.showError(any<String>()) }
        verify(exactly = 1) { ordersRepository.invalidatePanesFor(OrdersMutation.TakeOrder) }
        coVerify(exactly = 1) { ordersRepository.refreshPendingOffers() }
        assertEquals(1, calls("available").size)
    }

    @Test
    fun `dismissing the sheet refetches nothing`() = runTest {
        val vm = viewModel()
        advanceUntilIdle()
        vm.takeOrderInline("o1")
        calls.clear()

        vm.dismissContract()
        advanceUntilIdle()

        assertNull(vm.uiState.value.contractRequest)
        assertTrue(calls.isEmpty())
    }

    // ── the hero's slides ──────────────────────────────────────────────

    @Test
    fun `a failed slide raises one snackbar and the Active pane reconciles`() = runTest {
        coEvery { ordersRepository.startOrder("o1") } returns ApiResult.Error(ApiError.Network("down"))
        val vm = viewModel()
        advanceUntilIdle()
        calls.clear()

        vm.startOrderInline("o1")
        advanceUntilIdle()

        verify(exactly = 1) { snackbar.showError("translated error") }
        verify(exactly = 1) { ordersRepository.invalidatePanesFor(OrdersMutation.StartOrder) }
        assertEquals(1, calls("active").size)
        assertNull(vm.uiState.value.inFlightActionOrderId)
    }

    // ── History ────────────────────────────────────────────────────────

    @Test
    fun `History loads its period on open and refetches a new period from scratch`() = runTest {
        respond = { if (it.kind == "history") page(listOf(order("h", status = 5)), 1) else page(emptyList(), 0) }
        val vm = viewModel()
        advanceUntilIdle()

        vm.selectTab(OrdersTab.MyCompleted)
        advanceUntilIdle()
        vm.setCompletedPeriod(CompletedPeriod.LastMonth)
        advanceUntilIdle()

        val history = calls("history")
        assertEquals(2, history.size)
        history.forEach { call ->
            assertEquals("employee-1", call.employeeId)
            assertFalse("History is newest first", call.ascending)
            assertNotNull(call.from)
            assertNotNull(call.to)
        }
        val (from, to) = OrdersListViewModel.periodToDateRange(CompletedPeriod.LastMonth)
        assertEquals(from, history.last().from)
        assertEquals(to, history.last().to)
    }

    @Test
    fun `This week starts on Monday even where the device week starts on Sunday`() {
        val zone = ZoneId.of("Europe/Prague")
        val (from, to) = OrdersListViewModel.periodToDateRange(CompletedPeriod.ThisWeek, LocalDate.of(2026, 11, 12), zone, Locale.US)
        assertEquals(LocalDate.of(2026, 11, 9).atStartOfDay(zone).toInstant().toString(), from)
        assertEquals(LocalDate.of(2026, 11, 16).atStartOfDay(zone).toInstant().toString(), to)
    }

    @Test
    fun `an empty month reads last month once, as its reference`() = runTest {
        val (lastFrom, lastTo) = OrdersListViewModel.periodToDateRange(CompletedPeriod.LastMonth)
        respond = { if (it.kind == "history" && it.from == lastFrom) page(listOf(order("o1", status = 5)), 1) else page(emptyList(), 0) }
        val vm = viewModel()
        advanceUntilIdle()
        vm.selectTab(OrdersTab.MyCompleted)
        advanceUntilIdle()

        val history = calls("history")
        assertEquals(2, history.size)
        assertEquals(lastFrom, history.last().from)
        assertEquals(lastTo, history.last().to)
        assertEquals(listOf("o1"), vm.uiState.value.historyReference?.rows?.map { it.id })
    }

    @Test
    fun `an empty last month is no reference`() = runTest {
        respond = { page(emptyList(), 0) }
        val vm = viewModel()
        advanceUntilIdle()
        vm.selectTab(OrdersTab.MyCompleted)
        advanceUntilIdle()

        assertEquals(2, calls("history").size)
        assertNull(vm.uiState.value.historyReference)
    }

    @Test
    fun `a month with jobs reads no reference`() = runTest {
        respond = { if (it.kind == "history") page(listOf(order("h1", status = 5)), 1) else page(emptyList(), 0) }
        val vm = viewModel()
        advanceUntilIdle()
        vm.selectTab(OrdersTab.MyCompleted)
        advanceUntilIdle()

        assertEquals(1, calls("history").size)
        assertNull(vm.uiState.value.historyReference)
    }

    @Test
    fun `the board publishes the rows it loaded, for the detail to read by id`() = runTest {
        respond = {
            when (it.kind) {
                "available" -> page(listOf(order("v1")), 7)
                "active" -> page(listOf(order("a1", status = 2)), 1)
                else -> page(emptyList(), 0)
            }
        }
        viewModel()
        advanceUntilIdle()

        verify { ordersRepository.publishBoardRows(BoardRows(available = listOf(order("v1")), availableTotal = 7, own = listOf(order("a1", status = 2)))) }
    }

    private fun photo(type: PhotoType, at: String) = GetOrderPhotosOrderPhotoDto(photoType = type, capturedAt = at)

    @Test
    fun `the in-progress hero reads its job and photos, so it can say Finishing`() = runTest {
        respond = { if (it.kind == "active") page(listOf(order("mine", status = 4)), 1) else page(emptyList(), 0) }
        coEvery { ordersRepository.getById("mine") } returns ApiResult.Success(
            OrderItem(id = "mine", assignedEmployees = listOf(AssignedEmployeeDto(employeeId = "employee-1"))),
        )
        coEvery { ordersRepository.getPhotos("mine") } returns ApiResult.Success(
            GetOrderPhotosResponse(
                photos = listOf(
                    photo(PhotoType._1, "2030-01-02T09:05:00Z"),
                    photo(PhotoType._2, "2030-01-02T10:50:00Z"),
                    photo(PhotoType._2, "2030-01-02T10:46:00Z"),
                ),
            ),
        )
        val vm = viewModel()
        advanceUntilIdle()

        assertEquals(
            HeroProgress(orderId = "mine", crew = false, myMarkAt = null, firstAfterPhotoAt = Instant.parse("2030-01-02T10:46:00Z")),
            vm.uiState.value.heroProgress,
        )
    }

    @Test
    fun `on a crew job the hero takes her own mark, never a crew-mate's`() = runTest {
        respond = { if (it.kind == "active") page(listOf(order("crew", status = 4)), 1) else page(emptyList(), 0) }
        val mate = AssignedEmployeeDto(employeeId = "mate", completionConfirmedAt = "2030-01-02T10:41:00Z")
        coEvery { ordersRepository.getById("crew") } returns ApiResult.Success(
            OrderItem(id = "crew", assignedEmployees = listOf(AssignedEmployeeDto(employeeId = "employee-1"), mate)),
        )
        coEvery { ordersRepository.getPhotos("crew") } returns ApiResult.Success(GetOrderPhotosResponse(photos = listOf(photo(PhotoType._2, "2030-01-02T10:38:00Z"))))
        val vm = viewModel()
        advanceUntilIdle()
        assertEquals(true, vm.uiState.value.heroProgress?.crew)
        assertNull(vm.uiState.value.heroProgress?.myMarkAt)

        coEvery { ordersRepository.getById("crew") } returns ApiResult.Success(
            OrderItem(id = "crew", assignedEmployees = listOf(AssignedEmployeeDto(employeeId = "employee-1", completionConfirmedAt = "2030-01-02T10:44:00Z"), mate)),
        )
        vm.selectTab(OrdersTab.MyActive)
        vm.onRefresh()
        advanceUntilIdle()
        assertEquals(Instant.parse("2030-01-02T10:44:00Z"), vm.uiState.value.heroProgress?.myMarkAt)
    }

    @Test
    fun `a hero not yet in progress reads nothing more`() = runTest {
        respond = { if (it.kind == "active") page(listOf(order("next", status = 2)), 1) else page(emptyList(), 0) }
        val vm = viewModel()
        advanceUntilIdle()

        coVerify(exactly = 0) { ordersRepository.getById(any()) }
        coVerify(exactly = 0) { ordersRepository.getPhotos(any()) }
        assertNull(vm.uiState.value.heroProgress)
    }

    @Test
    fun `cash held keeps its first balance for the empty tab's panel, hidden or not`() = runTest {
        val held = CashHeld("CZK", 1240.0, 3000.0, cashJobsHidden = false)
        coEvery { periodPay.getCashHeld() } returns ApiResult.Success(listOf(held))
        val vm = viewModel()
        advanceUntilIdle()

        assertEquals(held, vm.uiState.value.cashHeld)
        assertNull(vm.uiState.value.cashHidden)
    }
}
