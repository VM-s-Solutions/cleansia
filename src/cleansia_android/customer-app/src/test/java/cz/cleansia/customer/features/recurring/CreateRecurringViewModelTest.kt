package cz.cleansia.customer.features.recurring

import androidx.lifecycle.SavedStateHandle
import app.cash.turbine.test
import cz.cleansia.core.network.ApiError
import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.customer.core.catalog.CatalogRepository
import cz.cleansia.customer.core.data.AddressRepository
import cz.cleansia.customer.core.orders.OrderRepository
import cz.cleansia.customer.core.recurring.RecurrenceFrequency
import cz.cleansia.customer.core.recurring.RecurringBookingRepository
import cz.cleansia.customer.core.recurring.RecurringBookingTemplateDto
import cz.cleansia.customer.core.recurring.UpdateRecurringBookingRequest
import cz.cleansia.customer.testing.MainDispatcherRule
import cz.cleansia.customer.ui.state.ActionState
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

    private lateinit var templatesFlow: MutableStateFlow<List<RecurringBookingTemplateDto>>

    @Before
    fun setUp() {
        recurringRepo = mockk(relaxed = true)
        orderRepo = mockk(relaxed = true)
        catalogRepo = mockk(relaxed = true)
        addressRepo = mockk(relaxed = true)
        snackbar = mockk(relaxed = true)
        templatesFlow = MutableStateFlow(emptyList())
        coEvery { catalogRepo.refresh() } returns ApiResult.Success(Unit)
        every { addressRepo.addresses } returns MutableStateFlow(emptyList())
        every { recurringRepo.templates } returns templatesFlow
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
            appContext = mockk(relaxed = true),
        )

    private fun fillValidForm(vm: CreateRecurringViewModel) {
        vm.setSavedAddressId("addr-1")
        vm.toggleService("svc-1")
        vm.setStartsOn("2026-07-01T00:00:00Z")
    }

    private val plusRefusal = "Recurring cleanings are a Cleansia Plus benefit — subscribe to set one up."

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
