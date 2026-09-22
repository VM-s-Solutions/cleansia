package cz.cleansia.customer.features.booking

import cz.cleansia.customer.core.catalog.CatalogRepository
import cz.cleansia.customer.core.market.InsuranceCoverage
import cz.cleansia.customer.core.market.MarketListItem
import cz.cleansia.customer.core.market.MarketRepository
import cz.cleansia.customer.core.market.MarketState
import cz.cleansia.customer.core.memberships.MembershipRepository
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

/**
 * ADR-0060 D2 with ADR-0058 D4: the insurance ceiling on the confirm step is the booking's own
 * country's when that country is a listed market (the address wins), else the chosen market's, and
 * absent when the market states none — never a figure in a guessed unit.
 */
@OptIn(ExperimentalCoroutinesApi::class)
class ConfirmStepViewModelTest {

    @get:Rule
    val mainRule = MainDispatcherRule()

    private lateinit var catalogRepository: CatalogRepository
    private lateinit var membershipRepository: MembershipRepository
    private lateinit var marketRepository: MarketRepository
    private val bookingCountry = MutableStateFlow<String?>(null)
    private val marketState = MutableStateFlow<MarketState>(MarketState.Unavailable)

    @Before
    fun setUp() {
        catalogRepository = mockk(relaxed = true)
        membershipRepository = mockk(relaxed = true)
        marketRepository = mockk(relaxed = true)
        every { catalogRepository.countryId } returns bookingCountry
        every { marketRepository.state } returns marketState
    }

    private fun viewModel() = ConfirmStepViewModel(catalogRepository, membershipRepository, marketRepository)

    private fun market(iso: String, currency: String, insurance: Double?, isDefault: Boolean = false) = MarketListItem(
        countryId = "$iso-id",
        isoCode = iso,
        isoAlpha2 = iso.take(2),
        name = iso,
        currencyId = "$currency-id",
        currencyCode = currency,
        currencySymbol = currency,
        isDefault = isDefault,
        insuranceCoverageAmount = insurance,
    )

    private val cze = market("CZE", "CZK", insurance = 1_000_000.0, isDefault = true)
    private val svk = market("SVK", "EUR", insurance = 40_000.0)
    private val pol = market("POL", "PLN", insurance = null)

    @Test
    fun `with no market there is no figure`() = runTest {
        val vm = viewModel()
        runCurrent()

        assertEquals(null, vm.insuranceCoverage.value)
    }

    @Test
    fun `the booking's country wins when it is a listed market`() = runTest {
        marketState.value = MarketState.Resolved(listOf(cze, svk), selected = svk)
        bookingCountry.value = "CZE-id"
        val vm = viewModel()
        runCurrent()

        assertEquals(InsuranceCoverage(1_000_000.0, "CZK"), vm.insuranceCoverage.value)
    }

    @Test
    fun `the chosen market answers when the booking's country is not listed`() = runTest {
        marketState.value = MarketState.Resolved(listOf(cze, svk), selected = svk)
        bookingCountry.value = null
        val vm = viewModel()
        runCurrent()

        assertEquals(InsuranceCoverage(40_000.0, "EUR"), vm.insuranceCoverage.value)
    }

    @Test
    fun `a market with no ceiling authored yields no figure`() = runTest {
        marketState.value = MarketState.Resolved(listOf(cze, pol), selected = pol)
        bookingCountry.value = "POL-id"
        val vm = viewModel()
        runCurrent()

        assertEquals(null, vm.insuranceCoverage.value)
    }

    @Test
    fun `the figure follows the address as the wizard moves`() = runTest {
        marketState.value = MarketState.Resolved(listOf(cze, svk), selected = cze)
        val vm = viewModel()
        runCurrent()
        assertEquals(InsuranceCoverage(1_000_000.0, "CZK"), vm.insuranceCoverage.value)

        bookingCountry.value = "SVK-id"
        runCurrent()

        assertEquals(InsuranceCoverage(40_000.0, "EUR"), vm.insuranceCoverage.value)
    }
}
