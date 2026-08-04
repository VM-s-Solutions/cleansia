package cz.cleansia.partner.features.profile

import android.content.Context
import app.cash.turbine.test
import cz.cleansia.core.network.ApiError
import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.core.ui.state.ActionState
import cz.cleansia.partner.R
import cz.cleansia.partner.api.client.CountryApi
import cz.cleansia.partner.api.model.CountryListItem
import cz.cleansia.partner.api.model.EmployeeItem
import cz.cleansia.partner.api.model.MyPayoutDetails
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
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Rule
import org.junit.Test
import retrofit2.Response

@OptIn(ExperimentalCoroutinesApi::class)
class BankSectionViewModelTest {

    @get:Rule
    val mainRule = MainDispatcherRule()

    private lateinit var repository: ProfileRepository
    private lateinit var countryApi: CountryApi
    private lateinit var snackbar: SnackbarController
    private lateinit var errorTranslator: ApiErrorTranslator
    private lateinit var appContext: Context

    private val json = Json { ignoreUnknownKeys = true }

    private val employee = mockk<EmployeeItem> {
        every { id } returns "emp-1"
        every { countryId } returns "country-cz"
    }

    private val czechPayout = MyPayoutDetails(
        bankCountryId = "country-cz",
        accountPrefix = "000019",
        accountNumber = "2000145399",
        bankCode = "0800",
        iban = "CZ6508000000192000145399",
    )

    @Before
    fun setUp() {
        repository = mockk()
        countryApi = mockk()
        snackbar = mockk(relaxed = true)
        errorTranslator = mockk()
        appContext = mockk(relaxed = true)
        every { errorTranslator.translate(any()) } returns "translated error"
        every { appContext.getString(R.string.error_profile_not_loaded) } returns "Profile not loaded yet"
        coEvery { countryApi.countryGetOverview() } returns Response.success(
            listOf(CountryListItem(id = "country-cz", isoCode = "CZE", name = "Czech Republic")),
        )
        coEvery { repository.getCurrentEmployee() } returns ApiResult.Success(employee)
        coEvery { repository.getPayoutDetails() } returns ApiResult.Success(null)
    }

    private fun viewModel() = BankSectionViewModel(
        profileRepository = repository,
        countryApi = countryApi,
        errorTranslator = errorTranslator,
        snackbar = snackbar,
        json = json,
        appContext = appContext,
    )

    /** The country lookup runs on `Dispatchers.IO`, so the load settles in real time, not virtual. */
    private suspend fun BankSectionViewModel.awaitSettled(): BankSectionUiState {
        uiState.test {
            while (awaitItem() is BankSectionUiState.Loading) Unit
            cancelAndIgnoreRemainingEvents()
        }
        return uiState.value
    }

    private suspend fun BankSectionViewModel.awaitForm(): BankForm =
        (awaitSettled() as BankSectionUiState.Loaded).form

    @Test
    fun `load transitions Loading to Loaded with the stored parts, padding stripped`() = runTest {
        coEvery { repository.getPayoutDetails() } returns ApiResult.Success(czechPayout)

        val vm = viewModel()
        assertEquals(BankSectionUiState.Loading, vm.uiState.value)

        val form = vm.awaitForm()
        assertEquals("emp-1", form.employeeId)
        assertEquals("country-cz", form.bankCountryId)
        assertEquals("19", form.accountPrefix)
        assertEquals("2000145399", form.accountNumber)
        assertEquals("0800", form.bankCode)
        assertEquals("CZ6508000000192000145399", form.iban)
    }

    @Test
    fun `a cleaner with no payout details yet gets an empty form, not an error`() = runTest {
        val form = viewModel().awaitForm()

        assertEquals("", form.accountNumber)
        assertEquals("", form.iban)
        // The bank is presumed to be in the country the cleaner lives in until they say otherwise.
        assertEquals("country-cz", form.bankCountryId)
        verify(exactly = 0) { snackbar.showError(any()) }
    }

    @Test
    fun `load failure transitions to Error and snackbars`() = runTest {
        coEvery { repository.getCurrentEmployee() } returns ApiResult.Error(ApiError.Network("down"))

        assertEquals(BankSectionUiState.Error, viewModel().awaitSettled())

        verify { snackbar.showError("translated error") }
    }

    @Test
    fun `a failed payout read is an error, not an empty form`() = runTest {
        coEvery { repository.getPayoutDetails() } returns ApiResult.Error(ApiError.Network("down"))

        assertEquals(BankSectionUiState.Error, viewModel().awaitSettled())

        verify { snackbar.showError("translated error") }
    }

    @Test
    fun `the local parts accept digits only`() = runTest {
        val vm = viewModel()
        vm.awaitForm()

        vm.onAccountPrefixChange("19-")
        vm.onAccountNumberChange("2000-145 399")
        vm.onBankCodeChange("/0800")

        val form = (vm.uiState.value as BankSectionUiState.Loaded).form
        assertEquals("19", form.accountPrefix)
        assertEquals("2000145399", form.accountNumber)
        assertEquals("0800", form.bankCode)
    }

    @Test
    fun `iban and swift are upper-cased and stripped of separators`() = runTest {
        val vm = viewModel()
        vm.awaitForm()

        vm.onIbanChange("cz65 0800 0000 1920 0014 5399")
        vm.onSwiftChange("gibacz-px")

        val form = (vm.uiState.value as BankSectionUiState.Loaded).form
        assertEquals("CZ6508000000192000145399", form.iban)
        assertEquals("GIBACZPX", form.swift)
    }

    @Test
    fun `an account with no identifier cannot be submitted`() = runTest {
        val vm = viewModel()
        assertFalse(vm.awaitForm().canSubmit)

        vm.save()
        advanceUntilIdle()

        coVerify(exactly = 0) {
            repository.updateBankDetails(any(), any(), any(), any(), any(), any(), any(), any(), any())
        }
    }

    @Test
    fun `an iban on its own is submittable — the server decides whether the scheme needs the parts`() =
        runTest {
            val vm = viewModel()
            vm.awaitForm()

            vm.onIbanChange("DE89370400440532013000")

            assertTrue((vm.uiState.value as BankSectionUiState.Loaded).form.canSubmit)
        }

    @Test
    fun `an account number without a bank country cannot be submitted`() = runTest {
        every { employee.countryId } returns null
        val vm = viewModel()
        vm.awaitForm()

        vm.onAccountNumberChange("5885638003")

        val form = (vm.uiState.value as BankSectionUiState.Loaded).form
        assertNull(form.bankCountryId)
        assertFalse(form.canSubmit)
    }

    @Test
    fun `save sends every part the backend now takes`() = runTest {
        coEvery {
            repository.updateBankDetails(any(), any(), any(), any(), any(), any(), any(), any(), any())
        } returns ApiResult.Success(Unit)

        val vm = viewModel()
        vm.awaitForm()

        vm.onBankCountrySelected("country-cz")
        vm.onAccountPrefixChange("19")
        vm.onAccountNumberChange("2000145399")
        vm.onBankCodeChange("0800")
        vm.onSwiftChange("GIBACZPX")
        vm.onBankNameChange(" Česká spořitelna ")
        vm.onHolderNameChange(" Jan Novák ")

        vm.saved.test {
            vm.save()
            advanceUntilIdle()
            awaitItem()
        }

        coVerify {
            repository.updateBankDetails(
                employeeId = "emp-1",
                bankCountryId = "country-cz",
                accountPrefix = "19",
                accountNumber = "2000145399",
                bankCode = "0800",
                iban = null,
                swift = "GIBACZPX",
                bankName = "Česká spořitelna",
                holderName = "Jan Novák",
            )
        }
        assertEquals(ActionState.Idle, vm.saveState.value)
    }

    @Test
    fun `save failure snackbars and returns to Idle`() = runTest {
        coEvery {
            repository.updateBankDetails(any(), any(), any(), any(), any(), any(), any(), any(), any())
        } returns ApiResult.Error(ApiError.BadRequest("bad", errorKey = "validation.payout.invalid_account_number"))

        val vm = viewModel()
        vm.awaitForm()
        vm.onAccountNumberChange("5885638004")
        vm.onBankCodeChange("5500")

        vm.save()
        advanceUntilIdle()

        assertTrue(vm.saveState.value is ActionState.Idle)
        verify { snackbar.showError("translated error") }
    }

    @Test
    fun `save without a loaded employee id snackbars instead of calling the repo`() = runTest {
        every { employee.id } returns null
        val vm = viewModel()
        vm.awaitForm()
        vm.onAccountNumberChange("5885638003")
        vm.onBankCodeChange("5500")

        vm.save()
        advanceUntilIdle()

        verify { snackbar.showError("Profile not loaded yet") }
        coVerify(exactly = 0) {
            repository.updateBankDetails(any(), any(), any(), any(), any(), any(), any(), any(), any())
        }
    }
}
