package cz.cleansia.customer.features.payments

import android.content.Context
import app.cash.turbine.test
import androidx.lifecycle.ViewModelStore
import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.customer.R
import cz.cleansia.customer.core.market.MarketListItem
import cz.cleansia.customer.core.market.MarketRepository
import cz.cleansia.customer.core.market.MarketState
import cz.cleansia.customer.core.payments.*
import cz.cleansia.customer.testing.MainDispatcherRule
import io.mockk.*
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.first
import org.junit.After
import kotlinx.coroutines.test.*
import kotlinx.serialization.json.Json
import org.junit.Assert.*
import org.junit.Rule
import org.junit.Test
import retrofit2.Response

@OptIn(kotlinx.coroutines.ExperimentalCoroutinesApi::class)
class PaymentsCardCaptureViewModelTest {
    @get:Rule val mainRule = MainDispatcherRule()
    private val api = mockk<SavedCardApi>()
    private val cards = SavedCardRepository(api, Json { ignoreUnknownKeys = true })
    private val receivables = mockk<ReceivableRepository>()
    private val snackbar = mockk<SnackbarController>(relaxed = true)
    private val context = mockk<Context>(relaxed = true)
    private val markets = mockk<MarketRepository>()
    private val marketState = MutableStateFlow<MarketState>(MarketState.Unavailable)
    private val setup = SavedCardSetup("new-card", "seti_fixture_secret", "cus_fixture", "ek_fixture")
    private val stores = mutableListOf<ViewModelStore>()
    private val card = SavedCard("new-card", "visa", "4242", 12, 2035, "CZK")

    private fun vm(key: String = "pk_test_fixture"): PaymentsViewModel {
        every { markets.state } returns marketState
        coEvery { receivables.getMine() } returns ApiResult.Success(emptyList())
        coEvery { api.createSetupIntent(true, any()) } returns Response.success(setup)
        coEvery { api.createSetupIntent(true, null) } returns Response.success(setup)
        return PaymentsViewModel(cards, receivables, snackbar, context, markets, StripeConfig(key)).also {
            stores += ViewModelStore().apply { put("payments", it) }
        }
    }

    @After
    fun clearViewModels() { stores.forEach { it.clear() } }

    private suspend fun TestScope.awaitLoaded(vm: PaymentsViewModel) {
        vm.state.first { it is PaymentsUiState.Loaded }
        advanceUntilIdle()
    }

    @Test
    fun `save requires both consent and existing build availability`() = runTest {
        coEvery { api.getMine() } returns Response.success(emptyList())
        val unavailable = vm("")
        awaitLoaded(unavailable)
        unavailable.setConsent(true)
        unavailable.saveCard()
        val available = vm()
        awaitLoaded(available)
        available.saveCard()
        advanceUntilIdle()
        coVerify(exactly = 0) { api.createSetupIntent(any(), any()) }
    }

    @Test
    fun `usable saved card prevents idle capture without hiding pending recovery`() = runTest {
        coEvery { api.getMine() } returns Response.success(listOf(card))
        val vm = vm()
        awaitLoaded(vm)
        assertFalse(vm.offersCardCapture.value)
        vm.setConsent(true)
        vm.saveCard()
        advanceUntilIdle()
        coVerify(exactly = 0) { api.createSetupIntent(any(), any()) }
    }

    @Test
    fun `cancel and failure keep screen usable without saved success`() = runTest {
        coEvery { api.getMine() } returns Response.success(emptyList())
        val vm = vm()
        awaitLoaded(vm)
        vm.capturePresentation.test {
            vm.setConsent(true)
            vm.saveCard()
            vm.saveCard()
            advanceUntilIdle()
            val presentation = awaitItem()
            vm.onCaptureResult(presentation, completed = false)
            advanceUntilIdle()
            assertTrue(cards.captures.value.isEmpty())
            vm.saveCard()
            advanceUntilIdle()
            vm.onCaptureResult(awaitItem(), completed = false, failed = true)
            advanceUntilIdle()
            assertTrue(cards.captures.value.isEmpty())
        }
        verify(exactly = 0) { snackbar.showSuccessKey(R.string.payments_card_saved) }
        verify(exactly = 1) { snackbar.showErrorKey(R.string.payments_card_setup_failed) }
        coVerify(exactly = 2) { api.createSetupIntent(any(), any()) }
    }

    @Test
    fun `late confirmation persists through route reentry without second setup`() = runTest {
        coEvery { api.getMine() } returns Response.success(emptyList())
        val first = vm()
        val store = ViewModelStore().apply { put("payments", first) }
        awaitLoaded(first)
        first.capturePresentation.test {
            first.setConsent(true)
            first.saveCard()
            advanceUntilIdle()
            first.onCaptureResult(awaitItem(), completed = true)
            advanceUntilIdle()
        }
        assertTrue(first.capture.value is SavedCardCapture.Pending)
        verify(exactly = 0) { snackbar.showSuccessKey(R.string.payments_card_saved) }
        store.clear()
        coEvery { api.getMine() } returns Response.success(listOf(card))
        val second = vm()
        awaitLoaded(second)
        cards.captures.first { it.isEmpty() }
        runCurrent()
        assertNull(second.capture.value)
        assertEquals(listOf(card), (second.state.value as PaymentsUiState.Loaded).cards)
        verify(exactly = 1) { snackbar.showSuccessKey(R.string.payments_card_saved) }
        coVerify(exactly = 1) { api.createSetupIntent(any(), any()) }
    }

    @Test
    fun `market switch pauses previous verification without discarding pending capture`() = runTest {
        coEvery { api.getMine() } returns Response.success(emptyList())
        val vm = vm()
        awaitLoaded(vm)
        vm.capturePresentation.test {
            vm.setConsent(true)
            vm.saveCard()
            advanceUntilIdle()
            vm.onCaptureResult(awaitItem(), completed = true)
            runCurrent()
        }
        val old = vm.captureMarket.value
        val eu = MarketListItem("svk", "SVK", "SK", "Slovakia", currencyId = "eur", currencyCode = "eur", currencySymbol = "€", isDefault = false)
        marketState.value = MarketState.Resolved(listOf(eu), eu)
        runCurrent()
        assertNull(vm.capture.value)
        assertFalse((cards.captures.value[old] as SavedCardCapture.Pending).verifying)
        assertEquals("EUR", vm.captureMarket.value.currencyCode)
        marketState.value = MarketState.Unavailable
        runCurrent()
        assertTrue(vm.capture.value is SavedCardCapture.Pending)
    }

    @Test
    fun `restart requires fresh consent and ignores earlier sheet callback`() = runTest {
        coEvery { api.getMine() } returns Response.success(emptyList())
        val vm = vm()
        awaitLoaded(vm)
        vm.capturePresentation.test {
            vm.setConsent(true)
            vm.saveCard()
            advanceUntilIdle()
            val old = awaitItem()
            vm.onCaptureResult(old, completed = true)
            advanceUntilIdle()
            vm.restartCapture()
            assertFalse(vm.consent.value)
            vm.saveCard()
            advanceUntilIdle()
            assertTrue(cards.captures.value.isEmpty())
            vm.setConsent(true)
            vm.saveCard()
            advanceUntilIdle()
            val current = awaitItem()
            vm.onCaptureResult(old, completed = true)
            assertEquals(current.attempt, cards.captures.value[current.market]?.attempt)
            assertTrue(cards.captures.value[current.market] is SavedCardCapture.Sheet)
        }
    }

    @Test
    fun `recreated screen callback uses retained ViewModel identity`() = runTest {
        coEvery { api.getMine() } returns Response.success(emptyList())
        val vm = vm()
        awaitLoaded(vm)
        vm.capturePresentation.test {
            vm.setConsent(true)
            vm.saveCard()
            advanceUntilIdle()
            assertNotNull(vm.claimCapturePresentation(awaitItem()))
        }
        // The replacement composable has no remembered presentation; ActivityResult still reaches this VM.
        vm.onPresentedCaptureResult(completed = true)
        advanceUntilIdle()
        assertTrue(vm.capture.value is SavedCardCapture.Pending)
        vm.restartCapture()
        vm.capturePresentation.test {
            vm.setConsent(true)
            vm.saveCard()
            advanceUntilIdle()
            awaitItem()
        }
        coVerify(exactly = 2) { api.createSetupIntent(any(), any()) }
    }

    @Test
    fun `setup finishing without active collector remains resumable`() = runTest {
        coEvery { api.getMine() } returns Response.success(emptyList())
        val vm = vm()
        awaitLoaded(vm)
        assertTrue("Idle capture must be offered: state=${vm.state.value}; cards=${cards.cards.value}", vm.offersCardCapture.value)
        vm.setConsent(true)
        vm.saveCard()
        advanceUntilIdle()
        cards.captures.first { it[vm.captureMarket.value] is SavedCardCapture.Sheet }
        runCurrent()
        assertTrue(vm.capture.value is SavedCardCapture.Sheet)
        vm.capturePresentation.test {
            vm.continueCapture()
            advanceUntilIdle()
            assertNotNull(vm.claimCapturePresentation(awaitItem()))
            vm.onPresentedCaptureResult(completed = false)
            advanceUntilIdle()
        }
        assertNull(vm.capture.value)
        coVerify(exactly = 1) { api.createSetupIntent(any(), any()) }
    }

    @Test
    fun `verification error retains pending then manual retry confirms exact card`() = runTest {
        coEvery { api.getMine() } returns Response.success(emptyList())
        val vm = vm()
        awaitLoaded(vm)
        vm.capturePresentation.test {
            vm.setConsent(true)
            vm.saveCard()
            advanceUntilIdle()
            val presentation = awaitItem()
            coEvery { api.getMine() } throws java.io.IOException("offline")
            vm.onCaptureResult(presentation, completed = true)
            advanceUntilIdle()
        }
        cards.captures.first { (it[vm.captureMarket.value] as? SavedCardCapture.Pending)?.verifying == false }
        runCurrent()
        assertTrue(vm.capture.value is SavedCardCapture.Pending)
        assertTrue(vm.consent.value)
        verify(exactly = 1) { snackbar.showError(any<String>()) }
        verify(exactly = 0) { snackbar.showSuccessKey(R.string.payments_card_saved) }
        coEvery { api.getMine() } returns Response.success(listOf(card))
        vm.verifyPending()
        cards.captures.first { it.isEmpty() }
        runCurrent()
        assertNull(vm.capture.value)
        assertFalse(vm.consent.value)
        verify(exactly = 1) { snackbar.showSuccessKey(R.string.payments_card_saved) }
    }

    @Test
    fun `session wipe clears capture consent and retained SDK callback identity`() = runTest {
        coEvery { api.getMine() } returns Response.success(emptyList())
        val vm = vm()
        awaitLoaded(vm)
        vm.capturePresentation.test {
            vm.setConsent(true)
            vm.saveCard()
            advanceUntilIdle()
            assertNotNull(vm.claimCapturePresentation(awaitItem()))
        }
        cards.clear()
        runCurrent()
        assertNull(vm.capture.value)
        assertFalse(vm.consent.value)
        vm.onPresentedCaptureResult(completed = true)
        advanceUntilIdle()
        assertTrue(cards.captures.value.isEmpty())
        verify(exactly = 0) { snackbar.showSuccessKey(R.string.payments_card_saved) }
    }

    @Test
    fun `background wipe rejects already consented stale screen before collectors resume`() = runTest {
        coEvery { api.getMine() } returns Response.success(emptyList())
        val vm = vm()
        awaitLoaded(vm)
        vm.setConsent(true)
        // AuthAuthenticator clears on an OkHttp thread. Block this test dispatcher until that clear returns.
        val wipe = Thread { kotlinx.coroutines.runBlocking { cards.clear() } }
        wipe.start()
        wipe.join()
        assertTrue(vm.consent.value)
        vm.saveCard()
        advanceUntilIdle()
        coVerify(exactly = 0) { api.createSetupIntent(any(), any()) }
        assertTrue(cards.captures.value.isEmpty())
    }

    @Test
    fun `delivered setup event cannot present after background wipe`() = runTest {
        coEvery { api.getMine() } returns Response.success(emptyList())
        val vm = vm()
        awaitLoaded(vm)
        vm.capturePresentation.test {
            vm.setConsent(true)
            vm.saveCard()
            val delivered = awaitItem()
            val wipe = Thread { kotlinx.coroutines.runBlocking { cards.clear() } }
            wipe.start()
            wipe.join()
            assertNull(vm.claimCapturePresentation(delivered))
            vm.onPresentedCaptureResult(completed = true)
            runCurrent()
            assertTrue(cards.captures.value.isEmpty())
        }
        verify(exactly = 0) { snackbar.showSuccessKey(R.string.payments_card_saved) }
    }

    @Test
    fun `delivered setup event cannot present in another market and remains resumable`() = runTest {
        coEvery { api.getMine() } returns Response.success(emptyList())
        val vm = vm()
        awaitLoaded(vm)
        vm.capturePresentation.test {
            vm.setConsent(true)
            vm.saveCard()
            val delivered = awaitItem()
            val eu = MarketListItem("svk", "SVK", "SK", "Slovakia", currencyId = "eur", currencyCode = "EUR", currencySymbol = "€", isDefault = false)
            marketState.value = MarketState.Resolved(listOf(eu), eu)
            assertNull(vm.claimCapturePresentation(delivered))
            marketState.value = MarketState.Unavailable
            runCurrent()
            vm.continueCapture()
            assertNotNull(vm.claimCapturePresentation(awaitItem()))
        }
        coVerify(exactly = 1) { api.createSetupIntent(any(), any()) }
    }
}
