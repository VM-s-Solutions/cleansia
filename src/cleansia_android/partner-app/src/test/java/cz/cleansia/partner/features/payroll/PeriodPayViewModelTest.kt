package cz.cleansia.partner.features.payroll

import androidx.lifecycle.SavedStateHandle
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.partner.core.auth.UserProfileData
import cz.cleansia.partner.core.auth.UserProfileStore
import cz.cleansia.partner.core.network.ApiError
import cz.cleansia.partner.core.network.ApiErrorTranslator
import cz.cleansia.partner.core.network.ApiResult
import cz.cleansia.partner.data.payroll.PeriodPayRepository
import cz.cleansia.partner.data.payroll.PeriodPaySummary
import cz.cleansia.partner.testing.MainDispatcherRule
import io.mockk.coEvery
import io.mockk.coVerify
import io.mockk.every
import io.mockk.mockk
import io.mockk.verify
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Before
import org.junit.Rule
import org.junit.Test

@OptIn(ExperimentalCoroutinesApi::class)
class PeriodPayViewModelTest {

    @get:Rule
    val mainRule = MainDispatcherRule()

    private lateinit var repository: PeriodPayRepository
    private lateinit var userProfileStore: UserProfileStore
    private lateinit var errorTranslator: ApiErrorTranslator
    private lateinit var snackbar: SnackbarController

    private val profile = UserProfileData(
        userId = "user-1",
        email = "cleaner@cleansia.cz",
        employeeId = "emp-1",
        isEmailConfirmed = true,
        hasAdminAccess = false,
        firstName = "Jana",
        lastName = "Nováková",
        role = "Employee",
    )

    private val summary = PeriodPaySummary(
        payPeriodId = "pp-1",
        payPeriodLabel = "1 – 15 Jun 2026",
        employeeId = "emp-1",
        totalOrders = 3,
        grandTotal = 4200.0,
    )

    @Before
    fun setUp() {
        repository = mockk()
        userProfileStore = mockk()
        errorTranslator = mockk()
        snackbar = mockk(relaxed = true)
        coEvery { userProfileStore.current() } returns profile
        every { errorTranslator.translate(any()) } returns "translated error"
    }

    private fun viewModel(payPeriodId: String = "pp-1") = PeriodPayViewModel(
        savedStateHandle = SavedStateHandle(mapOf("payPeriodId" to payPeriodId)),
        periodPayRepository = repository,
        userProfileStore = userProfileStore,
        errorTranslator = errorTranslator,
        snackbar = snackbar,
    )

    @Test
    fun `load resolves own employeeId from the profile store and goes Loading to Loaded`() = runTest {
        coEvery { repository.getPeriodPays("emp-1", "pp-1") } returns ApiResult.Success(summary)

        val vm = viewModel()
        assertEquals(PeriodPayUiState.Loading, vm.state.value)

        advanceUntilIdle()
        assertEquals(PeriodPayUiState.Loaded(summary), vm.state.value)
        coVerify(exactly = 1) { repository.getPeriodPays("emp-1", "pp-1") }
    }

    @Test
    fun `missing employeeId never hits the network and lands in Error`() = runTest {
        coEvery { userProfileStore.current() } returns profile.copy(employeeId = null)

        val vm = viewModel()
        advanceUntilIdle()

        assertEquals(PeriodPayUiState.Error, vm.state.value)
        coVerify(exactly = 0) { repository.getPeriodPays(any(), any()) }
    }

    @Test
    fun `api error snackbars the translated message and lands in Error`() = runTest {
        coEvery { repository.getPeriodPays("emp-1", "pp-1") } returns
            ApiResult.Error(ApiError.Server(statusCode = 500, message = "boom"))

        val vm = viewModel()
        advanceUntilIdle()

        assertEquals(PeriodPayUiState.Error, vm.state.value)
        verify { snackbar.showError("translated error") }
    }
}
