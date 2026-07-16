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
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Rule
import org.junit.Test

@OptIn(ExperimentalCoroutinesApi::class)
class PersonalSectionViewModelTest {

    @get:Rule
    val mainRule = MainDispatcherRule()

    private lateinit var repository: ProfileRepository
    private lateinit var snackbar: SnackbarController
    private lateinit var errorTranslator: ApiErrorTranslator
    private lateinit var appContext: Context

    private val employee = EmployeeItem(
        id = "emp-1",
        email = "jana@example.com",
        firstName = "Jana",
        lastName = "Nováková",
        phoneNumber = "+420123456789",
        birthDate = "1991-01-01",
    )

    private val employeeWithoutBirthDate = employee.copy(birthDate = null)

    @Before
    fun setUp() {
        repository = mockk()
        snackbar = mockk(relaxed = true)
        errorTranslator = mockk()
        appContext = mockk(relaxed = true)
        every { errorTranslator.translate(any()) } returns "translated error"
        every { appContext.getString(R.string.error_birth_date_required) } returns "Date of birth is required"
        every { appContext.getString(R.string.error_profile_not_loaded) } returns "Profile not loaded yet"
    }

    private fun viewModel() = PersonalSectionViewModel(repository, errorTranslator, snackbar, appContext)

    @Test
    fun `load transitions Loading to Loaded with the employee fields`() = runTest {
        coEvery { repository.getCurrentEmployee() } returns ApiResult.Success(employee)

        val vm = viewModel()
        assertEquals(PersonalSectionUiState.Loading, vm.uiState.value)

        advanceUntilIdle()
        val loaded = vm.uiState.value as PersonalSectionUiState.Loaded
        assertEquals("emp-1", loaded.form.employeeId)
        assertEquals("Jana", loaded.form.firstName)
        assertEquals("Nováková", loaded.form.lastName)
        assertEquals("1991-01-01", loaded.form.birthDate)
    }

    @Test
    fun `load failure transitions to Error and snackbars`() = runTest {
        coEvery { repository.getCurrentEmployee() } returns ApiResult.Error(ApiError.Network("down"))

        val vm = viewModel()
        advanceUntilIdle()

        assertEquals(PersonalSectionUiState.Error, vm.uiState.value)
        verify { snackbar.showError("translated error") }
    }

    @Test
    fun `missing birth date surfaces a localized field error and does not call the repo`() = runTest {
        coEvery { repository.getCurrentEmployee() } returns ApiResult.Success(employeeWithoutBirthDate)
        val vm = viewModel()
        advanceUntilIdle()

        vm.save()
        advanceUntilIdle()

        val loaded = vm.uiState.value as PersonalSectionUiState.Loaded
        assertEquals("Date of birth is required", loaded.form.birthDateError)
        assertNull(loaded.form.firstNameError)
        assertNull(loaded.form.lastNameError)
        assertEquals(ActionState.Idle, vm.saveState.value)
        coVerify(exactly = 0) {
            repository.updatePersonalInfo(any(), any(), any(), any(), any(), any())
        }
    }

    @Test
    fun `picking a birth date clears the field error`() = runTest {
        coEvery { repository.getCurrentEmployee() } returns ApiResult.Success(employeeWithoutBirthDate)
        val vm = viewModel()
        advanceUntilIdle()

        vm.save()
        advanceUntilIdle()
        vm.onBirthDateChange("1991-01-01")

        val loaded = vm.uiState.value as PersonalSectionUiState.Loaded
        assertNull(loaded.form.birthDateError)
        assertEquals("1991-01-01", loaded.form.birthDate)
    }

    @Test
    fun `save success sends the birth date and emits the saved effect`() = runTest {
        coEvery { repository.getCurrentEmployee() } returns ApiResult.Success(employee)
        coEvery {
            repository.updatePersonalInfo(any(), any(), any(), any(), any(), any())
        } returns ApiResult.Success(Unit)

        val vm = viewModel()
        advanceUntilIdle()

        vm.saved.test {
            vm.save()
            advanceUntilIdle()
            awaitItem()
        }
        assertEquals(ActionState.Idle, vm.saveState.value)
        coVerify(exactly = 1) {
            repository.updatePersonalInfo(
                employeeId = "emp-1",
                firstName = "Jana",
                lastName = "Nováková",
                birthDate = "1991-01-01",
                phone = "+420123456789",
                email = "jana@example.com",
            )
        }
    }

    @Test
    fun `save failure snackbars and returns to Idle`() = runTest {
        coEvery { repository.getCurrentEmployee() } returns ApiResult.Success(employee)
        coEvery {
            repository.updatePersonalInfo(any(), any(), any(), any(), any(), any())
        } returns ApiResult.Error(ApiError.Network("down"))

        val vm = viewModel()
        advanceUntilIdle()

        vm.save()
        advanceUntilIdle()

        assertTrue(vm.saveState.value is ActionState.Idle)
        verify { snackbar.showError("translated error") }
    }
}
