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
            listOf(
                CountryListItem(id = "country-cz", isoCode = "CZE", isoAlpha2 = "CZ", name = "Czech Republic"),
                CountryListItem(id = "country-sk", isoCode = "SVK", isoAlpha2 = "SK", name = "Slovakia"),
                CountryListItem(id = "country-de", isoCode = "DEU", isoAlpha2 = "DE", name = "Germany"),
            ),
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
        verify(exactly = 0) { snackbar.showError(any<String>()) }
        verify(exactly = 0) { snackbar.showError(any<ApiError>()) }
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

    private fun BankSectionViewModel.form(): BankForm = (uiState.value as BankSectionUiState.Loaded).form

    @Test
    fun `a CZ or SK bank takes the three parts and an IBAN alone does not submit`() = runTest {
        val vm = viewModel()
        vm.awaitForm()

        vm.onIbanChange("CZ6508000000192000145399")
        assertTrue(vm.form().usesDomesticAccount)
        assertFalse(vm.form().canSubmit)

        vm.onBankCountrySelected("country-sk")
        assertTrue(vm.form().usesDomesticAccount)
    }

    @Test
    fun `a bank outside CZ and SK takes one IBAN instead of the parts`() = runTest {
        val vm = viewModel()
        vm.awaitForm()

        vm.onBankCountrySelected("country-de")
        vm.onAccountNumberChange("5885638003")
        assertFalse(vm.form().usesDomesticAccount)
        assertFalse("the parts are not this country's identifier", vm.form().canSubmit)

        vm.onIbanChange("DE89370400440532013000")
        assertTrue(vm.form().canSubmit)
    }

    /**
     * The stored IBAN of a CZ account is the one the server derived from the old parts, so sending it
     * back with edited parts was refused as `validation.payout.iban_mismatch`.
     */
    @Test
    fun `a CZ save sends the parts and leaves the IBAN to the server`() = runTest {
        coEvery { repository.getPayoutDetails() } returns ApiResult.Success(czechPayout)
        coEvery {
            repository.updateBankDetails(any(), any(), any(), any(), any(), any(), any(), any(), any())
        } returns ApiResult.Success(Unit)
        val vm = viewModel()
        vm.awaitForm()

        vm.onAccountNumberChange("5885638003")
        vm.onBankCodeChange("5500")
        vm.save()
        advanceUntilIdle()

        coVerify {
            repository.updateBankDetails(
                employeeId = "emp-1",
                bankCountryId = "country-cz",
                accountPrefix = "19",
                accountNumber = "5885638003",
                bankCode = "5500",
                iban = null,
                swift = null,
                bankName = null,
                holderName = null,
            )
        }
    }

    @Test
    fun `an IBAN save sends the IBAN without the parts`() = runTest {
        coEvery { repository.getPayoutDetails() } returns ApiResult.Success(czechPayout)
        coEvery {
            repository.updateBankDetails(any(), any(), any(), any(), any(), any(), any(), any(), any())
        } returns ApiResult.Success(Unit)
        val vm = viewModel()
        vm.awaitForm()

        vm.onBankCountrySelected("country-de")
        vm.onIbanChange("de89 3704 0044 0532 0130 00")
        vm.onSwiftChange("cobadeff")
        vm.save()
        advanceUntilIdle()

        coVerify {
            repository.updateBankDetails(
                employeeId = "emp-1",
                bankCountryId = "country-de",
                accountPrefix = null,
                accountNumber = null,
                bankCode = null,
                iban = "DE89370400440532013000",
                swift = "COBADEFF",
                bankName = null,
                holderName = null,
            )
        }
    }

    @Test
    fun `an IBAN the server would refuse is named under the field instead of being sent`() = runTest {
        val vm = viewModel()
        vm.awaitForm()
        vm.onBankCountrySelected("country-de")
        vm.onIbanChange("DE89370400440532013001")
        assertFalse("no error before a save is tried", vm.form().ibanChecked)

        vm.save()
        advanceUntilIdle()

        assertTrue(vm.form().ibanChecked)
        assertEquals(IbanProblem.Invalid, vm.form().ibanProblem)
        coVerify(exactly = 0) {
            repository.updateBankDetails(any(), any(), any(), any(), any(), any(), any(), any(), any())
        }

        vm.onIbanChange("DE89370400440532013000")
        assertNull("the error clears as the IBAN is fixed", vm.form().ibanProblem)
    }

    @Test
    fun `an IBAN is valid exactly when the server's IbanCalculator takes it`() {
        mapOf(
            "DE89370400440532013000" to "DE",
            "GB82WEST12345698765432" to "GB",
            "AT611904300234573201" to "AT",
            "FR1420041010050500013M02606" to "FR",
            "NL91ABNA0417164300" to "NL",
            "PL61109010140000071219812874" to "PL",
            "CH9300762011623852957" to "CH",
            "UA213223130000026007233566001" to "UA",
            // Not in the registry, so the generic 15–34 and the check digits decide, as on the server.
            "US64SVBKUS6S3300958879" to "US",
        ).forEach { (iban, country) -> assertNull(iban, ibanProblem(iban, country)) }
        assertNull("spaces and lower case are a statement's, not a typo", ibanProblem("de89 3704 0044 0532 0130 00", "DE"))
    }

    @Test
    fun `each refusal names what the cleaner can fix`() {
        assertEquals(IbanProblem.OtherCountry, ibanProblem("CZ6508000000192000145399", "DE"))
        assertEquals(IbanProblem.WrongLength(22), ibanProblem("DE8937040044053201300", "DE"))
        assertEquals(IbanProblem.WrongLength(22), ibanProblem("DE893704004405320130000", "DE"))
        assertEquals(IbanProblem.Invalid, ibanProblem("DE89370400440532013001", "DE"))
        assertEquals(IbanProblem.Invalid, ibanProblem("0532013000", "DE"))
        assertEquals(IbanProblem.Invalid, ibanProblem("", "DE"))
        assertEquals("check digits are digits", IbanProblem.Invalid, ibanProblem("DEX9370400440532013000", "DE"))
        assertEquals("outside the registry the generic bound holds", IbanProblem.Invalid, ibanProblem("US64SVBK", "US"))
    }

    /** The client's table is the server's: a length only one of them knows refuses on one side only. */
    @Test
    fun `the registry lengths are the server's`() {
        val solutionDir = generateSequence(java.io.File(".").absoluteFile) { it.parentFile }
            .firstOrNull { java.io.File(it, "Cleansia.Api.sln").isFile }
            ?: error("Cleansia.Api.sln not found above ${java.io.File(".").absolutePath}")
        val source = java.io.File(solutionDir, "Cleansia.Core.Domain/Payouts/IbanCalculator.cs").readText()
        val table = source.substringAfter("RegistryLengths").substringBefore("};")
        val server = Regex("""\["([A-Z]{2})"\]\s*=\s*(\d+)""").findAll(table)
            .associate { it.groupValues[1] to it.groupValues[2].toInt() }
        assertTrue("the parser found no registry entries", server.isNotEmpty())
        assertEquals(server, IbanRegistryLengths)
    }

    @Test
    fun `an IBAN is drawn in groups of four and the caret crosses the spaces`() {
        val shown = IbanGroupsOfFour.filter(androidx.compose.ui.text.AnnotatedString("DE89370400440532013000"))
        assertEquals("DE89 3704 0044 0532 0130 00", shown.text.text)
        val mapping = shown.offsetMapping
        assertEquals(4, mapping.originalToTransformed(4))
        assertEquals(6, mapping.originalToTransformed(5))
        assertEquals(27, mapping.originalToTransformed(22))
        assertEquals(4, mapping.transformedToOriginal(5))
        assertEquals(22, mapping.transformedToOriginal(27))
        (0..22).forEach { assertEquals(it, mapping.transformedToOriginal(mapping.originalToTransformed(it))) }
        assertEquals("", IbanGroupsOfFour.filter(androidx.compose.ui.text.AnnotatedString("")).text.text)
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
