package cz.cleansia.customer.core.payments

import cz.cleansia.core.network.ApiResult
import io.mockk.coEvery
import io.mockk.coVerify
import io.mockk.mockk
import kotlinx.coroutines.CompletableDeferred
import kotlinx.coroutines.NonCancellable
import kotlinx.coroutines.async
import kotlinx.coroutines.launch
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.runCurrent
import kotlinx.coroutines.test.runTest
import kotlinx.coroutines.withContext
import kotlinx.serialization.json.Json
import org.junit.Assert.*
import org.junit.Test
import retrofit2.Response

@OptIn(kotlinx.coroutines.ExperimentalCoroutinesApi::class)
class SavedCardCaptureTest {
    private val api = mockk<SavedCardApi>()
    private val repo = SavedCardRepository(api, Json { ignoreUnknownKeys = true })
    private val cz = CardCaptureMarket("cze", "CZK")
    private val eur = CardCaptureMarket("svk", "EUR")
    private fun setup(id: String) = SavedCardSetup(id, "seti_fixture_secret", "cus_fixture", "ek_fixture")
    private fun card(id: String, currency: String = "CZK") = SavedCard(id, "visa", "4242", 12, 2035, currency)

    @Test
    fun `idle capture preserves usable card currency and expiry policy including default market`() {
        val held = listOf(card("held"))
        val month = java.time.YearMonth.of(2026, 10)
        assertFalse(held.needsCapture("czk", month))
        assertTrue(held.needsCapture("EUR", month))
        assertFalse(held.needsCapture(null, month))
        assertTrue(listOf(card("expired").copy(expYear = 2025)).needsCapture("CZK", month))
        assertTrue(emptyList<SavedCard>().needsCapture(null, month))
    }

    private suspend fun pending(id: String = "new", market: CardCaptureMarket = cz): Long {
        coEvery { api.createSetupIntent(true, market.countryId) } returns Response.success(setup(id))
        repo.beginCapture(market, true)
        val attempt = repo.captures.value.getValue(market).attempt
        assertTrue(repo.sheetCompleted(market, attempt))
        return attempt
    }

    @Test
    fun `consent and duplicate guard permit only one setup request`() = runTest {
        val gate = CompletableDeferred<Response<SavedCardSetup>>()
        coEvery { api.createSetupIntent(true, "cze") } coAnswers { gate.await() }
        assertNull(repo.beginCapture(cz, false))
        val first = async { repo.beginCapture(cz, true) }
        runCurrent()
        assertTrue(repo.captures.value[cz] is SavedCardCapture.Starting)
        assertNull(repo.beginCapture(cz, true))
        gate.complete(Response.success(setup("new")))
        first.await()
        assertTrue(repo.captures.value[cz] is SavedCardCapture.Sheet)
        coVerify(exactly = 1) { api.createSetupIntent(any(), any()) }
    }

    @Test
    fun `sheet cancellation clears capture and preserves existing cards`() = runTest {
        coEvery { api.getMine() } returns Response.success(listOf(card("held")))
        repo.refresh()
        coEvery { api.createSetupIntent(true, "cze") } returns Response.success(setup("new"))
        repo.beginCapture(cz, true)
        repo.abandonCapture(cz, repo.captures.value.getValue(cz).attempt)
        assertNull(repo.captures.value[cz])
        assertEquals(listOf(card("held")), repo.cards.value)
    }

    @Test
    fun `sheet success is pending until exact returned card arrives`() = runTest {
        pending()
        coEvery { api.getMine() } returns Response.success(listOf(card("old")))
        assertEquals(ApiResult.Success(false), repo.verifyCapture(cz, 1))
        assertTrue(repo.captures.value[cz] is SavedCardCapture.Pending)
        coEvery { api.getMine() } returns Response.success(listOf(card("new")))
        assertEquals(ApiResult.Success(true), repo.verifyCapture(cz))
        assertNull(repo.captures.value[cz])
    }

    @Test
    fun `bounded verification survives route reentry and later manual refresh`() = runTest {
        pending()
        coEvery { api.getMine() } returns Response.success(emptyList())
        assertEquals(ApiResult.Success(false), repo.verifyCapture(cz))
        coVerify(exactly = 8) { api.getMine() }
        assertFalse((repo.captures.value[cz] as SavedCardCapture.Pending).verifying)
        // The singleton cache outlives either route ViewModel; leaving a route stops its task only.
        val route = launch { repo.verifyCapture(cz) }
        runCurrent()
        route.cancel()
        runCurrent()
        assertTrue(repo.captures.value[cz] is SavedCardCapture.Pending)
        coEvery { api.getMine() } returns Response.success(listOf(card("new")))
        assertEquals(ApiResult.Success(true), repo.verifyCapture(cz))
        coVerify(exactly = 1) { api.createSetupIntent(any(), any()) }
    }

    @Test
    fun `market states are separate and mismatched currency never confirms`() = runTest {
        pending("cz-card", cz)
        pending("eur-card", eur)
        coEvery { api.getMine() } returns Response.success(listOf(card("cz-card", "EUR")))
        assertEquals(ApiResult.Success(false), repo.verifyCapture(cz, 1))
        assertTrue(repo.captures.value[eur] is SavedCardCapture.Pending)
        coEvery { api.getMine() } returns Response.success(listOf(card("eur-card", "EUR")))
        assertEquals(ApiResult.Success(true), repo.verifyCapture(eur, 1))
        assertTrue(repo.captures.value[cz] is SavedCardCapture.Pending)
    }

    @Test
    fun `restart ignores old callbacks and old late webhook cannot confirm new setup`() = runTest {
        val old = pending("old")
        repo.abandonCapture(cz, old)
        val current = pending("new")
        assertFalse(repo.sheetCompleted(cz, old))
        repo.abandonCapture(cz, old)
        assertEquals(current, repo.captures.value[cz]?.attempt)
        coEvery { api.getMine() } returns Response.success(listOf(card("old")))
        assertEquals(ApiResult.Success(false), repo.verifyCapture(cz, 1))
        assertEquals(current, repo.captures.value[cz]?.attempt)
    }

    @Test
    fun `verification failure retains pending for retry`() = runTest {
        pending()
        coEvery { api.getMine() } throws java.io.IOException("offline")
        assertTrue(repo.verifyCapture(cz) is ApiResult.Error)
        assertFalse((repo.captures.value[cz] as SavedCardCapture.Pending).verifying)
    }

    @Test
    fun `session wipe cancels polling and rejects stale card publication`() = runTest {
        pending()
        val gate = CompletableDeferred<Response<List<SavedCard>>>()
        val entered = CompletableDeferred<Unit>()
        coEvery { api.getMine() } coAnswers { withContext(NonCancellable) { entered.complete(Unit); gate.await() } }
        val route = launch { repo.verifyCapture(cz) }
        runCurrent()
        entered.await()
        withContext(kotlinx.coroutines.Dispatchers.Default) { repo.clear() }
        assertTrue(route.isCancelled)
        assertTrue(repo.captures.value.isEmpty())
        assertNull(repo.cards.value)
        gate.complete(Response.success(listOf(card("new"))))
        advanceUntilIdle()
        assertNull(repo.cards.value)
        assertTrue(repo.captures.value.isEmpty())
    }

    @Test
    fun `background wipe rejects held old setup response and callback after a fresh capture`() = runTest {
        val gate = CompletableDeferred<Response<SavedCardSetup>>()
        val entered = CompletableDeferred<Unit>()
        coEvery { api.createSetupIntent(true, "cze") } coAnswers {
            withContext(NonCancellable) { entered.complete(Unit); gate.await() }
        }
        val oldRequest = async { repo.beginCapture(cz, true) }
        entered.await()
        val oldAttempt = repo.captures.value.getValue(cz).attempt
        withContext(kotlinx.coroutines.Dispatchers.Default) { repo.clear() }
        coEvery { api.createSetupIntent(true, "cze") } returns Response.success(setup("new-session"))
        repo.beginCapture(cz, true)
        val current = repo.captures.value[cz] as SavedCardCapture.Sheet
        gate.complete(Response.success(setup("old-session")))
        assertNull(oldRequest.await())
        assertFalse(repo.sheetCompleted(cz, oldAttempt))
        repo.abandonCapture(cz, oldAttempt)
        assertEquals(current, repo.captures.value[cz])
        assertEquals("new-session", current.setup.savedCardId)
    }
}
