package cz.cleansia.partner.features.profile

import cz.cleansia.core.network.ApiError
import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.partner.api.model.EmployeeItem
import cz.cleansia.partner.api.model.MyPayoutDetails
import cz.cleansia.partner.core.network.ApiErrorTranslator
import cz.cleansia.partner.data.auth.AuthRepository
import cz.cleansia.partner.data.profile.ProfileRepository
import cz.cleansia.partner.testing.MainDispatcherRule
import io.mockk.coEvery
import io.mockk.coVerify
import io.mockk.every
import io.mockk.mockk
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Before
import org.junit.Rule
import org.junit.Test

/**
 * The bank row on the profile hub used to read `EmployeeItem.iban`, which the payout migration
 * removed. Pins its replacement: the row's summary comes from the payout destination.
 */
@OptIn(ExperimentalCoroutinesApi::class)
class ProfileViewModelTest {

    @get:Rule
    val mainRule = MainDispatcherRule()

    private lateinit var repository: ProfileRepository
    private lateinit var authRepository: AuthRepository
    private lateinit var errorTranslator: ApiErrorTranslator
    private lateinit var snackbar: SnackbarController

    private val employee = mockk<EmployeeItem>(relaxed = true) {
        every { id } returns "emp-1"
    }

    @Before
    fun setUp() {
        repository = mockk()
        authRepository = mockk(relaxed = true)
        errorTranslator = mockk()
        snackbar = mockk(relaxed = true)
        every { errorTranslator.translate(any()) } returns "translated error"
        coEvery { repository.getCurrentEmployee() } returns ApiResult.Success(employee)
        coEvery { repository.getRegistrationStatus() } returns ApiResult.Error(ApiError.Network("down"))
    }

    private fun viewModel() = ProfileViewModel(repository, authRepository, errorTranslator, snackbar)

    @Test
    fun `the bank row summarises the payout destination`() = runTest {
        coEvery { repository.getPayoutDetails() } returns ApiResult.Success(
            MyPayoutDetails(accountNumber = "5885638003", bankCode = "5500"),
        )

        val vm = viewModel()
        advanceUntilIdle()

        assertEquals("5885638003/5500", (vm.uiState.value as ProfileUiState.Loaded).payoutSummary)
    }

    @Test
    fun `no payout destination leaves the row without a summary`() = runTest {
        coEvery { repository.getPayoutDetails() } returns ApiResult.Success(null)

        val vm = viewModel()
        advanceUntilIdle()

        assertNull((vm.uiState.value as ProfileUiState.Loaded).payoutSummary)
    }

    /**
     * The radius row used to cost a second `GetCurrentEmployee` because the generated `EmployeeItem`
     * dropped `jobRadiusKm`. It now rides on the employee the hub already loaded — one read, one
     * source of truth for the value the row summarises.
     */
    @Test
    fun `the radius row rides on the one employee read the hub already makes`() = runTest {
        coEvery { repository.getCurrentEmployee() } returns
            ApiResult.Success(EmployeeItem(id = "emp-1", jobRadiusKm = 40))
        coEvery { repository.getPayoutDetails() } returns ApiResult.Success(null)

        val vm = viewModel()
        advanceUntilIdle()

        assertEquals(40, (vm.uiState.value as ProfileUiState.Loaded).employee.jobRadiusKm)
        coVerify(exactly = 1) { repository.getCurrentEmployee() }
    }
}
