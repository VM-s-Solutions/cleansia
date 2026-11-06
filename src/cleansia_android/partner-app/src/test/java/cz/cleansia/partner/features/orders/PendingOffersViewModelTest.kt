package cz.cleansia.partner.features.orders

import cz.cleansia.core.network.ApiError
import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.core.ui.state.ActionState
import cz.cleansia.partner.api.model.Code
import cz.cleansia.partner.api.model.OrderAddress
import cz.cleansia.partner.api.model.OrderItem
import cz.cleansia.partner.data.orders.PendingOffer
import cz.cleansia.partner.core.network.ApiErrorTranslator
import cz.cleansia.partner.data.orders.OrdersRepository
import cz.cleansia.partner.testing.MainDispatcherRule
import io.mockk.coEvery
import io.mockk.coVerify
import io.mockk.every
import io.mockk.mockk
import io.mockk.slot
import io.mockk.verify
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.test.advanceTimeBy
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Rule
import org.junit.Test

/**
 * "Confirming IS taking" — there is no confirm endpoint, and a UI that called anything else would be a
 * second acquisition path beside TakeOrder's single ordered chain. The take itself now runs inside
 * the contract sheet, so the confirm opens it and the sheet's verdict comes back as the confirm's
 * own. The refusal cases matter as much as the happy one: a reservation spends no capacity, so a
 * capped cleaner can be reserved a job and then refused the confirm, and that refusal has to read as
 * the platform's problem.
 */
@OptIn(ExperimentalCoroutinesApi::class)
class PendingOffersViewModelTest {

    @get:Rule
    val mainRule = MainDispatcherRule()

    private lateinit var ordersRepository: OrdersRepository
    private lateinit var errorTranslator: ApiErrorTranslator
    private lateinit var snackbar: SnackbarController
    private lateinit var offers: MutableStateFlow<List<PendingOffer>>

    private val weeklyCapKey = "order.weekly_limit_reached"

    @Before
    fun setUp() {
        ordersRepository = mockk(relaxed = true)
        errorTranslator = mockk()
        snackbar = mockk(relaxed = true)
        offers = MutableStateFlow(emptyList())
        every { ordersRepository.pendingOffers } returns offers
        every { ordersRepository.arePendingOffersStale() } returns true
        every { errorTranslator.translate(any()) } returns "translated error"
    }

    private fun viewModel() = PendingOffersViewModel(ordersRepository, errorTranslator, snackbar)

    private fun taken(id: String) = WorkContractOutcome.Taken(WorkContractRequest.Take(id))

    private fun refused(id: String, error: ApiError) = WorkContractOutcome.Refused(WorkContractRequest.Take(id), error)

    private fun offer(id: String) = PendingOffer(
        id = id,
        displayOrderNumber = "CL-$id",
        cleaningDateTime = "2026-08-12T09:00:00Z",
        estimatedTime = 120,
        respondByUtc = "2026-08-10T18:40:00Z",
        customerAddressApproximate = "Praha 4 · 14000",
        rooms = 2,
        bathrooms = 1,
        currencyCode = "CZK",
        estimatedCleanerPay = 600.0,
    )

    private fun mineJob(id: String) = OrderItem(
        id = id,
        orderStatus = Code(value = 2),
        isAssignedToCurrentUser = true,
        customerAddressApproximate = "Praha 4 · 14000",
        address = OrderAddress(street = "Mánesova 12", city = "Praha", latitude = 50.07, longitude = 14.44),
    )

    /**
     * A server that keeps its own rows, because the ViewModel re-asks after every action and a stub
     * that always replays the same list would model a server that forgets the write it just accepted.
     */
    private var serverRows: List<PendingOffer> = emptyList()

    private fun serverHolds(vararg rows: PendingOffer) {
        serverRows = rows.toList()
        coEvery { ordersRepository.refreshPendingOffers() } answers {
            offers.value = serverRows
            ApiResult.Success(serverRows)
        }
    }

    private fun serverAccepts(action: () -> Unit) {
        action()
        offers.value = serverRows
    }

    @Test
    fun `the list renders exactly what the server sent, coarse address included`() = runTest {
        val row = offer("a")
        serverHolds(row)

        val vm = viewModel()
        assertEquals(PendingOffersUiState.Loading, vm.uiState.value)
        advanceUntilIdle()

        assertEquals(PendingOffersUiState.Loaded(listOf(row)), vm.uiState.value)
        val rendered = (vm.uiState.value as PendingOffersUiState.Loaded).offers.single()
        assertEquals("Praha 4 · 14000", rendered.customerAddressApproximate)
        assertEquals("2026-08-10T18:40:00Z", rendered.respondByUtc)
    }

    @Test
    fun `no offers is a loaded empty list, never an error`() = runTest {
        serverHolds()

        val vm = viewModel()
        advanceUntilIdle()

        assertEquals(PendingOffersUiState.Loaded(emptyList()), vm.uiState.value)
    }

    @Test
    fun `a first load that fails with nothing cached is the error state`() = runTest {
        coEvery { ordersRepository.refreshPendingOffers() } returns ApiResult.Error(ApiError.Network("down"))

        val vm = viewModel()
        advanceUntilIdle()

        assertEquals(PendingOffersUiState.Error, vm.uiState.value)
    }

    @Test
    fun `declining calls the decline endpoint and the offer leaves the list`() = runTest {
        val kept = offer("keep")
        val refused = offer("refuse")
        serverHolds(kept, refused)
        val vm = viewModel()
        advanceUntilIdle()

        coEvery { ordersRepository.declinePreferredOffer("refuse") } answers {
            serverAccepts { serverRows = listOf(kept) }
            ApiResult.Success(Unit)
        }

        vm.decline(refused)
        advanceUntilIdle()

        coVerify(exactly = 1) { ordersRepository.declinePreferredOffer("refuse") }
        assertEquals(listOf("keep"), (vm.uiState.value as PendingOffersUiState.Loaded).offers.map { it.id })
        assertEquals(ActionState.Idle, vm.actionState.value)
    }

    /**
     * A failed RELEASE is not a failed handover. The job was never handed over, nothing changed, and
     * almost every cause is transient — so the refusal has to carry which action it belongs to, or the
     * screen apologises for something that did not happen.
     */
    @Test
    fun `a refused decline reads as a release failure, not as a handover failure`() = runTest {
        val row = offer("a")
        serverHolds(row)
        val vm = viewModel()
        advanceUntilIdle()
        coEvery { ordersRepository.declinePreferredOffer("a") } returns
            ApiResult.Error(ApiError.Network("down"))

        vm.decline(row)
        advanceUntilIdle()

        assertEquals(OfferAction.Decline, vm.offerRefusal.value?.action)
        assertEquals("translated error", vm.offerRefusal.value?.reason)
        assertEquals("CL-a", vm.offerRefusal.value?.displayOrderNumber)
        assertEquals(listOf("a"), (vm.uiState.value as PendingOffersUiState.Loaded).offers.map { it.id })
        verify(exactly = 0) { snackbar.showError(any<String>()) }
        verify(exactly = 0) { snackbar.showError(any<ApiError>()) }
    }

    /**
     * Confirming is TakeOrder — the shipped command with its one ordered Cascade.Stop chain. A UI that
     * reached for anything else would have built a second, weaker take gate. The sheet runs it; the
     * confirm opens the sheet for exactly this offer and takes nothing on its own.
     */
    @Test
    fun `confirming opens the contract sheet for the offer and takes nothing itself`() = runTest {
        val row = offer("a")
        serverHolds(row)
        val vm = viewModel()
        advanceUntilIdle()

        vm.confirm(row)
        advanceUntilIdle()

        assertEquals(WorkContractRequest.Take("a"), vm.contractRequest.value)
        coVerify(exactly = 0) { ordersRepository.takeOrder(any(), any()) }
        assertEquals(ActionState.Idle, vm.actionState.value)
    }

    @Test
    fun `a Taken verdict keeps the sheet up on the taken panel with the offer's area and reward, opening nothing`() = runTest {
        val row = offer("a")
        serverHolds(row)
        coEvery { ordersRepository.getById("a") } coAnswers {
            delay(1_000)
            ApiResult.Success(mineJob("a"))
        }
        val vm = viewModel()
        advanceUntilIdle()

        val opened = mutableListOf<String>()
        val job = launch { vm.confirmed.collect { opened += it } }
        advanceUntilIdle()
        vm.confirm(row)

        vm.onWorkContractOutcome(taken("a"))
        advanceTimeBy(500)

        assertEquals(WorkContractRequest.Take("a"), vm.contractRequest.value)
        val panel = vm.takenRow.value
        assertEquals("a", panel?.id)
        assertEquals("Praha 4 · 14000", panel?.customerAddressApproximate)
        assertEquals(600.0, panel?.estimatedCleanerPay)
        assertEquals("CZK", panel?.currency?.code)
        assertEquals("2026-08-12T09:00:00Z", panel?.cleaningDateTime)
        assertEquals(120, panel?.estimatedTime)
        assertNull("the strip waits for the job read back as hers", panel?.customerAddressLatitude)
        assertTrue(opened.isEmpty())
        advanceUntilIdle()
        job.cancel()
    }

    @Test
    fun `the job read back as hers replaces the offer's row, so the strip pins the door`() = runTest {
        val row = offer("a")
        serverHolds(row)
        coEvery { ordersRepository.getById("a") } returns ApiResult.Success(mineJob("a"))
        val vm = viewModel()
        advanceUntilIdle()
        vm.confirm(row)

        vm.onWorkContractOutcome(taken("a"))
        advanceUntilIdle()

        val panel = vm.takenRow.value
        assertEquals(50.07, panel?.customerAddressLatitude)
        assertEquals(14.44, panel?.customerAddressLongitude)
        assertEquals(2, panel?.orderStatus?.value)
        assertEquals(WorkContractRequest.Take("a"), vm.contractRequest.value)
        coVerify(exactly = 2) { ordersRepository.refreshPendingOffers() }
        assertEquals(ActionState.Idle, vm.actionState.value)
    }

    @Test
    fun `a failed read-back keeps the offer's row, so there is no strip`() = runTest {
        val row = offer("a")
        serverHolds(row)
        coEvery { ordersRepository.getById("a") } returns ApiResult.Error(ApiError.Network("down"))
        val vm = viewModel()
        advanceUntilIdle()
        vm.confirm(row)

        vm.onWorkContractOutcome(taken("a"))
        advanceUntilIdle()

        assertEquals("Praha 4 · 14000", vm.takenRow.value?.customerAddressApproximate)
        assertNull(vm.takenRow.value?.customerAddressLatitude)
        assertEquals(WorkContractRequest.Take("a"), vm.contractRequest.value)
    }

    @Test
    fun `Open job closes the panel and hands the screen the job to open`() = runTest {
        val row = offer("a")
        serverHolds(row)
        coEvery { ordersRepository.getById("a") } returns ApiResult.Success(mineJob("a"))
        val vm = viewModel()
        advanceUntilIdle()
        val opened = mutableListOf<String>()
        val job = launch { vm.confirmed.collect { opened += it } }
        vm.confirm(row)
        vm.onWorkContractOutcome(taken("a"))
        advanceUntilIdle()

        vm.openTakenJob()
        advanceUntilIdle()

        assertEquals(listOf("a"), opened)
        assertNull(vm.contractRequest.value)
        assertNull(vm.takenRow.value)
        job.cancel()
    }

    @Test
    fun `Back to jobs closes the panel onto the offers list and opens nothing`() = runTest {
        val row = offer("a")
        serverHolds(row)
        coEvery { ordersRepository.getById("a") } returns ApiResult.Success(mineJob("a"))
        val vm = viewModel()
        advanceUntilIdle()
        val opened = mutableListOf<String>()
        val job = launch { vm.confirmed.collect { opened += it } }
        vm.confirm(row)
        vm.onWorkContractOutcome(taken("a"))
        advanceUntilIdle()

        vm.dismissContract()
        advanceUntilIdle()

        assertTrue(opened.isEmpty())
        assertNull(vm.contractRequest.value)
        assertNull(vm.takenRow.value)
        job.cancel()
    }

    @Test
    fun `dismissing the sheet leaves the offer where it was`() = runTest {
        val row = offer("a")
        serverHolds(row)
        val vm = viewModel()
        advanceUntilIdle()
        vm.confirm(row)

        vm.dismissContract()
        advanceUntilIdle()

        assertNull(vm.contractRequest.value)
        coVerify(exactly = 1) { ordersRepository.refreshPendingOffers() }
        assertEquals(listOf("a"), (vm.uiState.value as PendingOffersUiState.Loaded).offers.map { it.id })
    }

    /**
     * The seam working as ruled: a reservation may not spend a cleaner's capacity, and the weekly cap
     * IS capacity, so the cap is never consulted when the job is reserved — only when it is confirmed.
     * Under a disclosed offer that is a visible broken promise, so the refusal is arranged here from
     * the server's real key and must survive to the screen with its own reason intact.
     */
    @Test
    fun `a confirm the weekly cap refuses is carried to the screen with the server's own reason`() = runTest {
        val row = offer("a")
        serverHolds(row)
        val vm = viewModel()
        advanceUntilIdle()

        val cap = ApiError.BadRequest(
            message = "A validation problem occurred.",
            code = null,
            validationErrors = mapOf("Command" to listOf(weeklyCapKey)),
            errorKey = weeklyCapKey,
        )
        val handed = slot<ApiError>()
        every { errorTranslator.translate(capture(handed)) } returns "You've reached your weekly order limit."

        vm.confirm(row)
        vm.onWorkContractOutcome(refused("a", cap))
        advanceUntilIdle()

        assertEquals(weeklyCapKey, (handed.captured as ApiError.BadRequest).errorKey)
        assertEquals(
            ActionState.Error("You've reached your weekly order limit."),
            vm.actionState.value,
        )
        assertEquals(OfferAction.Confirm, vm.offerRefusal.value?.action)
        assertEquals("CL-a", vm.offerRefusal.value?.displayOrderNumber)
        assertEquals("You've reached your weekly order limit.", vm.offerRefusal.value?.reason)
    }

    @Test
    fun `a refused confirm leaves the offer in a sane state and re-asks the server`() = runTest {
        val row = offer("a")
        serverHolds(row)
        val vm = viewModel()
        advanceUntilIdle()
        vm.confirm(row)
        vm.onWorkContractOutcome(refused("a", ApiError.BadRequest("nope", null, null, "order.no_available_spots")))
        advanceUntilIdle()

        // The server decides whether the row survives, so the list is re-asked rather than guessed at.
        coVerify(exactly = 2) { ordersRepository.refreshPendingOffers() }
        assertTrue(vm.uiState.value is PendingOffersUiState.Loaded)
    }

    /**
     * The blocked banner owns this message. A snackbar as well would state the bare reason without the
     * sentence that puts the failure on the platform, and the last one shown wins the cleaner's eye.
     */
    @Test
    fun `a refused confirm does not also snackbar the bare reason`() = runTest {
        val row = offer("a")
        serverHolds(row)
        val vm = viewModel()
        advanceUntilIdle()
        vm.confirm(row)
        vm.onWorkContractOutcome(refused("a", ApiError.BadRequest("nope", null, null, weeklyCapKey)))
        advanceUntilIdle()

        verify(exactly = 0) { snackbar.showError(any<String>()) }
        verify(exactly = 0) { snackbar.showError(any<ApiError>()) }
    }

    @Test
    fun `dismissing the refusal clears it without touching the list`() = runTest {
        val row = offer("a")
        serverHolds(row)
        val vm = viewModel()
        advanceUntilIdle()
        vm.confirm(row)
        vm.onWorkContractOutcome(refused("a", ApiError.BadRequest("nope", null, null, weeklyCapKey)))
        advanceUntilIdle()

        vm.dismissRefusal()

        assertEquals(ActionState.Idle, vm.actionState.value)
        assertNull(vm.offerRefusal.value)
        assertEquals(listOf("a"), (vm.uiState.value as PendingOffersUiState.Loaded).offers.map { it.id })
    }

    @Test
    fun `an action that succeeds clears the refusal left by the one before it`() = runTest {
        val row = offer("a")
        serverHolds(row)
        val vm = viewModel()
        advanceUntilIdle()
        coEvery { ordersRepository.declinePreferredOffer("a") } returns
            ApiResult.Error(ApiError.Network("down"))
        vm.decline(row)
        advanceUntilIdle()
        assertEquals(OfferAction.Decline, vm.offerRefusal.value?.action)

        coEvery { ordersRepository.declinePreferredOffer("a") } answers {
            serverAccepts { serverRows = emptyList() }
            ApiResult.Success(Unit)
        }
        vm.decline(row)
        advanceUntilIdle()

        assertNull(vm.offerRefusal.value)
    }

    @Test
    fun `a second action while one is in flight is refused`() = runTest {
        val a = offer("a")
        val b = offer("b")
        serverHolds(a, b)
        val vm = viewModel()
        advanceUntilIdle()
        coEvery { ordersRepository.declinePreferredOffer(any()) } coAnswers {
            delay(1_000)
            ApiResult.Success(Unit)
        }

        vm.decline(a)
        vm.confirm(b)
        // Far enough for the rival to have opened had the guard been missing, and short of the
        // decline's own completion — asserting before any dispatch would pass with no guard at all.
        advanceTimeBy(500)

        assertNull("a confirm may not open the sheet over a release in flight", vm.contractRequest.value)
        assertEquals(ActionState.Submitting, vm.actionState.value)
        assertEquals(OfferAction.Decline, vm.attempt.value?.action)
        advanceUntilIdle()
    }

    @Test
    fun `a warm cache is not refetched on entry`() = runTest {
        every { ordersRepository.arePendingOffersStale() } returns false
        offers.value = listOf(offer("a"))

        val vm = viewModel()
        advanceUntilIdle()

        coVerify(exactly = 0) { ordersRepository.refreshPendingOffers() }
        assertEquals(listOf("a"), (vm.uiState.value as PendingOffersUiState.Loaded).offers.map { it.id })
    }
}
