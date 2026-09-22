package cz.cleansia.customer.core.market

import android.content.Context
import cz.cleansia.core.network.ApiError
import cz.cleansia.core.network.ApiResult
import cz.cleansia.customer.R
import cz.cleansia.customer.core.settings.AppSettings
import cz.cleansia.customer.core.settings.AppSettingsRepository
import io.mockk.coEvery
import io.mockk.coVerify
import io.mockk.every
import io.mockk.mockk
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.test.runTest
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.ResponseBody.Companion.toResponseBody
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test
import retrofit2.Response

/**
 * ADR-0058 D3: the stored code is compared against the directory and never trusted on its own —
 * stored-if-listed, else the default, else the first — and the result is persisted. A failed read
 * keeps whatever was known and names nothing.
 */
class MarketRepositoryTest {

    private lateinit var api: MarketApi
    private lateinit var settings: AppSettingsRepository
    private lateinit var appContext: Context
    private val stored = MutableStateFlow(AppSettings())

    @Before
    fun setUp() {
        api = mockk()
        settings = mockk(relaxed = true)
        appContext = mockk(relaxed = true)
        every { settings.settings } returns stored
        every { appContext.getString(R.string.error_generic_network) } returns "network"
        every { appContext.getString(R.string.error_generic_server) } returns "server"
        every { appContext.getString(R.string.error_generic_unknown) } returns "unknown"
        every { appContext.packageName } returns "cz.cleansia.customer"
        val resources = mockk<android.content.res.Resources>(relaxed = true)
        every { appContext.resources } returns resources
        every { resources.getIdentifier(any(), any(), any()) } returns 0
    }

    private fun newRepo() = MarketRepository(api, settings, appContext)

    private fun errorBody() = "{}".toResponseBody("application/json".toMediaType())

    private fun market(iso: String, alpha2: String, currency: String, isDefault: Boolean = false) = MarketListItem(
        countryId = "$iso-id",
        isoCode = iso,
        isoAlpha2 = alpha2,
        name = iso,
        currencyId = "$currency-id",
        currencyCode = currency,
        currencySymbol = currency,
        isDefault = isDefault,
    )

    private val cze = market("CZE", "CZ", "CZK", isDefault = true)
    private val svk = market("SVK", "SK", "EUR")

    private fun directory(vararg markets: MarketListItem) {
        coEvery { api.getMarkets() } returns Response.success(markets.toList())
    }

    // ── resolution ──

    @Test
    fun refresh_withNothingStored_selectsTheDefaultAndPersistsIt() = runTest {
        directory(svk, cze)

        val result = newRepo().refresh()

        val resolved = (result as ApiResult.Success).data as MarketState.Resolved
        assertEquals("CZE", resolved.selected.isoCode)
        assertEquals(listOf("SVK", "CZE"), resolved.markets.map { it.isoCode })
        coVerify(exactly = 1) { settings.setMarket("CZE") }
    }

    @Test
    fun refresh_withAListedCodeStored_selectsItAndPersistsNothing() = runTest {
        stored.value = AppSettings(market = "SVK")
        directory(cze, svk)

        val repo = newRepo()
        repo.refresh()

        assertEquals("SVK", (repo.state.value as MarketState.Resolved).selected.isoCode)
        assertEquals("SVK-id", repo.state.value.countryId)
        coVerify(exactly = 0) { settings.setMarket(any()) }
    }

    @Test
    fun refresh_withADelistedCodeStored_fallsToTheDefaultAndOverwritesIt() = runTest {
        stored.value = AppSettings(market = "POL")
        directory(cze, svk)

        val repo = newRepo()
        repo.refresh()

        assertEquals("CZE", (repo.state.value as MarketState.Resolved).selected.isoCode)
        coVerify(exactly = 1) { settings.setMarket("CZE") }
    }

    /** ADR-0058 D2's "none" case: nothing is flagged, so the first listed market is the pre-selection. */
    @Test
    fun refresh_withNoDefaultFlagged_selectsTheFirstListedMarket() = runTest {
        directory(market("DEU", "DE", "EUR"), market("SVK", "SK", "EUR"))

        val repo = newRepo()
        repo.refresh()

        assertEquals("DEU", (repo.state.value as MarketState.Resolved).selected.isoCode)
        coVerify(exactly = 1) { settings.setMarket("DEU") }
    }

    /** A stored value is only ever compared against the list; a junk string can never be selected. */
    @Test
    fun refresh_neverSelectsAStoredValueTheDirectoryDoesNotList() = runTest {
        stored.value = AppSettings(market = "<script>alert(1)</script>")
        directory(cze)

        val repo = newRepo()
        repo.refresh()

        assertEquals("CZE", (repo.state.value as MarketState.Resolved).selected.isoCode)
    }

    @Test
    fun refresh_withAnEmptyDirectory_isUnavailable() = runTest {
        directory()

        val repo = newRepo()
        val result = repo.refresh()

        assertEquals(MarketState.Unavailable, repo.state.value)
        assertEquals(MarketState.Unavailable, (result as ApiResult.Success).data)
        coVerify(exactly = 0) { settings.setMarket(any()) }
    }

    // ── failure keeps the last known list ──

    @Test
    fun refresh_givenTransportFailureWithNothingKnown_isUnavailableAndPersistsNothing() = runTest {
        coEvery { api.getMarkets() } throws java.io.IOException("offline")

        val repo = newRepo()
        val result = repo.refresh()

        assertTrue(result is ApiResult.Error)
        assertTrue((result as ApiResult.Error).error is ApiError.Network)
        assertEquals(MarketState.Unavailable, repo.state.value)
        assertEquals(null, repo.state.value.countryId)
        coVerify(exactly = 0) { settings.setMarket(any()) }
    }

    @Test
    fun refresh_givenHttp500_isUnavailableAndPersistsNothing() = runTest {
        coEvery { api.getMarkets() } returns Response.error(500, errorBody())

        val repo = newRepo()
        val result = repo.refresh()

        assertTrue((result as ApiResult.Error).error is ApiError.Server)
        assertEquals(MarketState.Unavailable, repo.state.value)
        coVerify(exactly = 0) { settings.setMarket(any()) }
    }

    @Test
    fun refresh_givenAFailureAfterASuccess_keepsTheLastKnownList() = runTest {
        stored.value = AppSettings(market = "SVK")
        directory(cze, svk)
        val repo = newRepo()
        repo.refresh()

        coEvery { api.getMarkets() } returns Response.error(503, errorBody())
        repo.refresh()

        assertEquals("SVK", (repo.state.value as MarketState.Resolved).selected.isoCode)
        assertEquals(2, (repo.state.value as MarketState.Resolved).markets.size)
    }

    @Test
    fun staleness_isFreshOnlyAfterASuccessfulRead() = runTest {
        coEvery { api.getMarkets() } returns Response.error(500, errorBody())
        val repo = newRepo()
        assertTrue(repo.staleness.isStale())

        repo.refresh()
        assertTrue("a failed read must leave the directory stale so the next entry retries", repo.staleness.isStale())

        directory(cze)
        repo.refresh()
        assertFalse(repo.staleness.isStale())
    }

    // ── ensureLoaded ──

    @Test
    fun ensureLoaded_readsTheDirectoryOnceAndAnswersFromMemoryAfterwards() = runTest {
        directory(cze, svk)

        val repo = newRepo()
        val first = repo.ensureLoaded()
        val second = repo.ensureLoaded()

        assertEquals("CZE", (first as MarketState.Resolved).selected.isoCode)
        assertEquals(first, second)
        coVerify(exactly = 1) { api.getMarkets() }
    }

    @Test
    fun ensureLoaded_afterAFailedRead_doesNotRetryOnItsOwn() = runTest {
        coEvery { api.getMarkets() } returns Response.error(500, errorBody())

        val repo = newRepo()
        repo.ensureLoaded()
        repo.ensureLoaded()

        coVerify(exactly = 1) { api.getMarkets() }
        assertEquals(MarketState.Unavailable, repo.state.value)
    }

    // ── select ──

    @Test
    fun select_persistsTheCodeAndMovesTheSelection() = runTest {
        directory(cze, svk)
        val repo = newRepo()
        repo.refresh()

        repo.select("SVK")

        assertEquals("SVK", (repo.state.value as MarketState.Resolved).selected.isoCode)
        assertEquals("SVK-id", repo.state.value.countryId)
        coVerify(exactly = 1) { settings.setMarket("SVK") }
    }

    @Test
    fun select_ignoresACodeTheDirectoryDoesNotList() = runTest {
        directory(cze, svk)
        val repo = newRepo()
        repo.refresh()

        repo.select("POL")

        assertEquals("CZE", (repo.state.value as MarketState.Resolved).selected.isoCode)
        coVerify(exactly = 0) { settings.setMarket("POL") }
    }

    @Test
    fun select_whileUnavailable_persistsNothing() = runTest {
        val repo = newRepo()

        repo.select("SVK")

        assertEquals(MarketState.Unavailable, repo.state.value)
        coVerify(exactly = 0) { settings.setMarket(any()) }
    }

    // ── the derived readings ──

    @Test
    fun theChipAndSelectorRenderOnlyWithAChoice() = runTest {
        directory(cze)
        val repo = newRepo()
        repo.refresh()
        assertFalse(repo.state.value.offersAChoice)

        directory(cze, svk)
        repo.refresh()
        assertTrue(repo.state.value.offersAChoice)

        assertFalse(MarketState.Unavailable.offersAChoice)
    }

    @Test
    fun theDefaultCurrencyIsTheFlaggedMarketsOwn() = runTest {
        directory(svk, cze)
        val repo = newRepo()
        repo.refresh()

        assertEquals("CZK", repo.state.value.defaultCurrencyCode)
        assertEquals(null, MarketState.Unavailable.defaultCurrencyCode)
    }
}
