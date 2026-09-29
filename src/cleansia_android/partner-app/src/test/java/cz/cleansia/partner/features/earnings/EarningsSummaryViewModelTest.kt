package cz.cleansia.partner.features.earnings

import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.partner.data.dashboard.dashboardStats
import cz.cleansia.core.network.ApiError
import cz.cleansia.partner.core.network.ApiErrorTranslator
import cz.cleansia.core.network.ApiResult
import cz.cleansia.partner.data.payroll.CashHeld
import cz.cleansia.partner.data.payroll.PeriodPayRepository
import cz.cleansia.partner.features.earnings.EarningsSummaryUiState
import cz.cleansia.partner.features.earnings.EarningsSummaryViewModel
import cz.cleansia.partner.testing.MainDispatcherRule
import io.mockk.coEvery
import io.mockk.every
import io.mockk.mockk
import io.mockk.verify
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Rule
import org.junit.Test

@OptIn(ExperimentalCoroutinesApi::class)
class EarningsSummaryViewModelTest {

    @get:Rule
    val mainRule = MainDispatcherRule()

    private lateinit var dashboardRepository: cz.cleansia.partner.data.dashboard.DashboardRepository
    private lateinit var snackbar: SnackbarController
    private lateinit var errorTranslator: ApiErrorTranslator
    private lateinit var periodPayRepository: PeriodPayRepository

    private val stats = dashboardStats()

    private val held = listOf(CashHeld(currencyCode = "CZK", amount = 3250.5, floatCap = 3000.0, cashJobsHidden = true))

    @Before
    fun setUp() {
        dashboardRepository = mockk()
        snackbar = mockk(relaxed = true)
        errorTranslator = mockk()
        every { errorTranslator.translate(any()) } returns "translated error"
        periodPayRepository = mockk()
        coEvery { periodPayRepository.getCashHeld() } returns ApiResult.Success(emptyList())
    }

    private fun viewModel() =
        EarningsSummaryViewModel(dashboardRepository, periodPayRepository, snackbar, errorTranslator)

    @Test
    fun `init loads stats transitioning Loading to Loaded`() = runTest {
        coEvery { dashboardRepository.getStats(employeeId = null) } returns ApiResult.Success(stats)

        val vm = viewModel()
        assertEquals(EarningsSummaryUiState.Loading, vm.uiState.value)

        advanceUntilIdle()
        assertEquals(EarningsSummaryUiState.Loaded(stats), vm.uiState.value)
    }

    @Test
    fun `load failure transitions to Error and snackbars`() = runTest {
        coEvery { dashboardRepository.getStats(employeeId = null) } returns ApiResult.Error(ApiError.Network("down"))

        val vm = viewModel()
        advanceUntilIdle()

        assertTrue(vm.uiState.value is EarningsSummaryUiState.Error)
        verify { snackbar.showError("translated error") }
    }

    @Test
    fun `refresh after error reloads to Loaded`() = runTest {
        coEvery { dashboardRepository.getStats(employeeId = null) } returnsMany listOf(
            ApiResult.Error(ApiError.Network("down")),
            ApiResult.Success(stats),
        )

        val vm = viewModel()
        advanceUntilIdle()
        assertTrue(vm.uiState.value is EarningsSummaryUiState.Error)

        vm.refresh()
        advanceUntilIdle()
        assertEquals(EarningsSummaryUiState.Loaded(stats), vm.uiState.value)
    }

    @Test
    fun `init reads the cash the cleaner holds beside the stats`() = runTest {
        coEvery { dashboardRepository.getStats(employeeId = null) } returns ApiResult.Success(stats)
        coEvery { periodPayRepository.getCashHeld() } returns ApiResult.Success(held)

        val vm = viewModel()
        advanceUntilIdle()

        assertEquals(held, vm.cashHeld.value)
        assertEquals(EarningsSummaryUiState.Loaded(stats), vm.uiState.value)
    }

    @Test
    fun `a failed cash read beside loaded stats is raised once and shows no cash`() = runTest {
        coEvery { dashboardRepository.getStats(employeeId = null) } returns ApiResult.Success(stats)
        coEvery { periodPayRepository.getCashHeld() } returns ApiResult.Error(ApiError.Network("down"))

        val vm = viewModel()
        advanceUntilIdle()

        assertEquals(emptyList<CashHeld>(), vm.cashHeld.value)
        assertEquals(EarningsSummaryUiState.Loaded(stats), vm.uiState.value)
        verify(exactly = 1) { snackbar.showError("translated error") }
    }

    @Test
    fun `when both reads fail the cleaner is told once`() = runTest {
        coEvery { dashboardRepository.getStats(employeeId = null) } returns ApiResult.Error(ApiError.Network("down"))
        coEvery { periodPayRepository.getCashHeld() } returns ApiResult.Error(ApiError.Network("down"))

        val vm = viewModel()
        advanceUntilIdle()

        assertTrue(vm.uiState.value is EarningsSummaryUiState.Error)
        verify(exactly = 1) { snackbar.showError("translated error") }
    }
}
