package cz.cleansia.customer.features.recurring

import androidx.lifecycle.SavedStateHandle
import app.cash.turbine.test
import cz.cleansia.core.network.ApiError
import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.customer.core.catalog.CatalogRepository
import cz.cleansia.customer.core.data.AddressRepository
import cz.cleansia.customer.core.orders.OrderRepository
import cz.cleansia.customer.core.recurring.CreateRecurringBookingRequest
import cz.cleansia.customer.core.recurring.RecurringBookingRepository
import cz.cleansia.customer.core.recurring.RecurringBookingTemplateDto
import cz.cleansia.customer.testing.MainDispatcherRule
import cz.cleansia.customer.ui.state.ActionState
import io.mockk.coEvery
import io.mockk.coVerify
import io.mockk.every
import io.mockk.mockk
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

    @Before
    fun setUp() {
        recurringRepo = mockk(relaxed = true)
        orderRepo = mockk(relaxed = true)
        catalogRepo = mockk(relaxed = true)
        addressRepo = mockk(relaxed = true)
        snackbar = mockk(relaxed = true)
        coEvery { catalogRepo.refresh() } returns ApiResult.Success(Unit)
        every { addressRepo.addresses } returns MutableStateFlow(emptyList())
    }

    private fun viewModel(orderId: String? = null) =
        CreateRecurringViewModel(
            savedStateHandle = SavedStateHandle(mapOf("orderId" to orderId)),
            recurringRepo = recurringRepo,
            orderRepo = orderRepo,
            catalogRepo = catalogRepo,
            addressRepo = addressRepo,
            snackbar = snackbar,
        )

    private fun fillValidForm(vm: CreateRecurringViewModel) {
        vm.setSavedAddressId("addr-1")
        vm.toggleService("svc-1")
        vm.setStartsOn("2026-07-01T00:00:00Z")
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
}
