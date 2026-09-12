package cz.cleansia.customer.features.recurring

import android.content.Context
import androidx.lifecycle.SavedStateHandle
import androidx.lifecycle.ViewModelStore
import app.cash.turbine.test
import cz.cleansia.core.network.ApiError
import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.customer.R
import cz.cleansia.customer.core.catalog.CatalogRepository
import cz.cleansia.customer.core.catalog.CategoryDto
import cz.cleansia.customer.core.catalog.PackageListItem
import cz.cleansia.customer.core.catalog.ServiceListItem
import cz.cleansia.customer.core.data.AddressRepository
import cz.cleansia.customer.core.data.UserAddress
import cz.cleansia.customer.core.orders.OrderDetailDto
import cz.cleansia.customer.core.orders.OrderPackageDetailsDto
import cz.cleansia.customer.core.orders.OrderRepository
import cz.cleansia.customer.core.orders.OrderServiceDetailsDto
import cz.cleansia.customer.core.recurring.RecurrenceFrequency
import cz.cleansia.customer.core.recurring.RecurringBookingRepository
import cz.cleansia.customer.core.recurring.RecurringBookingTemplateDto
import cz.cleansia.customer.core.recurring.UpdateRecurringBookingRequest
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
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Rule
import org.junit.Test

@OptIn(ExperimentalCoroutinesApi::class)
class CreateRecurringViewModelTest {

    @get:Rule
    val mainRule = MainDispatcherRule()

    private lateinit var recurringRepo: RecurringBookingRepository
    private lateinit var orderRepo: OrderRepository
    private lateinit var catalogRepo: CatalogRepository
    private lateinit var addressRepo: AddressRepository
    private lateinit var snackbar: SnackbarController
    private lateinit var appContext: Context

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
        snackbar = mockk(relaxed = true)
        appContext = mockk(relaxed = true)
        templatesFlow = MutableStateFlow(emptyList())
        addressesFlow = MutableStateFlow(emptyList())
        catalogServicesFlow = MutableStateFlow(emptyList())
        catalogPackagesFlow = MutableStateFlow(emptyList())
        catalogCountryFlow = MutableStateFlow(null)
        catalogLoadedFlow = MutableStateFlow(false)
        coEvery { catalogRepo.refresh() } returns ApiResult.Success(Unit)
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
            snackbar = snackbar,
            appContext = appContext,
        )

    private fun fillValidForm(vm: CreateRecurringViewModel) {
        vm.setSavedAddressId("addr-1")
        vm.toggleService("svc-1")
        vm.setStartsOn("2026-07-01T00:00:00Z")
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

    private fun sourceOrder(services: List<String>, packages: List<String> = emptyList()) {
        coEvery { orderRepo.getById("ord-7") } returns ApiResult.Success(
            OrderDetailDto(
                id = "ord-7",
                rooms = 3,
                bathrooms = 2,
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
    fun `step one advances once a time of day is set`() = runTest {
        val vm = viewModel()
        advanceUntilIdle()
        assertEquals(true, vm.canAdvance.value)

        vm.setTimeOfDay("")
        runCurrent()
        assertEquals(false, vm.canAdvance.value)

        vm.setTimeOfDay("09:30")
        runCurrent()
        assertEquals(true, vm.canAdvance.value)
    }

    @Test
    fun `step two advances only with a service or a package picked`() = runTest {
        val vm = viewModel()
        advanceUntilIdle()
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
    fun `step three advances only with an address and a start date`() = runTest {
        val vm = viewModel()
        advanceUntilIdle()
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
}
