package cz.cleansia.partner.features.profile

import android.content.Context
import app.cash.turbine.test
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.core.ui.state.ActionState
import cz.cleansia.partner.R
import cz.cleansia.partner.api.model.EmployeeItem
import cz.cleansia.partner.core.network.ApiErrorTranslator
import cz.cleansia.core.network.ApiError
import cz.cleansia.core.network.ApiResult
import cz.cleansia.partner.data.profile.ProfileRepository
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
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test
import org.junit.Rule

@OptIn(ExperimentalCoroutinesApi::class)
class BankSectionViewModelTest {

    @get:Rule
    val mainRule = MainDispatcherRule()

    private lateinit var repository: ProfileRepository
    private lateinit var snackbar: SnackbarController
    private lateinit var errorTranslator: ApiErrorTranslator
    private lateinit var appContext: Context

    private val employee = mockk<EmployeeItem> {
        every { id } returns "emp-1"
        every { iban } returns "CZ6508000000192000145399"
    }

    @Before
    fun setUp() {
        repository = mockk()
        snackbar = mockk(relaxed = true)
        errorTranslator = mockk()
        appContext = mockk(relaxed = true)
        every { errorTranslator.translate(any()) } returns "translated error"
        every { appContext.getString(R.string.error_iban_required) } returns "IBAN is required"
        every { appContext.getString(R.string.error_profile_not_loaded) } returns "Profile not loaded yet"
    }

    private fun viewModel() = BankSectionViewModel(repository, errorTranslator, snackbar, appContext)

    @Test
    fun `load transitions Loading to Loaded with the employee iban`() = runTest {
        coEvery { repository.getCurrentEmployee() } returns ApiResult.Success(employee)

        val vm = viewModel()
        assertEquals(BankSectionUiState.Loading, vm.uiState.value)

        advanceUntilIdle()
        val loaded = vm.uiState.value as BankSectionUiState.Loaded
        assertEquals("emp-1", loaded.form.employeeId)
        assertEquals("CZ6508000000192000145399", loaded.form.iban)
    }

    @Test
    fun `load failure transitions to Error and snackbars`() = runTest {
        coEvery { repository.getCurrentEmployee() } returns ApiResult.Error(ApiError.Network("down"))

        val vm = viewModel()
        advanceUntilIdle()

        assertEquals(BankSectionUiState.Error, vm.uiState.value)
        verify { snackbar.showError("translated error") }
    }

    @Test
    fun `blank iban surfaces a localized field error and does not call the repo`() = runTest {
        coEvery { repository.getCurrentEmployee() } returns ApiResult.Success(employee)
        val vm = viewModel()
        advanceUntilIdle()

        vm.onIbanChange("")
        vm.save()
        advanceUntilIdle()

        val loaded = vm.uiState.value as BankSectionUiState.Loaded
        assertEquals("IBAN is required", loaded.form.ibanError)
        coVerify(exactly = 0) { repository.updateBankDetails(any(), any()) }
    }

    @Test
    fun `save success emits the saved effect and returns to Idle`() = runTest {
        coEvery { repository.getCurrentEmployee() } returns ApiResult.Success(employee)
        coEvery { repository.updateBankDetails("emp-1", any()) } returns ApiResult.Success(Unit)

        val vm = viewModel()
        advanceUntilIdle()

        vm.saved.test {
            vm.save()
            advanceUntilIdle()
            awaitItem()
        }
        assertEquals(ActionState.Idle, vm.saveState.value)
    }

    @Test
    fun `save failure snackbars and returns to Idle`() = runTest {
        coEvery { repository.getCurrentEmployee() } returns ApiResult.Success(employee)
        coEvery { repository.updateBankDetails("emp-1", any()) } returns
            ApiResult.Error(ApiError.Network("down"))

        val vm = viewModel()
        advanceUntilIdle()

        vm.save()
        advanceUntilIdle()

        assertTrue(vm.saveState.value is ActionState.Idle)
        verify { snackbar.showError("translated error") }
    }
}
