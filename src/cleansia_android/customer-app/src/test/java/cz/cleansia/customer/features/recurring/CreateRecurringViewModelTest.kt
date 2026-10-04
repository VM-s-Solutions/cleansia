package cz.cleansia.customer.features.recurring

import android.content.Context
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.lifecycle.SavedStateHandle
import androidx.lifecycle.ViewModelStore
import app.cash.turbine.test
import cz.cleansia.core.network.ApiError
import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.customer.R
import cz.cleansia.customer.core.booking.BookingApi
import cz.cleansia.customer.core.booking.CashEligibility
import cz.cleansia.customer.core.booking.DirtinessLevel
import cz.cleansia.customer.core.booking.PropertySize
import cz.cleansia.customer.core.booking.QuoteOrderCommand
import cz.cleansia.customer.core.booking.QuoteOrderResponse
import cz.cleansia.customer.core.catalog.CatalogRepository
import cz.cleansia.customer.core.catalog.CategoryDto
import cz.cleansia.customer.core.catalog.PackageListItem
import cz.cleansia.customer.core.catalog.PackageServiceSummary
import cz.cleansia.customer.core.catalog.ServiceListItem
import cz.cleansia.customer.core.consent.GdprConsentClient
import cz.cleansia.customer.core.consent.SignupConsentType
import cz.cleansia.customer.core.data.AddressRepository
import cz.cleansia.customer.core.data.UserAddress
import cz.cleansia.customer.core.market.MarketListItem
import cz.cleansia.customer.core.market.MarketRepository
import cz.cleansia.customer.core.market.MarketState
import cz.cleansia.customer.core.orders.OrderDetailDto
import cz.cleansia.customer.core.orders.OrderPackageDetailsDto
import cz.cleansia.customer.core.orders.OrderRepository
import cz.cleansia.customer.core.orders.OrderServiceDetailsDto
import cz.cleansia.customer.core.recurring.RecurrenceFrequency
import cz.cleansia.customer.core.recurring.RecurringBookingRepository
import cz.cleansia.customer.core.recurring.CreateRecurringBookingRequest
import cz.cleansia.customer.core.recurring.RecurringBookingTemplateDto
import cz.cleansia.customer.core.recurring.UpdateRecurringBookingRequest
import cz.cleansia.customer.features.booking.DoubleBooking
import cz.cleansia.customer.features.booking.selectedIncluding
import cz.cleansia.customer.testing.MainDispatcherRule
import cz.cleansia.customer.ui.state.ActionState
import io.mockk.clearMocks
import io.mockk.coEvery
import io.mockk.coVerify
import io.mockk.every
import io.mockk.mockk
import io.mockk.slot
import io.mockk.verify
import kotlinx.coroutines.CompletableDeferred
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.runCurrent
import kotlinx.coroutines.test.runTest
import kotlinx.datetime.LocalDate
import kotlinx.datetime.LocalDateTime
import kotlinx.datetime.TimeZone
import kotlinx.datetime.atStartOfDayIn
import kotlinx.datetime.toInstant
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Rule
import org.junit.Test
import retrofit2.Response

@OptIn(ExperimentalCoroutinesApi::class)
class CreateRecurringViewModelTest {

    @get:Rule
    val mainRule = MainDispatcherRule()

    private lateinit var recurringRepo: RecurringBookingRepository
    private lateinit var orderRepo: OrderRepository
    private lateinit var catalogRepo: CatalogRepository
    private lateinit var addressRepo: AddressRepository
    private lateinit var marketRepo: MarketRepository
    private lateinit var bookingApi: BookingApi
    private lateinit var consentClient: GdprConsentClient
    private lateinit var snackbar: SnackbarController
    private lateinit var appContext: Context
    private lateinit var marketFlow: MutableStateFlow<MarketState>

    private lateinit var templatesFlow: MutableStateFlow<List<RecurringBookingTemplateDto>>
    private lateinit var addressesFlow: MutableStateFlow<List<UserAddress>>
    private lateinit var catalogServicesFlow: MutableStateFlow<List<ServiceListItem>>
    private lateinit var catalogPackagesFlow: MutableStateFlow<List<PackageListItem>>
    private lateinit var catalogCountryFlow: MutableStateFlow<String?>
    private lateinit var catalogLoadedFlow: MutableStateFlow<Boolean>

    @Before
    fun setUp() {
        recurringRepo = mockk(relaxed = true)
        orderRepo = mockk(relaxed = true)
        catalogRepo = mockk(relaxed = true)
        addressRepo = mockk(relaxed = true)
        marketRepo = mockk(relaxed = true)
        bookingApi = mockk()
        coEvery { bookingApi.quote(any()) } returns Response.success(crewQuote(1))
        consentClient = mockk()
        coEvery { consentClient.grantedTypes() } returns
            setOf(SignupConsentType.TermsOfService, SignupConsentType.PrivacyPolicy)
        snackbar = mockk(relaxed = true)
        appContext = mockk(relaxed = true)
        marketFlow = MutableStateFlow(MarketState.Unavailable)
        every { marketRepo.state } returns marketFlow
        coEvery { marketRepo.ensureLoaded() } answers { marketFlow.value }
        templatesFlow = MutableStateFlow(emptyList())
        addressesFlow = MutableStateFlow(emptyList())
        catalogServicesFlow = MutableStateFlow(emptyList())
        catalogPackagesFlow = MutableStateFlow(emptyList())
        catalogCountryFlow = MutableStateFlow(null)
        catalogLoadedFlow = MutableStateFlow(false)
        coEvery { catalogRepo.refresh(null) } coAnswers {
            catalogLoadedFlow.value = true
            ApiResult.Success(Unit)
        }
        every { catalogRepo.services } returns catalogServicesFlow
        every { catalogRepo.packages } returns catalogPackagesFlow
        every { catalogRepo.countryId } returns catalogCountryFlow
        every { catalogRepo.loaded } returns catalogLoadedFlow
        every { addressRepo.addresses } returns addressesFlow
        every { recurringRepo.templates } returns templatesFlow
        every { appContext.getString(R.string.booking_market_items_unavailable) } returns marketNotice
    }

    private fun viewModel(orderId: String? = null, templateId: String? = null) =
        CreateRecurringViewModel(
            savedStateHandle = SavedStateHandle(
                mapOf("orderId" to orderId, "templateId" to templateId),
            ),
            recurringRepo = recurringRepo,
            orderRepo = orderRepo,
            catalogRepo = catalogRepo,
            addressRepo = addressRepo,
            marketRepo = marketRepo,
            bookingApi = bookingApi,
            consentClient = consentClient,
            snackbar = snackbar,
            appContext = appContext,
        )

    private fun slovakMarket(): MarketState {
        val svk = MarketListItem(
            countryId = "svk-id",
            isoCode = "SVK",
            isoAlpha2 = "SK",
            name = "Slovakia",
            currencyId = "cur-eur",
            currencyCode = "EUR",
            currencySymbol = "€",
            isDefault = false,
        )
        return MarketState.Resolved(listOf(svk), svk)
    }

    // The catalogue is a UiState like the booking wizard's: a failed entry read is retried from the
    // screen, and nothing is submitted against a catalogue the customer never saw (iOS parity, 4cc7a9dd).

    @Test
    fun `a successful entry read lands the catalogue as Loaded`() = runTest {
        val vm = viewModel()
        advanceUntilIdle()

        assertEquals(RecurringCatalogState.Loaded, vm.catalogState.value)
    }

    @Test
    fun `a failed entry read leaves the catalogue on Error and holds submit`() = runTest {
        coEvery { catalogRepo.refresh(null) } returns ApiResult.Error(ApiError.Network("boom"))

        val vm = viewModel()
        advanceUntilIdle()
        fillValidForm(vm)
        runCurrent()

        assertEquals(RecurringCatalogState.Error, vm.catalogState.value)
        assertEquals(false, vm.isValid.value)
        vm.submit()
        advanceUntilIdle()
        coVerify(exactly = 0) { recurringRepo.create(any()) }
        assertEquals(ActionState.Idle, vm.submitState.value)
    }

    @Test
    fun `step three cannot submit until a catalogue has landed`() = runTest {
        coEvery { catalogRepo.refresh(null) } returns ApiResult.Error(ApiError.Network("boom"))
        val vm = viewModel()
        advanceUntilIdle()
        fillValidForm(vm)
        vm.nextStep()
        vm.nextStep()
        runCurrent()
        assertEquals(false, vm.canAdvance.value)

        coEvery { catalogRepo.refresh(null) } coAnswers {
            catalogServicesFlow.value = listOf(service("svc-1"))
            catalogLoadedFlow.value = true
            ApiResult.Success(Unit)
        }
        vm.retryCatalog()
        advanceUntilIdle()

        assertEquals(RecurringCatalogState.Loaded, vm.catalogState.value)
        assertEquals(true, vm.canAdvance.value)
        assertEquals(true, vm.isValid.value)
    }

    @Test
    fun `retryCatalog re-reads the selected address's market and prunes the prefilled selection`() = runTest {
        addressesFlow.value = listOf(address("addr-sk", "svk-id", isDefault = true))
        coEvery { catalogRepo.refresh(any()) } returns ApiResult.Error(ApiError.Network("boom"))
        sourceOrder(services = listOf("s-1", "s-2"))
        val vm = viewModel(orderId = "ord-7")
        advanceUntilIdle()
        assertEquals(RecurringCatalogState.Error, vm.catalogState.value)
        assertEquals(setOf("s-1", "s-2"), vm.state.value.selectedServiceIds)

        slovakCatalogue(service("s-1"))
        vm.retryCatalog()
        advanceUntilIdle()

        assertEquals(RecurringCatalogState.Loaded, vm.catalogState.value)
        assertEquals(setOf("s-1"), vm.state.value.selectedServiceIds)
        verify(exactly = 1) { snackbar.showInfo(marketNotice) }
    }

    @Test
    fun `a retry that fails again stays on Error and prunes nothing`() = runTest {
        coEvery { catalogRepo.refresh(any()) } returns ApiResult.Error(ApiError.Network("boom"))
        val vm = viewModel()
        advanceUntilIdle()
        vm.toggleService("svc-1")

        vm.retryCatalog()
        advanceUntilIdle()

        assertEquals(RecurringCatalogState.Error, vm.catalogState.value)
        assertEquals(setOf("svc-1"), vm.state.value.selectedServiceIds)
        verify(exactly = 0) { snackbar.showInfo(any<String>()) }
    }

    // ADR-0058 D4/D5: the entry read prices the chosen market; the address then wins; leaving hands
    // the market back rather than the platform default.

    @Test
    fun `the entry read is for the chosen market`() = runTest {
        marketFlow.value = slovakMarket()
        slovakCatalogue(service("svc-1"))

        val vm = viewModel()
        advanceUntilIdle()

        coVerify(exactly = 1) { catalogRepo.refresh("svk-id") }
        coVerify(exactly = 0) { catalogRepo.refresh(null) }
        assertEquals(RecurringCatalogState.Loaded, vm.catalogState.value)
    }

    @Test
    fun `leaving the wizard after a foreign address returns the catalogue to the chosen market`() = runTest {
        marketFlow.value = slovakMarket()
        addressesFlow.value = listOf(address("addr-cz", "cze-id", isDefault = true))
        coEvery { catalogRepo.refresh("svk-id") } coAnswers {
            catalogCountryFlow.value = "svk-id"
            catalogLoadedFlow.value = true
            ApiResult.Success(Unit)
        }
        coEvery { catalogRepo.refresh("cze-id") } coAnswers {
            catalogCountryFlow.value = "cze-id"
            ApiResult.Success(Unit)
        }
        val vm = viewModel()
        advanceUntilIdle()
        assertEquals("cze-id", catalogCountryFlow.value)
        clearMocks(catalogRepo, answers = false)

        ViewModelStore().apply { put("wizard", vm) }.clear()
        advanceUntilIdle()

        coVerify(exactly = 1) { catalogRepo.refresh("svk-id") }
        coVerify(exactly = 0) { catalogRepo.refresh(null) }
    }

    private fun fillValidForm(vm: CreateRecurringViewModel) {
        vm.setSavedAddressId("addr-1")
        vm.toggleService("svc-1")
        vm.setDirtinessLevel(DirtinessLevel.Normal)
        vm.setStartsOn("2026-07-01T00:00:00Z")
        vm.setEarlyPerformanceRequested(true)
    }

    private val plusRefusal = "Recurring cleanings are a Cleansia Plus benefit — subscribe to set one up."

    private val marketNotice = "Some of your picks are not offered at this address and were removed."

    private fun address(serverId: String, countryId: String?, isDefault: Boolean = false) = UserAddress(
        id = serverId,
        serverId = serverId,
        label = serverId,
        street = "Hlavná 1",
        city = "Bratislava",
        zipCode = "81101",
        countryId = countryId,
        isDefault = isDefault,
    )

    private fun service(id: String) = ServiceListItem(
        id = id,
        name = "Service $id",
        basePrice = 10.0,
        perRoomPrice = 1.0,
        category = CategoryDto(id = "c-1", slug = "general", name = "General"),
    )

    private fun pkg(id: String) = PackageListItem(id = id, name = "Package $id", price = 20.0)

    private fun slovakCatalogue(vararg services: ServiceListItem) {
        coEvery { catalogRepo.refresh("svk-id") } coAnswers {
            catalogServicesFlow.value = services.toList()
            catalogPackagesFlow.value = emptyList()
            catalogCountryFlow.value = "svk-id"
            catalogLoadedFlow.value = true
            ApiResult.Success(Unit)
        }
    }

    private fun loadedCatalogue(countryId: String?, services: List<String>, packages: List<String>) {
        catalogServicesFlow.value = services.map(::service)
        catalogPackagesFlow.value = packages.map(::pkg)
        catalogCountryFlow.value = countryId
        catalogLoadedFlow.value = true
    }

    private fun sourceOrder(
        services: List<String>,
        packages: List<String> = emptyList(),
        rooms: Int = 3,
        bathrooms: Int = 2,
        cleaningDateTime: String? = null,
        dirtinessLevel: DirtinessLevel = DirtinessLevel.Normal,
    ) {
        coEvery { orderRepo.getById("ord-7") } returns ApiResult.Success(
            OrderDetailDto(
                id = "ord-7",
                rooms = rooms,
                bathrooms = bathrooms,
                cleaningDateTime = cleaningDateTime,
                dirtinessLevel = dirtinessLevel,
                totalPrice = 100.0,
                originalSubtotal = 100.0,
                appliedDiscountSource = 0,
                selectedServices = services.map { OrderServiceDetailsDto(id = it, name = "Service $it") },
                selectedPackages = packages.map { OrderPackageDetailsDto(id = it, name = "Package $it") },
            ),
        )
    }

    private val template = RecurringBookingTemplateDto(
        id = "tpl-1",
        frequency = 1,
        dayOfWeek = 4,
        timeOfDay = "10:00",
        rooms = 2,
        bathrooms = 1,
        savedAddressId = "addr-1",
        paymentType = 1,
        startsOn = "2026-07-01T00:00:00Z",
        isActive = true,
        requiresPaymentMethodChange = false,
    )

    @Test
    fun `starts Idle`() = runTest {
        val vm = viewModel()
        advanceUntilIdle()
        assertEquals(ActionState.Idle, vm.submitState.value)
    }

    @Test
    fun `submit success emits one-shot completion effect and returns to Idle`() = runTest {
        coEvery { recurringRepo.create(any()) } returns ApiResult.Success(template)

        val vm = viewModel()
        advanceUntilIdle()
        fillValidForm(vm)

        vm.submitted.test {
            vm.submit()
            advanceUntilIdle()
            awaitItem()
        }
        assertEquals(ActionState.Idle, vm.submitState.value)
        coVerify(exactly = 1) { recurringRepo.create(any()) }
    }

    @Test
    fun `submit failure surfaces ActionState Error and stays silent on no effect`() = runTest {
        coEvery { recurringRepo.create(any()) } returns
            ApiResult.Error(ApiError.Server(statusCode = 500, message = "server boom"))

        val vm = viewModel()
        advanceUntilIdle()
        fillValidForm(vm)

        vm.submit()
        advanceUntilIdle()

        assertTrue(vm.submitState.value is ActionState.Error)
    }

    @Test
    fun `disabled while submitting then re-entry guarded`() = runTest {
        val gate = CompletableDeferred<ApiResult<RecurringBookingTemplateDto>>()
        coEvery { recurringRepo.create(any()) } coAnswers { gate.await() }

        val vm = viewModel()
        advanceUntilIdle()
        fillValidForm(vm)

        vm.submit()
        runCurrent()
        assertEquals(ActionState.Submitting, vm.submitState.value)

        vm.submit()
        runCurrent()

        gate.complete(ApiResult.Success(template))
        advanceUntilIdle()

        coVerify(exactly = 1) { recurringRepo.create(any()) }
        assertEquals(ActionState.Idle, vm.submitState.value)
    }

    @Test
    fun `incomplete form does not submit`() = runTest {
        val vm = viewModel()
        advanceUntilIdle()

        vm.submit()
        advanceUntilIdle()

        coVerify(exactly = 0) { recurringRepo.create(any()) }
        assertEquals(ActionState.Idle, vm.submitState.value)
    }

    private val editableTemplate = template.copy(
        frequency = RecurrenceFrequency.Biweekly.code,
        dayOfWeek = 2,
        timeOfDay = "14:30",
        rooms = 5,
        bathrooms = 3,
        savedAddressId = "addr-9",
        selectedServiceIds = listOf("svc-7"),
        selectedPackageIds = listOf("pkg-3"),
        paymentType = 2,
        startsOn = "2026-09-15T00:00:00Z",
        dirtinessLevel = DirtinessLevel.Heavy,
    )

    @Test
    fun `edit mode prefills every field from the cached template`() = runTest {
        templatesFlow.value = listOf(editableTemplate)

        val vm = viewModel(templateId = "tpl-1")
        advanceUntilIdle()

        val state = vm.state.value
        assertTrue(vm.isEditing)
        assertEquals(RecurrenceFrequency.Biweekly, state.frequency)
        assertEquals(2, state.dayOfWeek)
        assertEquals("14:30", state.timeOfDay)
        assertEquals(5, state.rooms)
        assertEquals(3, state.bathrooms)
        assertEquals("addr-9", state.savedAddressId)
        assertEquals(setOf("svc-7"), state.selectedServiceIds)
        assertEquals(setOf("pkg-3"), state.selectedPackageIds)
        assertEquals(2, state.paymentType)
        assertEquals("2026-09-15T00:00:00Z", state.startsOnIso)
        assertEquals(DirtinessLevel.Heavy, state.dirtinessLevel)
    }

    @Test
    fun `edit mode refreshes when the template is not cached yet`() = runTest {
        coEvery { recurringRepo.refresh() } coAnswers {
            templatesFlow.value = listOf(editableTemplate)
            ApiResult.Success(Unit)
        }

        val vm = viewModel(templateId = "tpl-1")
        advanceUntilIdle()

        assertEquals("addr-9", vm.state.value.savedAddressId)
        coVerify(exactly = 1) { recurringRepo.refresh() }
    }

    @Test
    fun `edit mode submits an update carrying the template id and never creates`() = runTest {
        templatesFlow.value = listOf(editableTemplate)
        coEvery { recurringRepo.update(any()) } returns ApiResult.Success(editableTemplate)

        val vm = viewModel(templateId = "tpl-1")
        advanceUntilIdle()
        vm.setRooms(4)

        vm.submitted.test {
            vm.submit()
            advanceUntilIdle()
            awaitItem()
        }

        val request = slot<UpdateRecurringBookingRequest>()
        coVerify(exactly = 1) { recurringRepo.update(capture(request)) }
        coVerify(exactly = 0) { recurringRepo.create(any()) }
        assertEquals("tpl-1", request.captured.templateId)
        assertEquals(4, request.captured.rooms)
        assertEquals("addr-9", request.captured.savedAddressId)
        assertEquals(listOf("svc-7"), request.captured.selectedServiceIds)
        assertEquals(listOf("pkg-3"), request.captured.selectedPackageIds)
        assertEquals("2026-09-15T00:00:00Z", request.captured.startsOn)
        assertEquals(DirtinessLevel.Heavy, request.captured.dirtinessLevel)
        assertEquals(ActionState.Idle, vm.submitState.value)
    }

    @Test
    fun `edit mode update failure surfaces ActionState Error`() = runTest {
        templatesFlow.value = listOf(editableTemplate)
        coEvery { recurringRepo.update(any()) } returns
            ApiResult.Error(ApiError.Server(statusCode = 500, message = "server boom"))

        val vm = viewModel(templateId = "tpl-1")
        advanceUntilIdle()

        vm.submit()
        advanceUntilIdle()

        assertTrue(vm.submitState.value is ActionState.Error)
    }

    @Test
    fun `edit mode with an unresolvable template never submits defaults over the schedule`() = runTest {
        val vm = viewModel(templateId = "missing")
        advanceUntilIdle()

        vm.submit()
        advanceUntilIdle()

        coVerify(exactly = 0) { recurringRepo.update(any()) }
        coVerify(exactly = 0) { recurringRepo.create(any()) }
    }

    @Test
    fun `edit mode echoes the stored end date back so the update cannot erase it`() = runTest {
        templatesFlow.value = listOf(editableTemplate.copy(endsOn = "2026-12-31T00:00:00Z"))
        coEvery { recurringRepo.update(any()) } returns ApiResult.Success(editableTemplate)

        val vm = viewModel(templateId = "tpl-1")
        advanceUntilIdle()
        vm.setRooms(4)
        vm.submit()
        advanceUntilIdle()

        val request = slot<UpdateRecurringBookingRequest>()
        coVerify(exactly = 1) { recurringRepo.update(capture(request)) }
        assertEquals("2026-12-31T00:00:00Z", request.captured.endsOn)
    }

    // The server refuses a start on or after the end date (`recurring_template.ends_on_before_start`),
    // and this form cannot move the end date, so the picker stops the day before it — as the web does.

    @OptIn(ExperimentalMaterial3Api::class)
    @Test
    fun `edit mode offers no start on or after the stored end date`() = runTest {
        templatesFlow.value = listOf(editableTemplate.copy(endsOn = "2026-12-31T00:00:00Z"))

        val vm = viewModel(templateId = "tpl-1")
        advanceUntilIdle()

        val latest = vm.state.value.latestStartDate(TimeZone.UTC)
        assertEquals(LocalDate(2026, 12, 30), latest)
        val dates = StartsOnSelectableDates(today = LocalDate(2026, 9, 1), latest = latest)
        assertTrue(dates.isSelectableDate(utcMidnight(LocalDate(2026, 12, 30))))
        assertFalse(dates.isSelectableDate(utcMidnight(LocalDate(2026, 12, 31))))
        assertFalse(dates.isSelectableDate(utcMidnight(LocalDate(2027, 1, 15))))
    }

    @OptIn(ExperimentalMaterial3Api::class)
    @Test
    fun `a schedule with no end date leaves the start open after today`() = runTest {
        templatesFlow.value = listOf(editableTemplate)

        val vm = viewModel(templateId = "tpl-1")
        advanceUntilIdle()

        val latest = vm.state.value.latestStartDate(TimeZone.UTC)
        assertNull(latest)
        val dates = StartsOnSelectableDates(today = LocalDate(2026, 9, 1), latest = latest)
        assertTrue(dates.isSelectableDate(utcMidnight(LocalDate(2030, 1, 1))))
        assertFalse(dates.isSelectableDate(utcMidnight(LocalDate(2026, 8, 31))))
    }

    private fun utcMidnight(date: LocalDate): Long = date.atStartOfDayIn(TimeZone.UTC).toEpochMilliseconds()

    // Every basket validator refuses a home above BookingPolicy.MaxRooms / MaxBathrooms.

    @Test
    fun `the size steppers stop at the largest home the server accepts`() = runTest {
        val vm = viewModel()
        advanceUntilIdle()

        vm.setRooms(PropertySize.MAX_ROOMS + 1)
        vm.setBathrooms(PropertySize.MAX_BATHROOMS + 1)

        assertEquals(PropertySize.MAX_ROOMS, vm.state.value.rooms)
        assertEquals(PropertySize.MAX_BATHROOMS, vm.state.value.bathrooms)
    }

    @Test
    fun `a schedule made from a larger past order starts at the largest home the server accepts`() = runTest {
        sourceOrder(services = listOf("svc-1"), rooms = PropertySize.MAX_ROOMS + 3, bathrooms = PropertySize.MAX_BATHROOMS + 2)

        val vm = viewModel(orderId = "ord-7")
        advanceUntilIdle()

        assertEquals(PropertySize.MAX_ROOMS, vm.state.value.rooms)
        assertEquals(PropertySize.MAX_BATHROOMS, vm.state.value.bathrooms)
    }

    // The update replaces the favourite cleaner with whatever it is sent, so the loaded one rides along;
    // when the server no longer accepts them, dropping them is the customer's call, never the form's.

    private val notEligible = ApiResult.Error(
        ApiError.BadRequest(
            message = "The selected cleaner isn't available for this order.",
            errorKey = "order.preferred_employee.not_eligible",
        ),
    )

    @Test
    fun `edit mode echoes the stored favourite cleaner back so the update cannot erase it`() = runTest {
        templatesFlow.value = listOf(editableTemplate.copy(preferredEmployeeId = "emp-7"))
        coEvery { recurringRepo.update(any()) } returns ApiResult.Success(editableTemplate)

        val vm = viewModel(templateId = "tpl-1")
        advanceUntilIdle()
        vm.setRooms(4)
        vm.submit()
        advanceUntilIdle()

        val request = slot<UpdateRecurringBookingRequest>()
        coVerify(exactly = 1) { recurringRepo.update(capture(request)) }
        assertEquals("emp-7", request.captured.preferredEmployeeId)
    }

    @Test
    fun `a refused favourite cleaner is kept and offered back, and nothing is resent on its own`() = runTest {
        templatesFlow.value = listOf(editableTemplate.copy(preferredEmployeeId = "emp-7"))
        coEvery { recurringRepo.update(any()) } returns notEligible

        val vm = viewModel(templateId = "tpl-1")
        advanceUntilIdle()
        vm.submit()
        advanceUntilIdle()

        assertEquals(true, vm.preferredCleanerRefused.value)
        assertEquals("emp-7", vm.state.value.preferredEmployeeId)
        assertTrue(vm.submitState.value is ActionState.Error)
        coVerify(exactly = 1) { recurringRepo.update(any()) }
        verify(exactly = 0) { snackbar.showError(any<ApiError>()) }
    }

    @Test
    fun `saving without the favourite cleaner resends the same update with only the cleaner cleared`() = runTest {
        templatesFlow.value = listOf(editableTemplate.copy(preferredEmployeeId = "emp-7", endsOn = "2026-12-31T00:00:00Z"))
        val sent = mutableListOf<UpdateRecurringBookingRequest>()
        coEvery { recurringRepo.update(capture(sent)) } returnsMany listOf(notEligible, ApiResult.Success(editableTemplate))
        val vm = viewModel(templateId = "tpl-1")
        advanceUntilIdle()
        vm.setRooms(4)
        vm.submit()
        advanceUntilIdle()

        vm.submitted.test {
            vm.saveWithoutPreferredCleaner()
            advanceUntilIdle()
            awaitItem()
        }

        assertEquals(2, sent.size)
        assertEquals(null, sent[1].preferredEmployeeId)
        assertEquals(sent[0].copy(preferredEmployeeId = null), sent[1])
        assertEquals(false, vm.preferredCleanerRefused.value)
        assertEquals(ActionState.Idle, vm.submitState.value)
    }

    @Test
    fun `a new schedule carries the favourite cleaner the customer picked`() = runTest {
        coEvery { recurringRepo.create(any()) } returns ApiResult.Success(template)
        val vm = viewModel()
        advanceUntilIdle()
        fillValidForm(vm)
        vm.setPreferredEmployeeId("emp-7")

        vm.submit()
        advanceUntilIdle()

        val request = slot<CreateRecurringBookingRequest>()
        coVerify(exactly = 1) { recurringRepo.create(capture(request)) }
        assertEquals("emp-7", request.captured.preferredEmployeeId)
    }

    @Test
    fun `a new schedule with no pick names no favourite cleaner`() = runTest {
        coEvery { recurringRepo.create(any()) } returns ApiResult.Success(template)
        val vm = viewModel()
        advanceUntilIdle()
        fillValidForm(vm)

        vm.submit()
        advanceUntilIdle()

        val request = slot<CreateRecurringBookingRequest>()
        coVerify(exactly = 1) { recurringRepo.create(capture(request)) }
        assertNull(request.captured.preferredEmployeeId)
    }

    @Test
    fun `a refused favourite cleaner on a new schedule is kept and offered back`() = runTest {
        coEvery { recurringRepo.create(any()) } returns notEligible
        val vm = viewModel()
        advanceUntilIdle()
        fillValidForm(vm)
        vm.setPreferredEmployeeId("emp-7")

        vm.submit()
        advanceUntilIdle()

        assertEquals(true, vm.preferredCleanerRefused.value)
        assertEquals("emp-7", vm.state.value.preferredEmployeeId)
        assertTrue(vm.submitState.value is ActionState.Error)
        coVerify(exactly = 1) { recurringRepo.create(any()) }
        verify(exactly = 0) { snackbar.showError(any<ApiError>()) }
    }

    @Test
    fun `saving a new schedule without the favourite cleaner resends the same create with only the cleaner cleared`() = runTest {
        val sent = mutableListOf<CreateRecurringBookingRequest>()
        coEvery { recurringRepo.create(capture(sent)) } returnsMany listOf(notEligible, ApiResult.Success(template))
        val vm = viewModel()
        advanceUntilIdle()
        fillValidForm(vm)
        vm.setPreferredEmployeeId("emp-7")
        vm.submit()
        advanceUntilIdle()

        vm.submitted.test {
            vm.saveWithoutPreferredCleaner()
            advanceUntilIdle()
            awaitItem()
        }

        assertEquals(2, sent.size)
        assertEquals(sent[0].copy(preferredEmployeeId = null), sent[1])
        assertEquals(false, vm.preferredCleanerRefused.value)
        assertEquals(ActionState.Idle, vm.submitState.value)
    }

    @Test
    fun `an edit sends the favourite cleaner the customer changed to, and none once cleared`() = runTest {
        templatesFlow.value = listOf(editableTemplate.copy(preferredEmployeeId = "emp-7"))
        val sent = mutableListOf<UpdateRecurringBookingRequest>()
        coEvery { recurringRepo.update(capture(sent)) } returns ApiResult.Success(editableTemplate)
        val vm = viewModel(templateId = "tpl-1")
        advanceUntilIdle()

        vm.setPreferredEmployeeId("emp-9")
        vm.submit()
        advanceUntilIdle()
        vm.setPreferredEmployeeId(null)
        vm.submit()
        advanceUntilIdle()

        assertEquals(listOf("emp-9", null), sent.map { it.preferredEmployeeId })
    }

    @Test
    fun `a new pick withdraws the refusal of the previous one`() = runTest {
        coEvery { recurringRepo.create(any()) } returns notEligible
        val vm = viewModel()
        advanceUntilIdle()
        fillValidForm(vm)
        vm.setPreferredEmployeeId("emp-7")
        vm.submit()
        advanceUntilIdle()
        assertTrue(vm.preferredCleanerRefused.value)

        vm.setPreferredEmployeeId("emp-9")

        assertFalse(vm.preferredCleanerRefused.value)
        assertEquals("emp-9", vm.state.value.preferredEmployeeId)
    }

    @Test
    fun `submit failure shows the backend message, not a generic key`() = runTest {
        templatesFlow.value = listOf(editableTemplate)
        coEvery { recurringRepo.update(any()) } returns
            ApiResult.Error(ApiError.BadRequest(message = plusRefusal, errorKey = "recurring_booking.membership_required"))

        val vm = viewModel(templateId = "tpl-1")
        advanceUntilIdle()
        vm.submit()
        advanceUntilIdle()

        verify(exactly = 1) { snackbar.showError(match<ApiError> { it.getUserMessage() == plusRefusal }) }
        verify(exactly = 0) { snackbar.showErrorKey(any()) }
        assertEquals(false, vm.preferredCleanerRefused.value)
    }

    @Test
    fun `create failure shows the backend message, not a generic key`() = runTest {
        coEvery { recurringRepo.create(any()) } returns
            ApiResult.Error(ApiError.BadRequest(message = plusRefusal, errorKey = "recurring_booking.membership_required"))

        val vm = viewModel()
        advanceUntilIdle()
        fillValidForm(vm)

        vm.submit()
        advanceUntilIdle()

        verify(exactly = 1) { snackbar.showError(match<ApiError> { it.getUserMessage() == plusRefusal }) }
        verify(exactly = 0) { snackbar.showErrorKey(any()) }
    }

    @Test
    fun `submit failure on a transport error stays silent — the interceptor owns that toast`() = runTest {
        templatesFlow.value = listOf(editableTemplate)
        coEvery { recurringRepo.update(any()) } returns
            ApiResult.Error(ApiError.Network("Check your internet connection and try again."))

        val vm = viewModel(templateId = "tpl-1")
        advanceUntilIdle()
        vm.submit()
        advanceUntilIdle()

        verify(exactly = 0) { snackbar.showError(any<String>()) }
        verify(exactly = 0) { snackbar.showErrorKey(any()) }
        assertTrue(vm.submitState.value is ActionState.Error)
    }

    // ── the market rule — the service address's country prices the template ──
    //
    // Owner ruling 2026-09-12: an order is priced in the currency of the service address's country.
    // The wizard's picks are a saved address, so its country is the market: the catalogue is re-read
    // for it and a pick the new market does not offer is dropped with a notice, not refused at submit.

    @Test
    fun `picking a saved address reloads the catalogue for that address's country`() = runTest {
        addressesFlow.value = listOf(address("addr-legacy", countryId = null), address("addr-sk", "svk-id"))
        slovakCatalogue(service("svc-1"))

        val vm = viewModel()
        advanceUntilIdle()
        coVerify(exactly = 0) { catalogRepo.refresh("svk-id") }

        vm.setSavedAddressId("addr-sk")
        advanceUntilIdle()

        coVerify(exactly = 1) { catalogRepo.refresh("svk-id") }
    }

    @Test
    fun `the default saved address sets the market on entry`() = runTest {
        addressesFlow.value = listOf(address("addr-cz", countryId = null), address("addr-sk", "svk-id", isDefault = true))
        slovakCatalogue(service("svc-1"))

        viewModel()
        advanceUntilIdle()

        coVerify(exactly = 1) { catalogRepo.refresh("svk-id") }
    }

    @Test
    fun `an address in the market the catalogue already answers for reloads nothing`() = runTest {
        addressesFlow.value = listOf(address("addr-sk", "svk-id"), address("addr-sk-2", "svk-id"))
        catalogCountryFlow.value = "svk-id"

        val vm = viewModel()
        advanceUntilIdle()
        vm.setSavedAddressId("addr-sk-2")
        advanceUntilIdle()

        coVerify(exactly = 0) { catalogRepo.refresh("svk-id") }
    }

    @Test
    fun `an address change prunes what the new market does not offer and says so`() = runTest {
        addressesFlow.value = listOf(address("addr-legacy", countryId = null), address("addr-sk", "svk-id"))
        slovakCatalogue(service("svc-1"))

        val vm = viewModel()
        advanceUntilIdle()
        vm.toggleService("svc-1")
        vm.toggleService("svc-2")
        vm.togglePackage("pkg-1")

        vm.setSavedAddressId("addr-sk")
        advanceUntilIdle()

        assertEquals(setOf("svc-1"), vm.state.value.selectedServiceIds)
        assertEquals(emptySet<String>(), vm.state.value.selectedPackageIds)
        verify(exactly = 1) { snackbar.showInfo(marketNotice) }
    }

    @Test
    fun `an address change that drops nothing stays quiet`() = runTest {
        addressesFlow.value = listOf(address("addr-legacy", countryId = null), address("addr-sk", "svk-id"))
        slovakCatalogue(service("svc-1"))

        val vm = viewModel()
        advanceUntilIdle()
        vm.toggleService("svc-1")

        vm.setSavedAddressId("addr-sk")
        advanceUntilIdle()

        assertEquals(setOf("svc-1"), vm.state.value.selectedServiceIds)
        verify(exactly = 0) { snackbar.showInfo(any<String>()) }
    }

    /** A reload that failed says nothing about the market; pruning against it would empty the basket. */
    @Test
    fun `a failed reload keeps the picks and the old catalogue`() = runTest {
        addressesFlow.value = listOf(address("addr-legacy", countryId = null), address("addr-sk", "svk-id"))
        coEvery { catalogRepo.refresh("svk-id") } returns ApiResult.Error(ApiError.Network("boom"))

        val vm = viewModel()
        advanceUntilIdle()
        vm.toggleService("svc-1")
        vm.toggleService("svc-2")

        vm.setSavedAddressId("addr-sk")
        advanceUntilIdle()

        assertEquals(setOf("svc-1", "svc-2"), vm.state.value.selectedServiceIds)
        verify(exactly = 0) { snackbar.showInfo(any<String>()) }
    }

    /** The home carousel prices from this same repository; a Slovak template must not leave it in euros. */
    @Test
    fun `leaving the wizard after a foreign market returns the catalogue to the platform default`() = runTest {
        addressesFlow.value = listOf(address("addr-sk", "svk-id", isDefault = true))
        slovakCatalogue(service("svc-1"))

        val vm = viewModel()
        advanceUntilIdle()
        assertEquals("svk-id", catalogCountryFlow.value)
        clearMocks(catalogRepo, answers = false)

        ViewModelStore().apply { put("wizard", vm) }.clear()
        advanceUntilIdle()

        coVerify(exactly = 1) { catalogRepo.refresh(null) }
    }

    @Test
    fun `leaving the wizard on the platform default reloads nothing`() = runTest {
        val vm = viewModel()
        advanceUntilIdle()
        clearMocks(catalogRepo, answers = false)

        ViewModelStore().apply { put("wizard", vm) }.clear()
        advanceUntilIdle()

        coVerify(exactly = 0) { catalogRepo.refresh(any()) }
    }

    @Test
    fun `the wizard opens on its first step and cannot step back`() = runTest {
        val vm = viewModel()
        advanceUntilIdle()

        assertEquals(1, vm.step.value)
        assertEquals(false, vm.canStepBack.value)
    }

    @Test
    fun `stepping forward then back returns exactly one step`() = runTest {
        val vm = viewModel()
        advanceUntilIdle()

        vm.nextStep()
        vm.nextStep()
        runCurrent()
        assertEquals(3, vm.step.value)
        assertEquals(true, vm.canStepBack.value)

        vm.previousStep()
        runCurrent()
        assertEquals(2, vm.step.value)
        assertEquals(true, vm.canStepBack.value)

        vm.previousStep()
        runCurrent()
        assertEquals(1, vm.step.value)
        assertEquals(false, vm.canStepBack.value)
    }

    @Test
    fun `the steps are clamped at both ends`() = runTest {
        val vm = viewModel()
        advanceUntilIdle()

        vm.previousStep()
        assertEquals(1, vm.step.value)

        repeat(CreateRecurringViewModel.TOTAL_STEPS + 2) { vm.nextStep() }
        assertEquals(CreateRecurringViewModel.TOTAL_STEPS, vm.step.value)
    }

    @Test
    fun `the exposed address and catalogue flows mirror the repositories`() = runTest {
        val vm = viewModel()
        advanceUntilIdle()
        assertEquals(emptyList<UserAddress>(), vm.savedAddresses.value)
        assertEquals(emptyList<ServiceListItem>(), vm.services.value)
        assertEquals(emptyList<PackageListItem>(), vm.packages.value)

        addressesFlow.value = listOf(address("addr-1", countryId = null))
        catalogServicesFlow.value = listOf(service("svc-1"))
        catalogPackagesFlow.value = listOf(pkg("pkg-1"))
        advanceUntilIdle()

        assertEquals(listOf("addr-1"), vm.savedAddresses.value.map { it.serverId })
        assertEquals(listOf("svc-1"), vm.services.value.map { it.id })
        assertEquals(listOf("pkg-1"), vm.packages.value.map { it.id })
    }

    @Test
    fun `a prefilled pick the address's market does not offer is pruned with a notice`() = runTest {
        addressesFlow.value = listOf(address("addr-sk", "svk-id", isDefault = true))
        loadedCatalogue("svk-id", services = listOf("s-1"), packages = emptyList())
        sourceOrder(services = listOf("s-1", "s-2"), packages = listOf("p-1"))

        val vm = viewModel(orderId = "ord-7")
        advanceUntilIdle()

        coVerify(exactly = 0) { catalogRepo.refresh("svk-id") }
        assertEquals(setOf("s-1"), vm.state.value.selectedServiceIds)
        assertEquals(emptySet<String>(), vm.state.value.selectedPackageIds)
        assertEquals(3, vm.state.value.rooms)
        verify(exactly = 1) { snackbar.showInfo(marketNotice) }
    }

    @Test
    fun `a prefilled pick the address's market offers survives`() = runTest {
        addressesFlow.value = listOf(address("addr-sk", "svk-id", isDefault = true))
        loadedCatalogue("svk-id", services = listOf("s-1", "s-2"), packages = listOf("p-1"))
        sourceOrder(services = listOf("s-2"), packages = listOf("p-1"))

        val vm = viewModel(orderId = "ord-7")
        advanceUntilIdle()

        assertEquals(setOf("s-2"), vm.state.value.selectedServiceIds)
        assertEquals(setOf("p-1"), vm.state.value.selectedPackageIds)
    }

    @Test
    fun `a prefilled selection the market fully offers raises no notice`() = runTest {
        addressesFlow.value = listOf(address("addr-sk", "svk-id", isDefault = true))
        loadedCatalogue("svk-id", services = listOf("s-1"), packages = emptyList())
        sourceOrder(services = listOf("s-1"))

        val vm = viewModel(orderId = "ord-7")
        advanceUntilIdle()

        assertEquals(setOf("s-1"), vm.state.value.selectedServiceIds)
        verify(exactly = 0) { snackbar.showInfo(any<String>()) }
    }

    /** The catalogue on hand is another market's; judging the picks against it would drop what the reload may price. */
    @Test
    fun `a prefill landing before the market reload leaves the pruning to the reload`() = runTest {
        addressesFlow.value = listOf(address("addr-sk", "svk-id", isDefault = true))
        loadedCatalogue(null, services = listOf("s-9"), packages = emptyList())
        val reload = CompletableDeferred<Unit>()
        coEvery { catalogRepo.refresh("svk-id") } coAnswers {
            reload.await()
            catalogServicesFlow.value = listOf(service("s-1"))
            catalogPackagesFlow.value = emptyList()
            catalogCountryFlow.value = "svk-id"
            ApiResult.Success(Unit)
        }
        sourceOrder(services = listOf("s-1", "s-2"))

        val vm = viewModel(orderId = "ord-7")
        advanceUntilIdle()
        assertEquals(setOf("s-1", "s-2"), vm.state.value.selectedServiceIds)
        verify(exactly = 0) { snackbar.showInfo(any<String>()) }

        reload.complete(Unit)
        advanceUntilIdle()

        assertEquals(setOf("s-1"), vm.state.value.selectedServiceIds)
        verify(exactly = 1) { snackbar.showInfo(marketNotice) }
    }

    @Test
    fun `step one advances only on a start the picker offers`() = runTest {
        val vm = viewModel()
        advanceUntilIdle()
        assertEquals(true, vm.canAdvance.value)

        listOf("", "07:45", "09:07", "20:00", "03:07").forEach { start ->
            vm.setTimeOfDay(start)
            runCurrent()
            assertEquals("\"$start\" is not an offered start", false, vm.canAdvance.value)
        }

        listOf("08:00", "09:30", "19:45").forEach { start ->
            vm.setTimeOfDay(start)
            runCurrent()
            assertEquals("\"$start\" is an offered start", true, vm.canAdvance.value)
        }
    }

    // ── the start-time window: the server refuses a schedule off 08:00–19:45 or off the quarter-hour ──

    @Test
    fun `the picker offers the server's window on its 15-minute grid`() {
        val starts = CreateRecurringViewModel.START_TIMES

        assertEquals(48, starts.size)
        assertEquals(listOf("08:00", "08:15", "08:30", "08:45", "09:00"), starts.take(5))
        assertEquals("19:45", starts.last())
        assertTrue(starts.none { it < "08:00" || it >= "20:00" })
    }

    private fun localStart(hour: Int, minute: Int): String =
        LocalDateTime(2026, 7, 2, hour, minute).toInstant(TimeZone.currentSystemDefault()).toString()

    @Test
    fun `a schedule made from a past order takes the order's start when the picker offers it`() = runTest {
        sourceOrder(services = listOf("svc-1"), cleaningDateTime = localStart(9, 45))

        val vm = viewModel(orderId = "ord-7")
        advanceUntilIdle()

        assertEquals("09:45", vm.state.value.timeOfDay)
    }

    @Test
    fun `a past order's start the picker does not offer leaves the default start`() = runTest {
        listOf(localStart(6, 30), localStart(9, 7)).forEach { cleaningAt ->
            sourceOrder(services = listOf("svc-1"), cleaningDateTime = cleaningAt)

            val vm = viewModel(orderId = "ord-7")
            advanceUntilIdle()

            assertEquals(CreateRecurringFormState().timeOfDay, vm.state.value.timeOfDay)
            assertEquals(true, vm.canAdvance.value)
        }
    }

    @Test
    fun `a stored start off the grid goes back only once an offered start is picked`() = runTest {
        templatesFlow.value = listOf(editableTemplate.copy(timeOfDay = "03:07"))
        coEvery { recurringRepo.update(any()) } returns ApiResult.Success(editableTemplate)

        val vm = viewModel(templateId = "tpl-1")
        advanceUntilIdle()

        assertEquals(false, vm.canAdvance.value)
        vm.submit()
        advanceUntilIdle()
        coVerify(exactly = 0) { recurringRepo.update(any()) }

        vm.setTimeOfDay("08:15")
        runCurrent()
        assertEquals(true, vm.canAdvance.value)
        vm.submit()
        advanceUntilIdle()

        val request = slot<UpdateRecurringBookingRequest>()
        coVerify(exactly = 1) { recurringRepo.update(capture(request)) }
        assertEquals("08:15", request.captured.timeOfDay)
    }

    @Test
    fun `a schedule the server refuses for the start window says why and stays on the form`() = runTest {
        val refusal = ApiError.BadRequest(
            message = "Pick a start between 08:00 and 19:45 local time, on the quarter-hour, no more than 60 days ahead.",
            errorKey = "order.cleaning_date.outside_booking_window",
        )
        coEvery { recurringRepo.create(any()) } returns ApiResult.Error(refusal)

        val vm = viewModel()
        advanceUntilIdle()
        fillValidForm(vm)

        vm.submitted.test {
            vm.submit()
            advanceUntilIdle()
            expectNoEvents()
        }

        verify(exactly = 1) { snackbar.showError(refusal) }
        assertEquals(ActionState.Error(refusal.message), vm.submitState.value)
    }

    @Test
    fun `step two advances only with a service or a package picked`() = runTest {
        val vm = viewModel()
        advanceUntilIdle()
        vm.setDirtinessLevel(DirtinessLevel.Normal)
        vm.nextStep()
        runCurrent()
        assertEquals(false, vm.canAdvance.value)

        vm.toggleService("svc-1")
        runCurrent()
        assertEquals(true, vm.canAdvance.value)

        vm.toggleService("svc-1")
        vm.togglePackage("pkg-1")
        runCurrent()
        assertEquals(true, vm.canAdvance.value)

        vm.togglePackage("pkg-1")
        runCurrent()
        assertEquals(false, vm.canAdvance.value)
    }

    @Test
    fun `step two advances only once the customer has chosen a dirtiness level`() = runTest {
        val vm = viewModel()
        advanceUntilIdle()
        vm.nextStep()
        vm.toggleService("svc-1")
        runCurrent()
        assertEquals(false, vm.canAdvance.value)

        vm.setDirtinessLevel(DirtinessLevel.Increased)
        runCurrent()

        assertEquals(true, vm.canAdvance.value)
    }

    @Test
    fun `a new schedule without a chosen dirtiness level is never created`() = runTest {
        coEvery { recurringRepo.create(any()) } returns ApiResult.Success(template)
        val vm = viewModel()
        advanceUntilIdle()
        vm.setSavedAddressId("addr-1")
        vm.toggleService("svc-1")
        vm.setStartsOn("2026-07-01T00:00:00Z")
        advanceUntilIdle()

        vm.submit()
        advanceUntilIdle()

        assertEquals(false, vm.isValid.value)
        coVerify(exactly = 0) { recurringRepo.create(any()) }
    }

    @Test
    fun `a new schedule is created at the dirtiness level the customer chose`() = runTest {
        coEvery { recurringRepo.create(any()) } returns ApiResult.Success(template)
        val vm = viewModel()
        advanceUntilIdle()
        fillValidForm(vm)
        vm.setDirtinessLevel(DirtinessLevel.Heavy)

        vm.submit()
        advanceUntilIdle()

        val request = slot<CreateRecurringBookingRequest>()
        coVerify(exactly = 1) { recurringRepo.create(capture(request)) }
        assertEquals(DirtinessLevel.Heavy, request.captured.dirtinessLevel)
    }

    /** A one-off clean says little about how a home looks between visits, so a schedule asks again. */
    @Test
    fun `a schedule made from a past order still asks for the dirtiness level`() = runTest {
        sourceOrder(services = listOf("svc-1"), dirtinessLevel = DirtinessLevel.Heavy)

        val vm = viewModel(orderId = "ord-7")
        advanceUntilIdle()

        assertNull(vm.state.value.dirtinessLevel)
    }

    @Test
    fun `step three advances only with an address and a start date`() = runTest {
        val vm = viewModel()
        advanceUntilIdle()
        vm.setEarlyPerformanceRequested(true)
        vm.nextStep()
        vm.nextStep()
        runCurrent()
        assertEquals(false, vm.canAdvance.value)

        vm.setSavedAddressId("addr-1")
        runCurrent()
        assertEquals(false, vm.canAdvance.value)

        vm.setStartsOn("2026-07-01T00:00:00Z")
        runCurrent()
        assertEquals(true, vm.canAdvance.value)

        vm.setSavedAddressId("")
        runCurrent()
        assertEquals(false, vm.canAdvance.value)
    }

    @Test
    fun `create mode never calls update`() = runTest {
        coEvery { recurringRepo.create(any()) } returns ApiResult.Success(template)

        val vm = viewModel()
        advanceUntilIdle()
        fillValidForm(vm)

        vm.submit()
        advanceUntilIdle()

        coVerify(exactly = 0) { recurringRepo.update(any()) }
        assertTrue(!vm.isEditing)
    }

    // ── cash — only when the server quotes one cleaner for the schedule's selection ──

    private fun crewQuote(requiredEmployees: Int) = QuoteOrderResponse(
        totalPrice = 1000.0,
        finalPriceAfterDiscount = 1000.0,
        originalSubtotal = 1000.0,
        appliedDiscountSource = 0,
        currencyId = "cur-1",
        currencyCode = "CZK",
        servicesSubtotal = 1000.0,
        packagesSubtotal = 0.0,
        extrasSubtotal = 0.0,
        expressSurchargeApplied = false,
        expressSurchargeAmount = 0.0,
        expressSurchargeWaivedByMembership = false,
        requiredEmployees = requiredEmployees,
    )

    private fun crewOf(requiredEmployees: Int) {
        coEvery { bookingApi.quote(any()) } returns Response.success(crewQuote(requiredEmployees))
    }

    private fun kotlinx.coroutines.test.TestScope.filledForm(requiredEmployees: Int): CreateRecurringViewModel {
        crewOf(requiredEmployees)
        val vm = viewModel()
        advanceUntilIdle()
        fillValidForm(vm)
        advanceUntilIdle()
        return vm
    }

    @Test
    fun `a new schedule is paid by card until the customer chooses otherwise`() = runTest {
        val vm = viewModel()
        advanceUntilIdle()

        assertEquals(CreateRecurringViewModel.PAYMENT_CARD, vm.state.value.paymentType)
    }

    @Test
    fun `cash is offered once the quote says one cleaner does each clean`() = runTest {
        val vm = filledForm(requiredEmployees = 1)

        vm.setPaymentType(CreateRecurringViewModel.PAYMENT_CASH)

        assertEquals(CashEligibility.Available, vm.cashEligibility.value)
        assertEquals(CreateRecurringViewModel.PAYMENT_CASH, vm.state.value.paymentType)
    }

    @Test
    fun `cash is refused with the crew when each clean needs two cleaners`() = runTest {
        val vm = filledForm(requiredEmployees = 2)

        vm.setPaymentType(CreateRecurringViewModel.PAYMENT_CASH)

        assertEquals(CashEligibility.NeedsCard(2), vm.cashEligibility.value)
        assertEquals(CreateRecurringViewModel.PAYMENT_CARD, vm.state.value.paymentType)
    }

    @Test
    fun `the quote prices the form's selection in the address's market`() = runTest {
        addressesFlow.value = listOf(address("addr-1", "svk-id"))
        slovakCatalogue(service("svc-1"))
        val sent = mutableListOf<QuoteOrderCommand>()
        coEvery { bookingApi.quote(capture(sent)) } returns Response.success(crewQuote(1))
        val vm = viewModel()
        advanceUntilIdle()

        fillValidForm(vm)
        vm.setRooms(4)
        advanceUntilIdle()

        val last = sent.last()
        assertEquals(listOf("svc-1"), last.selectedServiceIds)
        assertEquals(4, last.rooms)
        assertEquals("svk-id", last.countryId)
        assertEquals(null, last.cleaningDate)
    }

    /** A heavier level lengthens each clean, so the crew quoted for it decides cash. */
    @Test
    fun `the crew quote prices the chosen dirtiness level and cash follows it`() = runTest {
        val sent = mutableListOf<QuoteOrderCommand>()
        coEvery { bookingApi.quote(capture(sent)) } coAnswers {
            val crew = if (sent.last().dirtinessLevel == DirtinessLevel.Heavy) 2 else 1
            Response.success(crewQuote(crew))
        }
        val vm = viewModel()
        advanceUntilIdle()
        fillValidForm(vm)
        advanceUntilIdle()
        assertEquals(CashEligibility.Available, vm.cashEligibility.value)

        vm.setDirtinessLevel(DirtinessLevel.Heavy)
        advanceUntilIdle()

        assertEquals(DirtinessLevel.Heavy, sent.last().dirtinessLevel)
        assertEquals(CashEligibility.NeedsCard(2), vm.cashEligibility.value)
    }

    @Test
    fun `a cash choice that stops being allowed is taken away, not switched to card`() = runTest {
        val vm = filledForm(requiredEmployees = 1)
        vm.setPaymentType(CreateRecurringViewModel.PAYMENT_CASH)

        crewOf(2)
        vm.toggleService("svc-2")
        advanceUntilIdle()

        assertEquals(null, vm.state.value.paymentType)
        assertEquals(true, vm.cashClearedNotice.value)
        assertEquals(false, vm.isValid.value)
        verify(exactly = 1) { snackbar.showInfoKey(R.string.recurring_cash_cleared) }
    }

    /** A legacy cash schedule the server now skips: the edit is the recovery, and it cannot keep cash. */
    @Test
    fun `editing a cash schedule that now needs two cleaners takes cash away`() = runTest {
        crewOf(2)
        templatesFlow.value = listOf(editableTemplate.copy(paymentType = CreateRecurringViewModel.PAYMENT_CASH))

        val vm = viewModel(templateId = "tpl-1")
        advanceUntilIdle()
        vm.submit()
        advanceUntilIdle()

        assertEquals(null, vm.state.value.paymentType)
        assertEquals(true, vm.cashClearedNotice.value)
        coVerify(exactly = 0) { recurringRepo.update(any()) }
    }

    @Test
    fun `the edit goes out once the customer picks card`() = runTest {
        crewOf(2)
        templatesFlow.value = listOf(editableTemplate.copy(paymentType = CreateRecurringViewModel.PAYMENT_CASH))
        coEvery { recurringRepo.update(any()) } returns ApiResult.Success(editableTemplate)
        val vm = viewModel(templateId = "tpl-1")
        advanceUntilIdle()

        vm.setPaymentType(CreateRecurringViewModel.PAYMENT_CARD)
        vm.submit()
        advanceUntilIdle()

        val request = slot<UpdateRecurringBookingRequest>()
        coVerify(exactly = 1) { recurringRepo.update(capture(request)) }
        assertEquals(CreateRecurringViewModel.PAYMENT_CARD, request.captured.paymentType)
        assertEquals(false, vm.cashClearedNotice.value)
    }

    @Test
    fun `eligible cash is sent as cash`() = runTest {
        coEvery { recurringRepo.create(any()) } returns ApiResult.Success(template)
        val vm = filledForm(requiredEmployees = 1)
        vm.setPaymentType(CreateRecurringViewModel.PAYMENT_CASH)

        vm.submit()
        advanceUntilIdle()

        val request = slot<CreateRecurringBookingRequest>()
        coVerify(exactly = 1) { recurringRepo.create(capture(request)) }
        assertEquals(CreateRecurringViewModel.PAYMENT_CASH, request.captured.paymentType)
    }

    /** The submit re-asks the server; the answer on screen may be older than the crew rules. */
    @Test
    fun `a cash submit whose fresh quote needs two cleaners creates nothing`() = runTest {
        val vm = filledForm(requiredEmployees = 1)
        vm.setPaymentType(CreateRecurringViewModel.PAYMENT_CASH)
        crewOf(2)

        vm.submit()
        advanceUntilIdle()

        coVerify(exactly = 0) { recurringRepo.create(any()) }
        assertEquals(null, vm.state.value.paymentType)
        assertEquals(ActionState.Idle, vm.submitState.value)
    }

    @Test
    fun `a cash submit the server could not quote is held back with the reason`() = runTest {
        val vm = filledForm(requiredEmployees = 1)
        vm.setPaymentType(CreateRecurringViewModel.PAYMENT_CASH)
        coEvery { bookingApi.quote(any()) } throws java.io.IOException("boom")

        vm.submit()
        advanceUntilIdle()

        coVerify(exactly = 0) { recurringRepo.create(any()) }
        verify(exactly = 1) { snackbar.showErrorKey(R.string.recurring_cash_unchecked) }
        assertTrue(vm.submitState.value is ActionState.Error)
        assertEquals(CreateRecurringViewModel.PAYMENT_CASH, vm.state.value.paymentType)
    }

    @Test
    fun `the form without a way to pay cannot be submitted`() = runTest {
        val vm = filledForm(requiredEmployees = 1)
        vm.setPaymentType(CreateRecurringViewModel.PAYMENT_CASH)
        crewOf(2)
        vm.toggleService("svc-2")
        advanceUntilIdle()
        vm.nextStep()
        vm.nextStep()
        runCurrent()

        vm.submit()
        advanceUntilIdle()

        assertEquals(false, vm.canAdvance.value)
        coVerify(exactly = 0) { recurringRepo.create(any()) }
    }

    // ── the booking's terms tick — a new schedule asks for it until both consents cover the texts in force ──

    private fun kotlinx.coroutines.test.TestScope.onStepThree(): CreateRecurringViewModel {
        coEvery { recurringRepo.create(any()) } returns ApiResult.Success(template)
        val vm = viewModel()
        advanceUntilIdle()
        fillValidForm(vm)
        vm.nextStep()
        vm.nextStep()
        runCurrent()
        return vm
    }

    @Test
    fun `a consent to an older text shows the tick and holds the schedule until it is ticked`() = runTest {
        coEvery { consentClient.grantedTypes() } returns setOf(SignupConsentType.PrivacyPolicy)
        val vm = onStepThree()

        assertEquals(true, vm.termsAsked.value)
        assertEquals(false, vm.canAdvance.value)
        vm.submit()
        advanceUntilIdle()
        coVerify(exactly = 0) { recurringRepo.create(any()) }

        vm.setTermsAccepted(true)
        runCurrent()
        assertEquals(true, vm.canAdvance.value)
        val sent = slot<CreateRecurringBookingRequest>()
        coEvery { recurringRepo.create(capture(sent)) } returns ApiResult.Success(template)
        vm.submit()
        advanceUntilIdle()

        assertEquals(true, sent.captured.termsAccepted)
    }

    @Test
    fun `consents covering the texts in force show no tick and assert nothing`() = runTest {
        val vm = onStepThree()

        assertEquals(false, vm.termsAsked.value)
        assertEquals(true, vm.canAdvance.value)
        val sent = slot<CreateRecurringBookingRequest>()
        coEvery { recurringRepo.create(capture(sent)) } returns ApiResult.Success(template)
        vm.submit()
        advanceUntilIdle()

        assertNull(sent.captured.termsAccepted)
    }

    @Test
    fun `a failed consent read asks for the tick`() = runTest {
        coEvery { consentClient.grantedTypes() } returns null
        val vm = onStepThree()

        assertEquals(true, vm.termsAsked.value)
        assertEquals(false, vm.canAdvance.value)
    }

    /** One tick covers the series: the server copies the schedule's request onto every occurrence. */
    @Test
    fun `a new schedule is held until the early-performance tick and then sends the request`() = runTest {
        val vm = onStepThree()
        vm.setEarlyPerformanceRequested(false)
        runCurrent()

        assertEquals(false, vm.canAdvance.value)
        vm.submit()
        advanceUntilIdle()
        coVerify(exactly = 0) { recurringRepo.create(any()) }

        vm.setEarlyPerformanceRequested(true)
        runCurrent()
        assertEquals(true, vm.canAdvance.value)
        val sent = slot<CreateRecurringBookingRequest>()
        coEvery { recurringRepo.create(capture(sent)) } returns ApiResult.Success(template)
        vm.submit()
        advanceUntilIdle()

        assertEquals(true, sent.captured.earlyPerformanceRequested)
    }

    @Test
    fun `an edit asks for no tick and reads no consents`() = runTest {
        coEvery { consentClient.grantedTypes() } returns emptySet()
        templatesFlow.value = listOf(template)

        val vm = viewModel(templateId = template.id)
        advanceUntilIdle()

        assertEquals(false, vm.termsAsked.value)
        coVerify(exactly = 0) { consentClient.grantedTypes() }
    }

    // ── a package and a service it includes ──
    //
    // Owner ruling: both are kept, so that service is booked twice. A tap that adds one asks first, as
    // on the booking sheet; a removal and a seeded selection never ask
    // (→ /product/business-rules#charging-a-package-and-a-service-together).

    private fun packageWith(id: String, vararg serviceIds: String) = PackageListItem(
        id = id,
        name = "Package $id",
        price = 20.0,
        includedServices = serviceIds.map { PackageServiceSummary(name = "Service $it", serviceId = it) },
    )

    private fun kotlinx.coroutines.test.TestScope.withDeepCleanPackage(
        orderId: String? = null,
        templateId: String? = null,
    ): CreateRecurringViewModel {
        catalogServicesFlow.value = listOf(service("svc-1"), service("svc-2"))
        catalogPackagesFlow.value = listOf(packageWith("pkg-1", "svc-1"))
        return viewModel(orderId = orderId, templateId = templateId).also { advanceUntilIdle() }
    }

    @Test
    fun `adding a service a chosen package includes asks first and adds nothing yet`() = runTest {
        val vm = withDeepCleanPackage()
        vm.togglePackage("pkg-1")

        vm.toggleService("svc-1")

        assertEquals(DoubleBooking.Service(service("svc-1"), catalogPackagesFlow.value), vm.doubleBooking.value)
        assertEquals(emptySet<String>(), vm.state.value.selectedServiceIds)
    }

    @Test
    fun `cancelling the confirm keeps the selection as it was`() = runTest {
        val vm = withDeepCleanPackage()
        vm.togglePackage("pkg-1")
        vm.toggleService("svc-1")

        vm.dismissDoubleBooking()

        assertNull(vm.doubleBooking.value)
        assertEquals(emptySet<String>(), vm.state.value.selectedServiceIds)
        assertEquals(setOf("pkg-1"), vm.state.value.selectedPackageIds)
    }

    @Test
    fun `confirming adds the service alongside the package`() = runTest {
        val vm = withDeepCleanPackage()
        vm.togglePackage("pkg-1")
        vm.toggleService("svc-1")

        vm.confirmDoubleBooking()

        assertNull(vm.doubleBooking.value)
        assertEquals(setOf("svc-1"), vm.state.value.selectedServiceIds)
        assertEquals(setOf("pkg-1"), vm.state.value.selectedPackageIds)
    }

    @Test
    fun `adding a package that includes a chosen service asks first and adds on confirm`() = runTest {
        val vm = withDeepCleanPackage()
        vm.toggleService("svc-1")

        vm.togglePackage("pkg-1")

        val pkg = catalogPackagesFlow.value.single()
        assertEquals(DoubleBooking.Package(pkg, pkg.includedServices!!), vm.doubleBooking.value)
        assertEquals(emptySet<String>(), vm.state.value.selectedPackageIds)

        vm.confirmDoubleBooking()

        assertEquals(setOf("pkg-1"), vm.state.value.selectedPackageIds)
        assertEquals(setOf("svc-1"), vm.state.value.selectedServiceIds)
    }

    @Test
    fun `removing either half never asks`() = runTest {
        val vm = withDeepCleanPackage()
        vm.toggleService("svc-1")
        vm.togglePackage("pkg-1")
        vm.confirmDoubleBooking()

        vm.toggleService("svc-1")
        vm.togglePackage("pkg-1")

        assertNull(vm.doubleBooking.value)
        assertEquals(emptySet<String>(), vm.state.value.selectedServiceIds)
        assertEquals(emptySet<String>(), vm.state.value.selectedPackageIds)
    }

    @Test
    fun `a schedule prefilled from an order never asks and its service is marked`() = runTest {
        sourceOrder(services = listOf("svc-1"), packages = listOf("pkg-1"))

        val vm = withDeepCleanPackage(orderId = "ord-7")

        assertEquals(setOf("svc-1"), vm.state.value.selectedServiceIds)
        assertEquals(setOf("pkg-1"), vm.state.value.selectedPackageIds)
        assertNull(vm.doubleBooking.value)
        assertEquals(
            catalogPackagesFlow.value,
            catalogPackagesFlow.value.selectedIncluding("svc-1", vm.state.value.selectedPackageIds),
        )
    }

    // R5: two chosen packages that include the same service book it again, so the second one asks too.
    private fun kotlinx.coroutines.test.TestScope.withOverlappingPackages(templateId: String? = null): CreateRecurringViewModel {
        catalogServicesFlow.value = listOf(service("svc-1"), service("svc-2"))
        catalogPackagesFlow.value = listOf(packageWith("pkg-1", "svc-1"), packageWith("pkg-2", "svc-1", "svc-2"))
        return viewModel(templateId = templateId).also { advanceUntilIdle() }
    }

    @Test
    fun `adding a package that shares a service with a chosen package asks first and adds on confirm`() = runTest {
        val vm = withOverlappingPackages()
        vm.togglePackage("pkg-1")

        vm.togglePackage("pkg-2")

        val pkg2 = catalogPackagesFlow.value[1]
        assertEquals(DoubleBooking.Package(pkg2, listOf(pkg2.includedServices!![0])), vm.doubleBooking.value)
        assertEquals(setOf("pkg-1"), vm.state.value.selectedPackageIds)

        vm.confirmDoubleBooking()

        assertEquals(setOf("pkg-1", "pkg-2"), vm.state.value.selectedPackageIds)
    }

    @Test
    fun `adding a service two chosen packages include asks with the once-more message`() = runTest {
        val vm = withOverlappingPackages()
        vm.togglePackage("pkg-1")
        vm.togglePackage("pkg-2")
        vm.confirmDoubleBooking()

        vm.toggleService("svc-1")

        val pick = vm.doubleBooking.value as DoubleBooking.Service
        assertEquals(catalogPackagesFlow.value, pick.packages)
        assertEquals(R.string.booking_twice_service_message_many, pick.messageRes)
        assertEquals(emptySet<String>(), vm.state.value.selectedServiceIds)
    }

    @Test
    fun `a schedule being edited with two overlapping packages never asks`() = runTest {
        templatesFlow.value = listOf(template.copy(selectedServiceIds = emptyList(), selectedPackageIds = listOf("pkg-1", "pkg-2")))

        val vm = withOverlappingPackages(templateId = template.id)

        assertEquals(setOf("pkg-1", "pkg-2"), vm.state.value.selectedPackageIds)
        assertNull(vm.doubleBooking.value)
    }

    @Test
    fun `a schedule being edited never asks`() = runTest {
        templatesFlow.value = listOf(
            template.copy(selectedServiceIds = listOf("svc-1"), selectedPackageIds = listOf("pkg-1")),
        )

        val vm = withDeepCleanPackage(templateId = template.id)

        assertEquals(setOf("svc-1"), vm.state.value.selectedServiceIds)
        assertEquals(setOf("pkg-1"), vm.state.value.selectedPackageIds)
        assertNull(vm.doubleBooking.value)
    }
}
