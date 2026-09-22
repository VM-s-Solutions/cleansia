package cz.cleansia.partner.features.orders

import cz.cleansia.core.network.ApiError
import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.ui.state.ActionState
import cz.cleansia.partner.core.settings.AppSettingsRepository
import cz.cleansia.partner.data.orders.OrdersRepository
import cz.cleansia.partner.data.orders.WorkContract
import cz.cleansia.partner.data.orders.WorkContractAcceptanceFacts
import cz.cleansia.partner.data.orders.WorkContractJobFacts
import cz.cleansia.partner.testing.MainDispatcherRule
import io.mockk.coEvery
import io.mockk.coVerify
import io.mockk.mockk
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch
import kotlinx.coroutines.test.advanceTimeBy
import kotlinx.coroutines.test.TestScope
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Rule
import org.junit.Test

/**
 * The sheet is where the take now happens, and what it sends is the whole point: the text row the
 * cleaner was shown, echoed on the swipe. The mismatch loop is the one refusal it owns — re-run the
 * preview, reset the gesture, say so — and every other verdict is handed to the host untouched.
 */
@OptIn(ExperimentalCoroutinesApi::class)
class WorkContractSheetViewModelTest {

    @get:Rule
    val mainRule = MainDispatcherRule()

    private lateinit var ordersRepository: OrdersRepository
    private lateinit var appSettings: AppSettingsRepository

    private val orderId = "order-1"
    private val take = WorkContractRequest.Take(orderId)

    @Before
    fun setUp() {
        ordersRepository = mockk(relaxed = true)
        appSettings = mockk()
        coEvery { appSettings.emailLanguageTag() } returns "cs"
    }

    private fun viewModel() = WorkContractSheetViewModel(ordersRepository, appSettings)

    private fun contract(textId: String, acceptance: WorkContractAcceptanceFacts? = null) = WorkContract(
        legalDocumentTextId = textId,
        version = "2026-09-20",
        language = "cs",
        title = "Smlouva o dílo",
        contentHtml = "<p>text</p>",
        facts = WorkContractJobFacts(
            orderNumber = "CL-2026-0042",
            cleaningDateTimeUtc = "2026-08-12T09:00:00Z",
            estimatedMinutes = 180,
            totalPrice = 1850.0,
            currencyCode = "CZK",
            locationApproximate = "Praha 4 · 14000",
            rooms = 3,
            bathrooms = 1,
            services = listOf("Standard cleaning"),
            packages = emptyList(),
            extraSlugs = emptyList(),
        ),
        acceptance = acceptance,
    )

    private fun badRequest(key: String) = ApiError.BadRequest(
        message = "A validation problem occurred.",
        validationErrors = mapOf("Command" to listOf(key)),
        errorKey = key,
    )

    private fun runTestCollectingOutcomes(
        block: suspend TestScope.(WorkContractSheetViewModel, List<WorkContractOutcome>) -> Unit,
    ) = runTest {
            val vm = viewModel()
            val outcomes = mutableListOf<WorkContractOutcome>()
            val job = launch { vm.outcome.collect { outcomes += it } }
            advanceUntilIdle()
            block(vm, outcomes)
            job.cancel()
        }

    @Test
    fun `opening for a take loads the preview in the app's language`() = runTest {
        val preview = contract("text-1")
        coEvery { ordersRepository.getWorkContractPreview(orderId, "cs") } returns ApiResult.Success(preview)

        val vm = viewModel()
        vm.open(take)
        assertEquals(WorkContractUiState.Loading, vm.uiState.value)
        advanceUntilIdle()

        assertEquals(WorkContractUiState.Loaded(preview), vm.uiState.value)
        assertEquals(ActionState.Idle, vm.actionState.value)
        assertNull(vm.notice.value)
    }

    @Test
    fun `the swipe takes the order once with the previewed text id and hands the host a Taken`() =
        runTestCollectingOutcomes { vm, outcomes ->
            coEvery { ordersRepository.getWorkContractPreview(orderId, "cs") } returns ApiResult.Success(contract("text-1"))
            coEvery { ordersRepository.takeOrder(orderId, "text-1") } returns ApiResult.Success(Unit)
            vm.open(take)
            advanceUntilIdle()

            vm.accept()
            advanceUntilIdle()

            coVerify(exactly = 1) { ordersRepository.takeOrder(orderId, "text-1") }
            coVerify(exactly = 0) { ordersRepository.acceptWorkContract(any(), any()) }
            assertEquals(listOf<WorkContractOutcome>(WorkContractOutcome.Taken(take)), outcomes)
            assertEquals(ActionState.Idle, vm.actionState.value)
        }

    @Test
    fun `the swipe is busy while the take is in flight and refuses a second swipe`() = runTest {
        coEvery { ordersRepository.getWorkContractPreview(orderId, "cs") } returns ApiResult.Success(contract("text-1"))
        coEvery { ordersRepository.takeOrder(orderId, "text-1") } coAnswers {
            delay(1_000)
            ApiResult.Success(Unit)
        }
        val vm = viewModel()
        vm.open(take)
        advanceUntilIdle()

        vm.accept()
        vm.accept()
        advanceTimeBy(500)

        assertEquals(ActionState.Submitting, vm.actionState.value)
        advanceUntilIdle()
        coVerify(exactly = 1) { ordersRepository.takeOrder(orderId, "text-1") }
    }

    /**
     * The mismatch contract: the echoed text is not this order's, so the sheet re-runs the preview,
     * renders whatever the server says now, springs the gesture back and tells the cleaner to read
     * again. Nothing reaches the host — the sheet has not ended.
     */
    @Test
    fun `a text mismatch reloads the preview, resets the swipe, shows the notice and stays open`() =
        runTestCollectingOutcomes { vm, outcomes ->
            val first = contract("text-1")
            val second = contract("text-2")
            coEvery { ordersRepository.getWorkContractPreview(orderId, "cs") } returnsMany listOf(
                ApiResult.Success(first),
                ApiResult.Success(second),
            )
            coEvery { ordersRepository.takeOrder(orderId, "text-1") } returns
                ApiResult.Error(badRequest("contract.text_mismatch"))
            vm.open(take)
            advanceUntilIdle()

            vm.accept()
            advanceUntilIdle()

            coVerify(exactly = 2) { ordersRepository.getWorkContractPreview(orderId, "cs") }
            coVerify(exactly = 1) { ordersRepository.takeOrder(any(), any()) }
            assertEquals(WorkContractUiState.Loaded(second), vm.uiState.value)
            assertEquals(ActionState.Idle, vm.actionState.value)
            assertEquals(WorkContractNotice.TextUpdated, vm.notice.value)
            assertTrue("a mismatch is the sheet's to handle, not the host's", outcomes.isEmpty())
        }

    @Test
    fun `after a mismatch the next swipe echoes the re-previewed text`() = runTestCollectingOutcomes { vm, outcomes ->
        coEvery { ordersRepository.getWorkContractPreview(orderId, "cs") } returnsMany listOf(
            ApiResult.Success(contract("text-1")),
            ApiResult.Success(contract("text-2")),
        )
        coEvery { ordersRepository.takeOrder(orderId, "text-1") } returns ApiResult.Error(badRequest("contract.text_mismatch"))
        coEvery { ordersRepository.takeOrder(orderId, "text-2") } returns ApiResult.Success(Unit)
        vm.open(take)
        advanceUntilIdle()
        vm.accept()
        advanceUntilIdle()

        vm.accept()
        advanceUntilIdle()

        coVerify(exactly = 1) { ordersRepository.takeOrder(orderId, "text-2") }
        assertEquals(listOf<WorkContractOutcome>(WorkContractOutcome.Taken(take)), outcomes)
    }

    /**
     * A refusal that is not about the contract — the weekly cap, a seat already gone — is the host's
     * to frame in its own words, exactly as when the take was one tap; the sheet neither swallows nor
     * reloads on it.
     */
    @Test
    fun `any other refusal is handed to the host and the preview is not reloaded`() =
        runTestCollectingOutcomes { vm, outcomes ->
            coEvery { ordersRepository.getWorkContractPreview(orderId, "cs") } returns ApiResult.Success(contract("text-1"))
            val cap = badRequest("order.weekly_limit_reached")
            coEvery { ordersRepository.takeOrder(orderId, "text-1") } returns ApiResult.Error(cap)
            vm.open(take)
            advanceUntilIdle()

            vm.accept()
            advanceUntilIdle()

            coVerify(exactly = 1) { ordersRepository.getWorkContractPreview(orderId, "cs") }
            assertEquals(listOf<WorkContractOutcome>(WorkContractOutcome.Refused(take, cap)), outcomes)
            assertNull(vm.notice.value)
            assertEquals(ActionState.Idle, vm.actionState.value)
        }

    @Test
    fun `a preview with no text in force is unavailable, with nothing to swipe`() = runTestCollectingOutcomes { vm, outcomes ->
        coEvery { ordersRepository.getWorkContractPreview(orderId, "cs") } returns
            ApiResult.Error(badRequest("legal.document_not_found"))
        vm.open(take)
        advanceUntilIdle()

        assertEquals(WorkContractUiState.Unavailable, vm.uiState.value)

        vm.accept()
        advanceUntilIdle()

        coVerify(exactly = 0) { ordersRepository.takeOrder(any(), any()) }
        assertTrue(outcomes.isEmpty())
    }

    @Test
    fun `a preview that fails for any other reason is the error state and retry re-asks`() = runTest {
        coEvery { ordersRepository.getWorkContractPreview(orderId, "cs") } returnsMany listOf(
            ApiResult.Error(ApiError.Network("down")),
            ApiResult.Success(contract("text-1")),
        )
        val vm = viewModel()
        vm.open(take)
        advanceUntilIdle()
        assertEquals(WorkContractUiState.Error, vm.uiState.value)

        vm.retry()
        advanceUntilIdle()

        assertTrue(vm.uiState.value is WorkContractUiState.Loaded)
    }

    @Test
    fun `accept mode previews the same text and swipes the standalone acceptance`() =
        runTestCollectingOutcomes { vm, outcomes ->
            val accept = WorkContractRequest.Accept(orderId)
            coEvery { ordersRepository.getWorkContractPreview(orderId, "cs") } returns ApiResult.Success(contract("text-1"))
            coEvery { ordersRepository.acceptWorkContract(orderId, "text-1") } returns ApiResult.Success(Unit)
            vm.open(accept)
            advanceUntilIdle()

            vm.accept()
            advanceUntilIdle()

            coVerify(exactly = 1) { ordersRepository.acceptWorkContract(orderId, "text-1") }
            coVerify(exactly = 0) { ordersRepository.takeOrder(any(), any()) }
            assertEquals(listOf<WorkContractOutcome>(WorkContractOutcome.Accepted(accept)), outcomes)
        }

    @Test
    fun `accept mode handles a mismatch the same way`() = runTestCollectingOutcomes { vm, outcomes ->
        val accept = WorkContractRequest.Accept(orderId)
        coEvery { ordersRepository.getWorkContractPreview(orderId, "cs") } returnsMany listOf(
            ApiResult.Success(contract("text-1")),
            ApiResult.Success(contract("text-2")),
        )
        coEvery { ordersRepository.acceptWorkContract(orderId, "text-1") } returns
            ApiResult.Error(badRequest("contract.text_mismatch"))
        vm.open(accept)
        advanceUntilIdle()

        vm.accept()
        advanceUntilIdle()

        assertEquals(WorkContractNotice.TextUpdated, vm.notice.value)
        assertEquals(WorkContractUiState.Loaded(contract("text-2")), vm.uiState.value)
        assertTrue(outcomes.isEmpty())
    }

    @Test
    fun `read mode loads the accepted contract by its acceptance and a swipe does nothing`() =
        runTestCollectingOutcomes { vm, outcomes ->
            val stored = contract(
                "text-1",
                WorkContractAcceptanceFacts(acceptedOn = "2026-08-10T18:40:00Z", documentVersion = "2026-09-20", acceptedLanguage = "cs"),
            )
            coEvery { ordersRepository.getWorkContract("acceptance-1", "cs") } returns ApiResult.Success(stored)
            vm.open(WorkContractRequest.Read("acceptance-1"))
            advanceUntilIdle()

            assertEquals(WorkContractUiState.Loaded(stored), vm.uiState.value)

            vm.accept()
            advanceUntilIdle()

            coVerify(exactly = 0) { ordersRepository.takeOrder(any(), any()) }
            coVerify(exactly = 0) { ordersRepository.acceptWorkContract(any(), any()) }
            coVerify(exactly = 0) { ordersRepository.getWorkContractPreview(any(), any()) }
            assertTrue(outcomes.isEmpty())
            assertEquals(ActionState.Idle, vm.actionState.value)
        }

    /** The preview is the server's word at that moment: a second open never shows the first one's text. */
    @Test
    fun `every open reloads and clears what the previous open left behind`() = runTest {
        coEvery { ordersRepository.getWorkContractPreview(orderId, "cs") } returnsMany listOf(
            ApiResult.Success(contract("text-1")),
            ApiResult.Success(contract("text-2")),
            ApiResult.Success(contract("text-3")),
        )
        coEvery { ordersRepository.takeOrder(orderId, "text-1") } returns ApiResult.Error(badRequest("contract.text_mismatch"))
        val vm = viewModel()
        vm.open(take)
        advanceUntilIdle()
        vm.accept()
        advanceUntilIdle()
        assertEquals(WorkContractNotice.TextUpdated, vm.notice.value)

        vm.open(take)
        advanceUntilIdle()

        assertNull(vm.notice.value)
        assertEquals(WorkContractUiState.Loaded(contract("text-3")), vm.uiState.value)
        coVerify(exactly = 3) { ordersRepository.getWorkContractPreview(orderId, "cs") }
    }
}
