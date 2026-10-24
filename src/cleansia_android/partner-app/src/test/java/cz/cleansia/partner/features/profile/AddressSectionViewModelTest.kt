package cz.cleansia.partner.features.profile

import android.content.Context
import app.cash.turbine.ReceiveTurbine
import app.cash.turbine.test
import cz.cleansia.core.location.GeocodedAddress
import cz.cleansia.core.servicearea.ServiceAreaDataSource
import cz.cleansia.core.servicearea.ServiceAreaProvider
import cz.cleansia.core.servicearea.ServicedCity
import cz.cleansia.core.servicearea.ServicedCountry
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.partner.R
import cz.cleansia.partner.api.client.CountryApi
import cz.cleansia.partner.api.model.CountryListItem
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
import kotlinx.serialization.json.Json
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Before
import org.junit.Rule
import org.junit.Test
import retrofit2.Response

@OptIn(ExperimentalCoroutinesApi::class)
class AddressSectionViewModelTest {

    @get:Rule
    val mainRule = MainDispatcherRule()

    private lateinit var repository: ProfileRepository
    private lateinit var countryApi: CountryApi
    private lateinit var errorTranslator: ApiErrorTranslator
    private lateinit var snackbar: SnackbarController
    private lateinit var appContext: Context

    private val json = Json { ignoreUnknownKeys = true }

    private val servicedCzechia = ServicedCountry(id = "country-cz", isoCode = "cz", name = "Czech Republic")
    private val servicedSlovakia = ServicedCountry(id = "country-sk", isoCode = "sk", name = "Slovakia")

    private class FakeServiceAreaDataSource(
        var countries: List<ServicedCountry>? = null,
        var cities: List<ServicedCity>? = null,
    ) : ServiceAreaDataSource {
        override suspend fun fetchServicedCountries(): List<ServicedCountry>? = countries
        override suspend fun fetchServiceCities(countryId: String?): List<ServicedCity>? = cities
    }

    @Before
    fun setUp() {
        repository = mockk()
        countryApi = mockk()
        errorTranslator = mockk()
        snackbar = mockk(relaxed = true)
        appContext = mockk(relaxed = true)
        every { errorTranslator.translate(any()) } returns "translated error"
        every { appContext.getString(R.string.error_address_incomplete) } returns "This spot has no full address"
        every { appContext.getString(R.string.error_pick_address_first) } returns "Pick your address on the map first"
    }

    private fun viewModel(dataSource: FakeServiceAreaDataSource) = AddressSectionViewModel(
        profileRepository = repository,
        countryApi = countryApi,
        errorTranslator = errorTranslator,
        snackbar = snackbar,
        serviceAreaProvider = ServiceAreaProvider(dataSource),
        json = json,
        appContext = appContext,
    )

    private fun stubServicedCountries(vararg countries: CountryListItem) {
        coEvery { countryApi.countryGetServiced() } returns Response.success(countries.toList())
    }

    private fun employeeWithSavedCzAddress() = mockk<EmployeeItem> {
        every { id } returns "emp-1"
        every { street } returns "Dlouhá 1"
        every { city } returns "Prague"
        every { zipCode } returns "11000"
        every { countryId } returns "country-cz"
    }

    private fun employeeWithoutAddress() = mockk<EmployeeItem> {
        every { id } returns "emp-1"
        every { street } returns null
        every { city } returns null
        every { zipCode } returns null
        every { countryId } returns null
    }

    private suspend fun ReceiveTurbine<AddressSectionUiState>.awaitLoaded(): AddressForm {
        while (true) {
            val state = awaitItem()
            if (state is AddressSectionUiState.Loaded) return state.form
        }
    }

    private suspend fun ReceiveTurbine<AddressSectionUiState>.awaitResolvedStatus(): ServiceAreaStatus {
        while (true) {
            val state = awaitItem()
            val status = (state as? AddressSectionUiState.Loaded)?.form?.serviceAreaStatus ?: continue
            if (status != ServiceAreaStatus.Unknown) return status
        }
    }

    private fun AddressSectionViewModel.form(): AddressForm = (uiState.value as AddressSectionUiState.Loaded).form

    private suspend fun AddressSectionViewModel.awaitForm(): AddressForm {
        uiState.test {
            awaitLoaded()
            cancelAndIgnoreRemainingEvents()
        }
        return form()
    }

    private val bratislava = GeocodedAddress(
        latitude = 48.15,
        longitude = 17.11,
        street = "Hlavná 1",
        city = "Bratislava",
        zipCode = "81101",
        country = "Slovakia",
        countryIsoCode = "sk",
        formatted = "Hlavná 1, Bratislava",
    )

    private fun viewModelForSlovakia(): AddressSectionViewModel {
        stubServicedCountries(CountryListItem(id = "country-sk", isoCode = "SVK", name = "Slovakia"))
        coEvery { repository.getCurrentEmployee() } returns ApiResult.Success(employeeWithoutAddress())
        return viewModel(FakeServiceAreaDataSource(countries = listOf(servicedSlovakia), cities = emptyList()))
    }

    private fun assertNothingSent() = coVerify(exactly = 0) {
        repository.updateAddress(any(), any(), any(), any(), any(), any(), any(), any())
    }

    /** `UpdateAddressInfo` takes a street of 5 to 255 characters, a city of 2 to 100 and a postcode of 3 to 20. */
    @Test
    fun `a pick without a full address is named on the card at once, and save sends nothing`() = runTest {
        val vm = viewModelForSlovakia()
        vm.awaitForm()

        listOf(
            bratislava.copy(street = ""),
            bratislava.copy(street = "Hl 1"),
            bratislava.copy(city = "B"),
            bratislava.copy(zipCode = "81"),
        ).forEach { pick ->
            vm.applyPick(pick)
            advanceUntilIdle()
            assertEquals(pick.toString(), "This spot has no full address", vm.form().addressError)

            vm.save()
            advanceUntilIdle()
            assertEquals(pick.toString(), "This spot has no full address", vm.form().addressError)
        }
        assertNothingSent()
        verify(exactly = 0) { snackbar.showError(any<String>()) }
    }

    @Test
    fun `a full re-pick clears the card's error`() = runTest {
        val vm = viewModelForSlovakia()
        vm.awaitForm()
        vm.applyPick(bratislava.copy(street = ""))
        advanceUntilIdle()

        vm.applyPick(bratislava)
        advanceUntilIdle()

        assertNull(vm.form().addressError)
    }

    @Test
    fun `save with nothing picked names it on the card instead of a snackbar`() = runTest {
        val vm = viewModelForSlovakia()
        vm.awaitForm()

        vm.save()
        advanceUntilIdle()

        assertEquals("Pick your address on the map first", vm.form().addressError)
        assertNothingSent()
        verify(exactly = 0) { snackbar.showError(any<String>()) }
    }

    /** A stored address is read back, not picked: an incomplete one is named when the cleaner saves it. */
    @Test
    fun `an incomplete stored address is named on the card when it is saved`() = runTest {
        stubServicedCountries(CountryListItem(id = "country-cz", isoCode = "CZE", name = "Czech Republic"))
        coEvery { repository.getCurrentEmployee() } returns ApiResult.Success(
            mockk<EmployeeItem> {
                every { id } returns "emp-1"
                every { street } returns "Ab"
                every { city } returns "Prague"
                every { zipCode } returns "11000"
                every { countryId } returns "country-cz"
            },
        )
        val vm = viewModel(FakeServiceAreaDataSource(countries = listOf(servicedCzechia), cities = emptyList()))

        assertNull("nothing is said before the cleaner acts", vm.awaitForm().addressError)
        vm.save()
        advanceUntilIdle()

        assertEquals("This spot has no full address", vm.form().addressError)
        assertNothingSent()
    }

    /** The server's street, city and postcode refusals name no part, so they read as the one message. */
    @Test
    fun `a server refusal of the address parts is named on the card`() = runTest {
        listOf("common.required", "address.invalid_length").forEach { key ->
            coEvery { repository.updateAddress(any(), any(), any(), any(), any(), any(), any(), any()) } returns
                ApiResult.Error(ApiError.BadRequest("Validation Error", validationErrors = mapOf("Street" to listOf(key))))
            val vm = viewModelForSlovakia()
            vm.awaitForm()
            vm.applyPick(bratislava)
            advanceUntilIdle()

            vm.save()
            advanceUntilIdle()

            assertEquals(key, "This spot has no full address", vm.form().addressError)
        }
        verify(exactly = 0) { snackbar.showError(any<String>()) }
    }

    @Test
    fun `a refusal that names no part of the address stays a snackbar`() = runTest {
        coEvery { repository.updateAddress(any(), any(), any(), any(), any(), any(), any(), any()) } returns
            ApiResult.Error(ApiError.Network("down"))
        val vm = viewModelForSlovakia()
        vm.awaitForm()
        vm.applyPick(bratislava)
        advanceUntilIdle()

        vm.save()
        advanceUntilIdle()

        verify { snackbar.showError("translated error") }
        assertNull(vm.form().addressError)
    }

    @Test
    fun `saved address with a backend alpha-3 country code resolves to InServicedCity`() = runTest {
        stubServicedCountries(CountryListItem(id = "country-cz", isoCode = "CZE", name = "Czech Republic"))
        coEvery { repository.getCurrentEmployee() } returns ApiResult.Success(employeeWithSavedCzAddress())
        val dataSource = FakeServiceAreaDataSource(
            countries = listOf(servicedCzechia),
            cities = listOf(ServicedCity(id = "city-prg", countryId = "country-cz", name = "Prague")),
        )

        val vm = viewModel(dataSource)

        vm.uiState.test {
            assertEquals(ServiceAreaStatus.InServicedCity("Prague"), awaitResolvedStatus())
            cancelAndIgnoreRemainingEvents()
        }
    }

    @Test
    fun `slovak pick saves with the backend country id resolved from alpha-3 SVK`() = runTest {
        stubServicedCountries(CountryListItem(id = "country-sk", isoCode = "SVK", name = "Slovakia"))
        coEvery { repository.getCurrentEmployee() } returns ApiResult.Success(employeeWithoutAddress())
        coEvery {
            repository.updateAddress(any(), any(), any(), any(), any(), any(), any(), any())
        } returns ApiResult.Success(Unit)
        val dataSource = FakeServiceAreaDataSource(
            countries = listOf(servicedSlovakia),
            cities = listOf(ServicedCity(id = "city-ba", countryId = "country-sk", name = "Bratislava")),
        )

        val vm = viewModel(dataSource)
        vm.uiState.test {
            awaitLoaded()
            cancelAndIgnoreRemainingEvents()
        }

        vm.applyPick(
            GeocodedAddress(
                latitude = 48.15,
                longitude = 17.11,
                street = "Hlavná 1",
                city = "Bratislava",
                zipCode = "81101",
                country = "Slovakia",
                countryIsoCode = "sk",
                formatted = "Hlavná 1, Bratislava",
            ),
        )
        advanceUntilIdle()

        vm.saved.test {
            vm.save()
            advanceUntilIdle()
            awaitItem()
        }

        coVerify {
            repository.updateAddress(
                employeeId = "emp-1",
                street = "Hlavná 1",
                city = "Bratislava",
                zipCode = "81101",
                countryId = "country-sk",
                state = null,
                latitude = 48.15,
                longitude = 17.11,
            )
        }
        verify(exactly = 0) { snackbar.showError(any<String>()) }
        verify(exactly = 0) { snackbar.showError(any<ApiError>()) }
    }

    @Test
    fun `pick in an unserviced country shows CountryNotServiced`() = runTest {
        stubServicedCountries(CountryListItem(id = "country-cz", isoCode = "CZE", name = "Czech Republic"))
        coEvery { repository.getCurrentEmployee() } returns ApiResult.Success(employeeWithoutAddress())
        val dataSource = FakeServiceAreaDataSource(countries = listOf(servicedCzechia), cities = emptyList())

        val vm = viewModel(dataSource)
        vm.uiState.test {
            awaitLoaded()
            cancelAndIgnoreRemainingEvents()
        }

        vm.applyPick(
            GeocodedAddress(
                latitude = 52.52,
                longitude = 13.40,
                street = "Unter den Linden 1",
                city = "Berlin",
                zipCode = "10117",
                country = "Germany",
                countryIsoCode = "de",
                formatted = "Unter den Linden 1, Berlin",
            ),
        )
        advanceUntilIdle()

        val form = (vm.uiState.value as AddressSectionUiState.Loaded).form
        assertEquals(ServiceAreaStatus.CountryNotServiced, form.serviceAreaStatus)
    }

    @Test
    fun `service-area lookup failure keeps the indicator Unknown, not CountryNotServiced`() = runTest {
        stubServicedCountries(CountryListItem(id = "country-cz", isoCode = "CZE", name = "Czech Republic"))
        coEvery { repository.getCurrentEmployee() } returns ApiResult.Success(employeeWithoutAddress())
        val dataSource = FakeServiceAreaDataSource(countries = null, cities = null)

        val vm = viewModel(dataSource)
        vm.uiState.test {
            awaitLoaded()
            cancelAndIgnoreRemainingEvents()
        }

        vm.applyPick(
            GeocodedAddress(
                latitude = 50.08,
                longitude = 14.43,
                street = "Dlouhá 1",
                city = "Prague",
                zipCode = "11000",
                country = "Czech Republic",
                countryIsoCode = "cz",
                formatted = "Dlouhá 1, Prague",
            ),
        )
        advanceUntilIdle()

        val form = (vm.uiState.value as AddressSectionUiState.Loaded).form
        assertEquals(ServiceAreaStatus.Unknown, form.serviceAreaStatus)
    }
}
