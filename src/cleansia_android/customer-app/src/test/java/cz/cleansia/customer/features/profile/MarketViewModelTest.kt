package cz.cleansia.customer.features.profile

import cz.cleansia.customer.core.market.MarketListItem
import cz.cleansia.customer.core.market.MarketRepository
import cz.cleansia.customer.core.market.MarketState
import cz.cleansia.customer.testing.MainDispatcherRule
import io.mockk.coVerify
import io.mockk.every
import io.mockk.mockk
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Before
import org.junit.Rule
import org.junit.Test

@OptIn(ExperimentalCoroutinesApi::class)
class MarketViewModelTest {

    @get:Rule
    val mainRule = MainDispatcherRule()

    private lateinit var marketRepository: MarketRepository
    private val state = MutableStateFlow<MarketState>(MarketState.Unavailable)

    @Before
    fun setUp() {
        marketRepository = mockk(relaxed = true)
        every { marketRepository.state } returns state
    }

    private fun viewModel() = MarketViewModel(marketRepository)

    private fun market(iso: String) = MarketListItem(
        countryId = "$iso-id",
        isoCode = iso,
        isoAlpha2 = iso.take(2),
        name = iso,
        currencyId = "cur",
        currencyCode = "EUR",
        currencySymbol = "€",
        isDefault = false,
    )

    @Test
    fun `the screen mirrors the directory`() = runTest {
        val cze = market("CZE")
        val svk = market("SVK")
        state.value = MarketState.Resolved(listOf(cze, svk), selected = svk)

        assertEquals(MarketState.Resolved(listOf(cze, svk), selected = svk), viewModel().state.value)
    }

    /** ADR-0058 D3: the choice is written to the device store; every reader follows the repository. */
    @Test
    fun `choosing a market persists it through the repository`() = runTest {
        viewModel().select("SVK")
        advanceUntilIdle()

        coVerify(exactly = 1) { marketRepository.select("SVK") }
    }
}
