package cz.cleansia.partner.features.profile

import android.content.Context
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
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.runTest
import kotlinx.serialization.json.Json
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.ResponseBody.Companion.toResponseBody
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
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
        return (uiState.value as IdentificationSectionUiState.Loaded).form
    }

    @Test
    fun `an operator-onboarded company shows its stored legal name`() = runTest {
        coEvery { repository.getCurrentEmployee() } returns ApiResult.Success(operatorOnboardedCompany)

        val form = viewModel().awaitLoaded()

        assertEquals("Uklid Praha s.r.o.", form.storedLegalEntityName)
    }

    @Test
    fun `a natural person shows no legal name, even if the payload carries one`() = runTest {
        coEvery { repository.getCurrentEmployee() } returns
            ApiResult.Success(naturalPerson.copy(legalEntityName = "stale"))

        val form = viewModel().awaitLoaded()

        assertNull(form.storedLegalEntityName)
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

    @Test
    fun `a missing registration number is refused locally and the repo is not called`() = runTest {
        coEvery { repository.getCurrentEmployee() } returns
            ApiResult.Success(naturalPerson.copy(registrationNumber = null))
        every { appContext.getString(R.string.error_registration_number_required) } returns "Enter it"

        val vm = viewModel()
        vm.awaitLoaded()

        vm.save()
        advanceUntilIdle()

        verify { snackbar.showError("Enter it") }
        coVerify(exactly = 0) { repository.updateIdentification(any(), any(), any(), any(), any()) }
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
