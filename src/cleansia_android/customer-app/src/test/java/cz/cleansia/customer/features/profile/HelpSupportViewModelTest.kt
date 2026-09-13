package cz.cleansia.customer.features.profile

import cz.cleansia.customer.core.market.InsuranceCoverage
import cz.cleansia.customer.core.market.MarketListItem
import cz.cleansia.customer.core.market.MarketRepository
import cz.cleansia.customer.core.market.MarketState
import cz.cleansia.customer.testing.MainDispatcherRule
import io.mockk.every
import io.mockk.mockk
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.test.runCurrent
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Before
import org.junit.Rule
import org.junit.Test

/** ADR-0060 D2: the FAQ states the chosen market's insurance ceiling, or no figure at all. */
@OptIn(ExperimentalCoroutinesApi::class)
class HelpSupportViewModelTest {

    @get:Rule
    val mainRule = MainDispatcherRule()

    private lateinit var marketRepository: MarketRepository
    private val marketState = MutableStateFlow<MarketState>(MarketState.Unavailable)

    @Before
    fun setUp() {
        marketRepository = mockk(relaxed = true)
        every { marketRepository.state } returns marketState
    }

    private fun market(iso: String, currency: String, insurance: Double?) = MarketListItem(
        countryId = "$iso-id",
        isoCode = iso,
        isoAlpha2 = iso.take(2),
        name = iso,
        currencyId = "$currency-id",
        currencyCode = currency,
        currencySymbol = currency,
        isDefault = false,
        insuranceCoverageAmount = insurance,
    )

    @Test
    fun `the chosen market's ceiling is the figure`() = runTest {
        val cze = market("CZE", "CZK", 1_000_000.0)
        val svk = market("SVK", "EUR", 40_000.0)
        marketState.value = MarketState.Resolved(listOf(cze, svk), selected = svk)

        val vm = HelpSupportViewModel(marketRepository)
        runCurrent()

        assertEquals(InsuranceCoverage(40_000.0, "EUR"), vm.insuranceCoverage.value)
    }

    @Test
    fun `no market or no authored ceiling means no figure`() = runTest {
        val vm = HelpSupportViewModel(marketRepository)
        runCurrent()
        assertEquals(null, vm.insuranceCoverage.value)

        val pol = market("POL", "PLN", null)
        marketState.value = MarketState.Resolved(listOf(pol), selected = pol)
        runCurrent()
        assertEquals(null, vm.insuranceCoverage.value)
    }
}
