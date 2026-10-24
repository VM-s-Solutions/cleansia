package cz.cleansia.partner.features.profile

import android.content.Context
import androidx.lifecycle.viewModelScope
import app.cash.turbine.ReceiveTurbine
import app.cash.turbine.test
import cz.cleansia.core.network.ApiError
import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.core.ui.state.ActionState
import cz.cleansia.partner.R
import cz.cleansia.partner.api.client.CountryApi
import cz.cleansia.partner.api.model.EmployeeEntityType
import cz.cleansia.partner.api.model.EmployeeItem
import cz.cleansia.partner.core.network.ApiErrorTranslator
import cz.cleansia.partner.data.profile.ProfileRepository
import cz.cleansia.partner.testing.MainDispatcherRule
import io.mockk.coEvery
import io.mockk.coVerify
import io.mockk.every
import io.mockk.mockk
import io.mockk.verify
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.job
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.runTest
import kotlinx.serialization.json.Json
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.ResponseBody.Companion.toResponseBody
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Rule
import org.junit.Test
import retrofit2.Response

/**
 * A cleaner contracts as a natural person, so this form offers no entity-type choice and no legal
 * name to type. What an operator set by hand is still shown: the stored legal name of a
 * legal-entity row rides the form as a read-only value, never as something the save could carry.
 */
@OptIn(ExperimentalCoroutinesApi::class)
class IdentificationSectionViewModelTest {

    @get:Rule
    val mainRule = MainDispatcherRule()

    private lateinit var repository: ProfileRepository
    private lateinit var countryApi: CountryApi
    private lateinit var snackbar: SnackbarController
    private lateinit var errorTranslator: ApiErrorTranslator
    private lateinit var appContext: Context
    private val json = Json { ignoreUnknownKeys = true; isLenient = true }

    private val naturalPerson = EmployeeItem(
        id = "emp-1",
        countryId = "country-cz",
        nationalityId = "country-cz",
        passportId = "AB1234567",
        entityType = EmployeeEntityType._1,
        registrationNumber = "12345678",
    )

    private val operatorOnboardedCompany = naturalPerson.copy(
        entityType = EmployeeEntityType._2,
        legalEntityName = "Uklid Praha s.r.o.",
    )

    @Before
    fun setUp() {
        repository = mockk()
        countryApi = mockk()
        snackbar = mockk(relaxed = true)
        errorTranslator = mockk()
        appContext = mockk(relaxed = true)
        every { errorTranslator.translate(any()) } returns "translated error"
        every { appContext.getString(R.string.error_pick_nationality) } returns "Pick your nationality"
        every { appContext.getString(R.string.error_passport_required) } returns "Enter your passport / ID number"
        every { appContext.getString(R.string.error_pick_business_country) } returns "Pick the business country"
        every { appContext.getString(R.string.error_registration_number_required) } returns "Enter it"
        every { appContext.getString(R.string.error_validation_passport_id_invalid_length) } returns "5 to 20 characters"
        every { appContext.getString(R.string.error_legal_entity_name_missing) } returns "Contact support"
        coEvery { countryApi.countryGetOverview() } returns Response.success(emptyList())
        coEvery { countryApi.countryGetFieldLabels(any()) } returns Response.error(
            404,
            "".toResponseBody("application/json".toMediaType()),
        )
    }

    private fun viewModel() =
        IdentificationSectionViewModel(repository, countryApi, errorTranslator, snackbar, json, appContext)

    // The country read runs through safeApiCall on the IO dispatcher, which the test scheduler
    // cannot advance, so the load is awaited rather than stepped — the address section's idiom.
    private suspend fun ReceiveTurbine<IdentificationSectionUiState>.awaitLoaded(): IdentificationForm {
        while (true) {
            val state = awaitItem()
            if (state is IdentificationSectionUiState.Loaded) return state.form
        }
    }

    private suspend fun IdentificationSectionViewModel.awaitLoaded(): IdentificationForm {
        uiState.test {
            awaitLoaded()
            cancelAndIgnoreRemainingEvents()
        }
        // Loaded is published before the field-labels read, which hops to Dispatchers.IO and
        // resumes on Main after the rule has reset it — so every child the load launched is
        // joined here, or the next test in the JVM inherits its uncaught exception.
        viewModelScope.coroutineContext.job.children.forEach { it.join() }
        return (uiState.value as IdentificationSectionUiState.Loaded).form
    }

    @Test
    fun `an operator-onboarded company shows its stored legal name`() = runTest {
        coEvery { repository.getCurrentEmployee() } returns ApiResult.Success(operatorOnboardedCompany)

        val form = viewModel().awaitLoaded()

        assertTrue(form.isLegalEntity)
        assertEquals("Uklid Praha s.r.o.", form.storedLegalEntityName)
        assertNull(form.legalEntityNameError)
    }

    /**
     * The registration lock needs a company's name, which only an operator can set, so a company row
     * without one keeps the cleaner on this step: the locked field says why instead of staying hidden.
     */
    @Test
    fun `an operator-onboarded company without a name says why the step cannot finish`() = runTest {
        listOf(null, " ").forEach { name ->
            coEvery { repository.getCurrentEmployee() } returns
                ApiResult.Success(operatorOnboardedCompany.copy(legalEntityName = name))

            val form = viewModel().awaitLoaded()

            assertTrue(form.isLegalEntity)
            assertEquals("Contact support", form.legalEntityNameError)
        }
    }

    @Test
    fun `a natural person shows no legal name, even if the payload carries one`() = runTest {
        coEvery { repository.getCurrentEmployee() } returns
            ApiResult.Success(naturalPerson.copy(legalEntityName = "stale"))

        val form = viewModel().awaitLoaded()

        assertFalse(form.isLegalEntity)
        assertNull(form.storedLegalEntityName)
        assertNull(form.legalEntityNameError)
    }

    @Test
    fun `save sends the person and business fields and nothing about the entity`() = runTest {
        coEvery { repository.getCurrentEmployee() } returns ApiResult.Success(operatorOnboardedCompany)
        coEvery {
            repository.updateIdentification(any(), any(), any(), any(), any())
        } returns ApiResult.Success(Unit)

        val vm = viewModel()
        vm.awaitLoaded()

        vm.saved.test {
            vm.save()
            advanceUntilIdle()
            awaitItem()
        }
        assertEquals(ActionState.Idle, vm.saveState.value)
        coVerify(exactly = 1) {
            repository.updateIdentification(
                employeeId = "emp-1",
                nationalityId = "country-cz",
                passportId = "AB1234567",
                businessCountryId = "country-cz",
                registrationNumber = "12345678",
            )
        }
    }

    private fun IdentificationSectionViewModel.form(): IdentificationForm =
        (uiState.value as IdentificationSectionUiState.Loaded).form

    /** Waits out the field-labels read a business-country pick starts, which hops to Dispatchers.IO. */
    private suspend fun IdentificationSectionViewModel.settle() =
        viewModelScope.coroutineContext.job.children.forEach { it.join() }

    @Test
    fun `a missing registration number is named under its field and the repo is not called`() = runTest {
        coEvery { repository.getCurrentEmployee() } returns
            ApiResult.Success(naturalPerson.copy(registrationNumber = null))

        val vm = viewModel()
        vm.awaitLoaded()

        vm.save()
        advanceUntilIdle()

        assertEquals("Enter it", vm.form().registrationNumberError)
        verify(exactly = 0) { snackbar.showError(any<String>()) }
        coVerify(exactly = 0) { repository.updateIdentification(any(), any(), any(), any(), any()) }
    }

    @Test
    fun `every missing field is named under it in one pass and nothing is sent`() = runTest {
        coEvery { repository.getCurrentEmployee() } returns ApiResult.Success(
            naturalPerson.copy(nationalityId = null, passportId = " ", countryId = null, registrationNumber = null),
        )

        val vm = viewModel()
        vm.awaitLoaded()

        vm.save()
        advanceUntilIdle()

        val form = vm.form()
        assertEquals("Pick your nationality", form.nationalityError)
        assertEquals("Enter your passport / ID number", form.passportError)
        assertEquals("Pick the business country", form.businessCountryError)
        assertEquals("Enter it", form.registrationNumberError)
        assertEquals(ActionState.Idle, vm.saveState.value)
        verify(exactly = 0) { snackbar.showError(any<String>()) }
        coVerify(exactly = 0) { repository.updateIdentification(any(), any(), any(), any(), any()) }
    }

    /** `ValidatePassportId` counts 5 to 20 characters of what is sent, and the save sends it trimmed. */
    @Test
    fun `a passport outside 5 to 20 characters is named under it and not sent`() = runTest {
        listOf(" AB12 ", "A".repeat(21)).forEach { passport ->
            coEvery { repository.getCurrentEmployee() } returns
                ApiResult.Success(naturalPerson.copy(passportId = passport))
            val vm = viewModel()
            vm.awaitLoaded()

            vm.save()
            advanceUntilIdle()

            assertEquals(passport, "5 to 20 characters", vm.form().passportError)
        }
        coVerify(exactly = 0) { repository.updateIdentification(any(), any(), any(), any(), any()) }
    }

    @Test
    fun `the passport and registration number are checked and sent trimmed`() = runTest {
        coEvery { repository.getCurrentEmployee() } returns ApiResult.Success(
            naturalPerson.copy(passportId = " AB123 ", registrationNumber = " 12345678\n"),
        )
        coEvery {
            repository.updateIdentification(any(), any(), any(), any(), any())
        } returns ApiResult.Success(Unit)

        val vm = viewModel()
        vm.awaitLoaded()
        vm.save()
        advanceUntilIdle()

        coVerify(exactly = 1) {
            repository.updateIdentification(
                employeeId = "emp-1",
                nationalityId = "country-cz",
                passportId = "AB123",
                businessCountryId = "country-cz",
                registrationNumber = "12345678",
            )
        }
    }

    @Test
    fun `editing a field clears its own error and leaves the others`() = runTest {
        coEvery { repository.getCurrentEmployee() } returns ApiResult.Success(
            naturalPerson.copy(nationalityId = null, passportId = null, countryId = null, registrationNumber = null),
        )
        val vm = viewModel()
        vm.awaitLoaded()
        vm.save()
        advanceUntilIdle()

        vm.onNationalitySelected("country-sk")
        assertNull(vm.form().nationalityError)
        assertNotNull(vm.form().passportError)

        vm.onPassportChange("AB1234567")
        assertNull(vm.form().passportError)
        assertNotNull(vm.form().businessCountryError)

        vm.onBusinessCountrySelected("country-sk")
        vm.settle()
        assertNull(vm.form().businessCountryError)
        assertNotNull(vm.form().registrationNumberError)

        vm.onRegistrationNumberChange("12345678")
        assertNull(vm.form().registrationNumberError)
    }

    /** Refusals only the business register can give are the registration number's, not a snackbar's. */
    @Test
    fun `a register refusal is named under the registration number`() = runTest {
        listOf(
            "validation.registration_number.invalid_format",
            "validation.registration_number.not_registered",
            "employee.business_ceased",
            "employee.trade_licence_inactive",
        ).forEach { key ->
            coEvery { repository.getCurrentEmployee() } returns ApiResult.Success(naturalPerson)
            coEvery { repository.updateIdentification(any(), any(), any(), any(), any()) } returns ApiResult.Error(
                ApiError.BadRequest("Validation Error", validationErrors = mapOf("RegistrationNumber" to listOf(key))),
            )
            val vm = viewModel()
            vm.awaitLoaded()

            vm.save()
            advanceUntilIdle()

            assertEquals(key, "translated error", vm.form().registrationNumberError)
            assertEquals(key, ActionState.Idle, vm.saveState.value)
        }
        verify(exactly = 0) { snackbar.showError(any<String>()) }
    }

    @Test
    fun `a passport length refusal from the server is named under the passport`() = runTest {
        coEvery { repository.getCurrentEmployee() } returns ApiResult.Success(naturalPerson)
        coEvery { repository.updateIdentification(any(), any(), any(), any(), any()) } returns ApiResult.Error(
            ApiError.BadRequest(
                "Validation Error",
                validationErrors = mapOf("PassportId" to listOf("validation.passport_id.invalid_length")),
            ),
        )
        val vm = viewModel()
        vm.awaitLoaded()

        vm.save()
        advanceUntilIdle()

        assertEquals("translated error", vm.form().passportError)
        assertNull(vm.form().registrationNumberError)
        verify(exactly = 0) { snackbar.showError(any<String>()) }
    }

    /** A register that does not answer is not a fault in the number the cleaner typed. */
    @Test
    fun `a refusal that names no field stays a snackbar`() = runTest {
        coEvery { repository.getCurrentEmployee() } returns ApiResult.Success(naturalPerson)
        coEvery { repository.updateIdentification(any(), any(), any(), any(), any()) } returns ApiResult.Error(
            ApiError.BadRequest("Bad Request", errorKey = "employee.business_registry_unavailable"),
        )
        val vm = viewModel()
        vm.awaitLoaded()

        vm.save()
        advanceUntilIdle()

        verify { snackbar.showError("translated error") }
        assertNull(vm.form().registrationNumberError)
    }

    @Test
    fun `save failure snackbars and returns to Idle`() = runTest {
        coEvery { repository.getCurrentEmployee() } returns ApiResult.Success(naturalPerson)
        coEvery {
            repository.updateIdentification(any(), any(), any(), any(), any())
        } returns ApiResult.Error(ApiError.Network("down"))

        val vm = viewModel()
        vm.awaitLoaded()

        vm.save()
        advanceUntilIdle()

        assertEquals(ActionState.Idle, vm.saveState.value)
        verify { snackbar.showError("translated error") }
    }
}
