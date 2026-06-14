package cz.cleansia.customer.features.disputes

import android.content.Context
import androidx.lifecycle.SavedStateHandle
import app.cash.turbine.test
import cz.cleansia.customer.R
import cz.cleansia.customer.core.disputes.DisputeRepository
import cz.cleansia.customer.testing.MainDispatcherRule
import cz.cleansia.customer.ui.state.ActionState
import io.mockk.coEvery
import io.mockk.coVerify
import io.mockk.every
import io.mockk.mockk
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Rule
import org.junit.Test

@OptIn(ExperimentalCoroutinesApi::class)
class CreateDisputeViewModelTest {

    @get:Rule
    val mainRule = MainDispatcherRule()

    private lateinit var repository: DisputeRepository
    private lateinit var appContext: Context

    @Before
    fun setUp() {
        repository = mockk(relaxed = true)
        appContext = mockk(relaxed = true)
        every { appContext.getString(R.string.dispute_create_missing_order) } returns "missing order"
        every { appContext.getString(R.string.dispute_create_retry_hint) } returns "retry hint"
    }

    private fun viewModel(orderId: String? = "order-1") =
        CreateDisputeViewModel(
            disputeRepository = repository,
            savedStateHandle = SavedStateHandle(mapOf("orderId" to orderId)),
            appContext = appContext,
        )

    private val validDescription = "Cleaner skipped the kitchen entirely"

    @Test
    fun `starts Idle`() = runTest {
        assertEquals(ActionState.Idle, viewModel().submitState.value)
    }

    @Test
    fun `submit success emits created id and returns to Idle`() = runTest {
        coEvery { repository.create("order-1", 3, validDescription) } returns "dispute-9"

        val vm = viewModel()
        vm.createdDisputeId.test {
            vm.submit(3, validDescription)
            advanceUntilIdle()
            assertEquals("dispute-9", awaitItem())
        }
        assertEquals(ActionState.Idle, vm.submitState.value)
        coVerify { repository.refresh() }
    }

    @Test
    fun `submit failure surfaces ActionState Error and no effect`() = runTest {
        coEvery { repository.create("order-1", 3, validDescription) } returns null

        val vm = viewModel()
        vm.submit(3, validDescription)
        advanceUntilIdle()

        assertTrue(vm.submitState.value is ActionState.Error)
        assertEquals("retry hint", (vm.submitState.value as ActionState.Error).message)
    }

    @Test
    fun `missing orderId surfaces ActionState Error without calling repo`() = runTest {
        val vm = viewModel(orderId = null)
        vm.submit(3, validDescription)
        advanceUntilIdle()

        assertTrue(vm.submitState.value is ActionState.Error)
        assertEquals("missing order", (vm.submitState.value as ActionState.Error).message)
        coVerify(exactly = 0) { repository.create(any(), any(), any()) }
    }

    @Test
    fun `submit is re-entry guarded while submitting`() = runTest {
        var calls = 0
        coEvery { repository.create("order-1", 3, validDescription) } coAnswers {
            calls++
            "dispute-9"
        }

        val vm = viewModel()
        vm.submit(3, validDescription)
        vm.submit(3, validDescription)
        advanceUntilIdle()

        assertEquals(1, calls)
    }

    @Test
    fun `out-of-range description does not submit`() = runTest {
        val vm = viewModel()
        vm.submit(3, "too short")
        advanceUntilIdle()

        coVerify(exactly = 0) { repository.create(any(), any(), any()) }
    }

    @Test
    fun `clearError resets to Idle`() = runTest {
        coEvery { repository.create("order-1", 3, validDescription) } returns null

        val vm = viewModel()
        vm.submit(3, validDescription)
        advanceUntilIdle()
        assertTrue(vm.submitState.value is ActionState.Error)

        vm.clearError()
        assertEquals(ActionState.Idle, vm.submitState.value)
    }
}
