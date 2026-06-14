package cz.cleansia.partner.features.earnings

import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.partner.api.model.DashboardStatsDto
import cz.cleansia.partner.core.network.ApiError
import cz.cleansia.partner.core.network.ApiErrorTranslator
import cz.cleansia.partner.core.network.ApiResult
import cz.cleansia.partner.features.earnings.viewmodels.EarningsSummaryUiState
import cz.cleansia.partner.features.earnings.viewmodels.EarningsSummaryViewModel
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

    private val stats = mockk<DashboardStatsDto>()

    @Before
    fun setUp() {
        dashboardRepository = mockk()
        snackbar = mockk(relaxed = true)
        errorTranslator = mockk()
        every { errorTranslator.translate(any()) } returns "translated error"
    }

    private fun viewModel() = EarningsSummaryViewModel(dashboardRepository, snackbar, errorTranslator)

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
}
