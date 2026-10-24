package cz.cleansia.partner.features.profile

import android.content.Context
import app.cash.turbine.test
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.core.ui.state.ActionState
import cz.cleansia.partner.R
import cz.cleansia.partner.api.model.EmployeeItem
import cz.cleansia.partner.core.network.ApiErrorTranslator
import cz.cleansia.partner.data.user.CurrentUser
import cz.cleansia.partner.data.user.UserRepository
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
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Rule
import org.junit.Test
import java.time.LocalDate
import java.time.ZoneOffset

@OptIn(ExperimentalCoroutinesApi::class)
class PersonalSectionViewModelTest {

    @get:Rule
    val mainRule = MainDispatcherRule()

    private lateinit var repository: ProfileRepository
    private lateinit var snackbar: SnackbarController
    private lateinit var errorTranslator: ApiErrorTranslator
    private lateinit var userRepository: UserRepository
    private lateinit var appContext: Context

    private val employee = EmployeeItem(
        id = "emp-1",
        email = "jana@example.com",
        firstName = "Jana",
        lastName = "Nováková",
        phoneNumber = "+420777123456",
        birthDate = "1991-01-01",
    )

    private val employeeWithoutBirthDate = employee.copy(birthDate = null)

    @Before
    fun setUp() {
        repository = mockk()
        snackbar = mockk(relaxed = true)
        errorTranslator = mockk()
        userRepository = mockk()
        // The photo lives on the user row; the form reads it alongside the employee record. These
        // cases are about the fields, so the cleaner simply has no photo and the avatar shows initials.
        coEvery { userRepository.getCurrentUser() } returns ApiResult.Success(
            CurrentUser(
                email = "ondrej@example.com",
                firstName = "Ondrej",
                lastName = "Novak",
                phoneNumber = null,
                birthDate = null,
                preferredLanguageCode = "en",
                profilePhotoUrl = null,
            ),
        )
        appContext = mockk(relaxed = true)
        every { errorTranslator.translate(any()) } returns "translated error"
        every { appContext.getString(R.string.error_birth_date_required) } returns "Date of birth is required"
        every { appContext.getString(R.string.error_profile_not_loaded) } returns "Profile not loaded yet"
        every { appContext.getString(R.string.error_first_name_required) } returns "First name is required"
        every { appContext.getString(R.string.error_last_name_required) } returns "Last name is required"
        every { appContext.getString(R.string.error_phone_number_required) } returns "Phone number is required"
        every { appContext.getString(R.string.error_phone_number_invalid) } returns "Enter the number with its country code"
        every { appContext.getString(R.string.validation_max_50) } returns "Maximum 50 characters allowed"
        every { appContext.getString(R.string.validation_min_age_18) } returns "You must be at least 18 years old"
        every { appContext.getString(R.string.validation_max_age_120) } returns "Invalid date of birth"
    }

    private fun viewModel() =
        PersonalSectionViewModel(repository, userRepository, errorTranslator, snackbar, appContext)

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
            repository.updatePersonalInfo(any(), any(), any(), any(), any())
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

    private fun PersonalSectionViewModel.form(): PersonalForm = (uiState.value as PersonalSectionUiState.Loaded).form

    @Test
    fun `blank names are named under their fields and nothing is sent`() = runTest {
        coEvery { repository.getCurrentEmployee() } returns
            ApiResult.Success(employee.copy(firstName = " ", lastName = null))
        val vm = viewModel()
        advanceUntilIdle()

        vm.save()
        advanceUntilIdle()

        assertEquals("First name is required", vm.form().firstNameError)
        assertEquals("Last name is required", vm.form().lastNameError)
        coVerify(exactly = 0) { repository.updatePersonalInfo(any(), any(), any(), any(), any()) }
        verify(exactly = 0) { snackbar.showError(any<String>()) }
    }

    @Test
    fun `missing phone surfaces a field error and does not call the repo`() = runTest {
        coEvery { repository.getCurrentEmployee() } returns ApiResult.Success(employee.copy(phoneNumber = null))
        val vm = viewModel()
        advanceUntilIdle()

        vm.save()
        advanceUntilIdle()

        assertEquals("Phone number is required", vm.form().phoneError)
        assertNull(vm.form().firstNameError)
        assertEquals(ActionState.Idle, vm.saveState.value)
        coVerify(exactly = 0) { repository.updatePersonalInfo(any(), any(), any(), any(), any()) }
        verify(exactly = 0) { snackbar.showError(any<String>()) }
    }

    @Test
    fun `a phone without its country code, or one libphonenumber refuses, is named and not sent`() = runTest {
        listOf("+1", "777123456", "+420123456789").forEach { phone ->
            coEvery { repository.getCurrentEmployee() } returns ApiResult.Success(employee.copy(phoneNumber = phone))
            val vm = viewModel()
            advanceUntilIdle()

            vm.save()
            advanceUntilIdle()

            assertEquals(phone, "Enter the number with its country code", vm.form().phoneError)
        }
        coVerify(exactly = 0) { repository.updatePersonalInfo(any(), any(), any(), any(), any()) }
        verify(exactly = 0) { snackbar.showError(any<String>()) }
    }

    @Test
    fun `a stored phone with spaces is valid and is sent as stored`() = runTest {
        coEvery { repository.getCurrentEmployee() } returns
            ApiResult.Success(employee.copy(phoneNumber = "+420 777123456"))
        coEvery { repository.updatePersonalInfo(any(), any(), any(), any(), any()) } returns ApiResult.Success(Unit)
        val vm = viewModel()
        advanceUntilIdle()

        vm.save()
        advanceUntilIdle()

        assertNull(vm.form().phoneError)
        coVerify(exactly = 1) {
            repository.updatePersonalInfo(any(), any(), any(), any(), phone = "+420 777123456")
        }
    }

    /** The server's own bounds (`UpdatePersonalInfo`): today minus 120 years to today minus 18, on UTC. */
    @Test
    fun `a birth date is refused under 18 and over 120 years, the server's bounds`() {
        val today = LocalDate.of(2026, 10, 24)
        assertEquals(R.string.error_birth_date_required, birthDateProblem(" ", today))
        assertEquals(R.string.validation_min_age_18, birthDateProblem("2008-10-25", today))
        assertNull("eighteen today", birthDateProblem("2008-10-24", today))
        assertNull("a hundred and twenty today", birthDateProblem("1906-10-24", today))
        assertEquals(R.string.validation_max_age_120, birthDateProblem("1906-10-23", today))
        assertEquals("a leap-day birthday turns 18 on 1 March", R.string.validation_min_age_18, birthDateProblem("2008-02-29", LocalDate.of(2026, 2, 28)))
        assertNull("not a date the picker writes: the server decides", birthDateProblem("29/02/2008", today))
    }

    @Test
    fun `an under-age or implausible birth date is named under the field and nothing is sent`() = runTest {
        val today = LocalDate.now(ZoneOffset.UTC)
        mapOf(
            today.minusYears(17) to "You must be at least 18 years old",
            today.minusYears(121) to "Invalid date of birth",
        ).forEach { (birthDate, message) ->
            coEvery { repository.getCurrentEmployee() } returns
                ApiResult.Success(employee.copy(birthDate = birthDate.toString()))
            val vm = viewModel()
            advanceUntilIdle()

            vm.save()
            advanceUntilIdle()

            assertEquals(message, vm.form().birthDateError)
        }
        coVerify(exactly = 0) { repository.updatePersonalInfo(any(), any(), any(), any(), any()) }
        verify(exactly = 0) { snackbar.showError(any<String>()) }
    }

    @Test
    fun `a name over 50 characters is named under its field and nothing is sent`() = runTest {
        coEvery { repository.getCurrentEmployee() } returns
            ApiResult.Success(employee.copy(firstName = "a".repeat(51), lastName = " ${"b".repeat(51)} "))
        val vm = viewModel()
        advanceUntilIdle()

        vm.save()
        advanceUntilIdle()

        assertEquals("Maximum 50 characters allowed", vm.form().firstNameError)
        assertEquals("Maximum 50 characters allowed", vm.form().lastNameError)
        coVerify(exactly = 0) { repository.updatePersonalInfo(any(), any(), any(), any(), any()) }

        vm.onFirstNameChange("a".repeat(50))
        assertNull("the error clears as the name is edited", vm.form().firstNameError)
    }

    @Test
    fun `names of exactly 50 characters, counted without the spaces around them, are sent`() = runTest {
        coEvery { repository.getCurrentEmployee() } returns
            ApiResult.Success(employee.copy(firstName = " ${"a".repeat(50)} ", lastName = "b".repeat(50)))
        coEvery { repository.updatePersonalInfo(any(), any(), any(), any(), any()) } returns ApiResult.Success(Unit)
        val vm = viewModel()
        advanceUntilIdle()

        vm.save()
        advanceUntilIdle()

        coVerify(exactly = 1) {
            repository.updatePersonalInfo(any(), firstName = "a".repeat(50), lastName = "b".repeat(50), any(), any())
        }
    }

    @Test
    fun `typing a phone clears its error`() = runTest {
        coEvery { repository.getCurrentEmployee() } returns ApiResult.Success(employee.copy(phoneNumber = " "))
        val vm = viewModel()
        advanceUntilIdle()
        vm.save()
        advanceUntilIdle()
        assertNotNull(vm.form().phoneError)

        vm.onPhoneChange("+420777123456")

        assertNull(vm.form().phoneError)
        assertEquals("+420777123456", vm.form().phone)
    }

    @Test
    fun `save success sends the birth date and emits the saved effect`() = runTest {
        coEvery { repository.getCurrentEmployee() } returns ApiResult.Success(employee)
        coEvery {
            repository.updatePersonalInfo(any(), any(), any(), any(), any())
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
                phone = "+420777123456",
            )
        }
    }

    @Test
    fun `save failure snackbars and returns to Idle`() = runTest {
        coEvery { repository.getCurrentEmployee() } returns ApiResult.Success(employee)
        coEvery {
            repository.updatePersonalInfo(any(), any(), any(), any(), any())
        } returns ApiResult.Error(ApiError.Network("down"))

        val vm = viewModel()
        advanceUntilIdle()

        vm.save()
        advanceUntilIdle()

        assertTrue(vm.saveState.value is ActionState.Idle)
        verify { snackbar.showError("translated error") }
    }
}
