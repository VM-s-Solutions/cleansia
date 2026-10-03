package cz.cleansia.customer.features.payments

import android.content.Context
import app.cash.turbine.test
import cz.cleansia.core.network.ApiError
import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.core.ui.state.ActionState
import cz.cleansia.customer.R
import cz.cleansia.customer.core.payments.Receivable
import cz.cleansia.customer.core.payments.ReceivableRepository
import cz.cleansia.customer.core.payments.SavedCard
import cz.cleansia.customer.core.payments.SavedCardRepository
import cz.cleansia.customer.testing.MainDispatcherRule
import io.mockk.coEvery
import io.mockk.coVerify
import io.mockk.every
import io.mockk.mockk
import io.mockk.verify
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.runTest
import kotlinx.datetime.Instant
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Before
import org.junit.Rule
import org.junit.Test

@OptIn(ExperimentalCoroutinesApi::class)
class PaymentsViewModelTest {

    @get:Rule
    val mainRule = MainDispatcherRule()

    private lateinit var savedCards: SavedCardRepository
    private lateinit var receivables: ReceivableRepository
    private lateinit var snackbar: SnackbarController
    private lateinit var appContext: Context

    private val card = SavedCard(id = "card-1", brand = "visa", last4 = "4242", expMonth = 4, expYear = 2029, currencyCode = "CZK")
    private val fee = Receivable(
        id = "rcv-1",
        orderId = "ord-1",
        displayOrderNumber = "CL-1042",
        kind = 1,
        amount = 450.0,
        currencyCode = "CZK",
        createdOn = Instant.parse("2026-09-28T10:00:00Z"),
    )
    private val serverDown = ApiError.Server(statusCode = 500, message = "Server unavailable.")

    @Before
    fun setUp() {
        savedCards = mockk()
        receivables = mockk()
        snackbar = mockk(relaxed = true)
        appContext = mockk(relaxed = true)
        every { appContext.getString(R.string.error_generic_server) } returns "server"
        every { appContext.getString(R.string.error_generic_unknown) } returns "unknown"
        every { appContext.packageName } returns "cz.cleansia.customer"
        val resources = mockk<android.content.res.Resources>(relaxed = true)
        every { appContext.resources } returns resources
        every { resources.getIdentifier(any(), any(), any()) } returns 0
        coEvery { savedCards.refresh() } returns ApiResult.Success(listOf(card))
        coEvery { receivables.getMine() } returns ApiResult.Success(listOf(fee))
    }

    private fun viewModel() = PaymentsViewModel(savedCards, receivables, snackbar, appContext)

    @Test
    fun `load shows what is owed beside the saved card`() = runTest {
        val vm = viewModel()
        assertEquals(PaymentsUiState.Loading, vm.state.value)

        advanceUntilIdle()

        assertEquals(PaymentsUiState.Loaded(receivables = listOf(fee), cards = listOf(card)), vm.state.value)
    }

    /** Half a page would say "nothing owed" or "no card" when the truth is "we could not read it". */
    @Test
    fun `either read failing is an error, never half a page`() = runTest {
        coEvery { receivables.getMine() } returns ApiResult.Error(serverDown)
        val owedFails = viewModel()
        advanceUntilIdle()
        assertEquals(PaymentsUiState.Error, owedFails.state.value)

        coEvery { receivables.getMine() } returns ApiResult.Success(listOf(fee))
        coEvery { savedCards.refresh() } returns ApiResult.Error(serverDown)
        val cardsFail = viewModel()
        advanceUntilIdle()
        assertEquals(PaymentsUiState.Error, cardsFail.state.value)
    }

    @Test
    fun `removing the card drops it from the page`() = runTest {
        coEvery { savedCards.remove("card-1") } returns ApiResult.Success(Unit)
        val vm = viewModel()
        advanceUntilIdle()

        vm.remove(card)
        assertEquals(ActionState.Submitting, vm.removeState.value)
        advanceUntilIdle()

        assertEquals(PaymentsUiState.Loaded(receivables = listOf(fee), cards = emptyList()), vm.state.value)
        assertEquals(ActionState.Idle, vm.removeState.value)
        verify(exactly = 1) { snackbar.showSuccessKey(R.string.payments_card_removed) }
    }

    /** The system confirm has already closed, so the refusal is the snackbar's and the row is free again. */
    @Test
    fun `a refused removal keeps the card and says so in the snackbar`() = runTest {
        coEvery { savedCards.remove("card-1") } returns ApiResult.Error(serverDown)
        val vm = viewModel()
        advanceUntilIdle()

        vm.remove(card)
        advanceUntilIdle()

        assertEquals(ActionState.Idle, vm.removeState.value)
        assertEquals(PaymentsUiState.Loaded(receivables = listOf(fee), cards = listOf(card)), vm.state.value)
        verify(exactly = 1) { snackbar.showError("Server unavailable.") }
    }

    @Test
    fun `paying hands the pay link to the browser and re-reads on return`() = runTest {
        coEvery { receivables.createPayLink("rcv-1") } returns ApiResult.Success("https://checkout.stripe.com/c/pay/cs_1")
        val vm = viewModel()
        advanceUntilIdle()

        vm.payLinks.test {
            vm.pay(fee)
            advanceUntilIdle()
            assertEquals("https://checkout.stripe.com/c/pay/cs_1", awaitItem())
        }

        coEvery { receivables.getMine() } returns ApiResult.Success(emptyList())
        vm.onResumed()
        advanceUntilIdle()

        assertEquals(PaymentsUiState.Loaded(receivables = emptyList(), cards = listOf(card)), vm.state.value)
        assertEquals(ActionState.Idle, vm.payState.value)
    }

    @Test
    fun `coming back without having paid reads nothing again`() = runTest {
        val vm = viewModel()
        advanceUntilIdle()

        vm.onResumed()
        advanceUntilIdle()

        coVerify(exactly = 1) { receivables.getMine() }
    }

    @Test
    fun `every receivable kind the server defines has its own label, and a later one reads as the generic`() {
        val labels = (1..4).map { receivableKindLabelRes(it) }

        assertEquals(4, labels.filterNotNull().toSet().size)
        assertNull(receivableKindLabelRes(0))
        assertNull(receivableKindLabelRes(5))
    }
}
