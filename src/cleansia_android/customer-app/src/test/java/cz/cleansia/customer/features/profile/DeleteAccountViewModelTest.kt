package cz.cleansia.customer.features.profile

import app.cash.turbine.test
import cz.cleansia.core.network.ApiError
import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.customer.R
import cz.cleansia.customer.core.user.UserRepository
import cz.cleansia.customer.testing.MainDispatcherRule
import cz.cleansia.customer.ui.state.ActionState
import io.mockk.coEvery
import io.mockk.coVerify
import io.mockk.mockk
import io.mockk.verify
import kotlinx.coroutines.CompletableDeferred
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.runCurrent
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Rule
import org.junit.Test

@OptIn(ExperimentalCoroutinesApi::class)
class DeleteAccountViewModelTest {

    @get:Rule
    val mainRule = MainDispatcherRule()

    private lateinit var repository: UserRepository
    private lateinit var snackbar: SnackbarController

    @Before
    fun setUp() {
        repository = mockk(relaxed = true)
        snackbar = mockk(relaxed = true)
    }

    private fun viewModel() = DeleteAccountViewModel(repository, snackbar)

    @Test
    fun `starts Idle`() {
        assertEquals(ActionState.Idle, viewModel().deleteState.value)
    }

    @Test
    fun `deleteAccount success shows confirmation snackbar, emits effect, returns Idle`() = runTest {
        coEvery { repository.deleteAccount() } returns ApiResult.Success(Unit)

        val vm = viewModel()
        vm.accountDeleted.test {
            vm.deleteAccount()
            advanceUntilIdle()
            awaitItem()
        }
        verify(exactly = 1) { snackbar.showSuccessKey(R.string.delete_account_success) }
        assertEquals(ActionState.Idle, vm.deleteState.value)
    }

    @Test
    fun `deleteAccount http failure surfaces snackbar and ActionState Error`() = runTest {
        coEvery { repository.deleteAccount() } returns
            ApiResult.Error(ApiError.Server(statusCode = 500, message = "server boom"))

        val vm = viewModel()
        vm.deleteAccount()
        advanceUntilIdle()

        verify(exactly = 1) { snackbar.showError("server boom") }
        assertTrue(vm.deleteState.value is ActionState.Error)
    }

    @Test
    fun `deleteAccount network failure stays silent but shows ActionState Error`() = runTest {
        coEvery { repository.deleteAccount() } returns
            ApiResult.Error(ApiError.Network("offline"))

        val vm = viewModel()
        vm.deleteAccount()
        advanceUntilIdle()

        verify(exactly = 0) { snackbar.showError(any<String>()) }
        assertTrue(vm.deleteState.value is ActionState.Error)
    }

    @Test
    fun `deleteAccount is re-entry guarded while in flight`() = runTest {
        val gate = CompletableDeferred<ApiResult<Unit>>()
        coEvery { repository.deleteAccount() } coAnswers { gate.await() }

        val vm = viewModel()
        vm.deleteAccount()
        runCurrent()
        assertEquals(ActionState.Submitting, vm.deleteState.value)

        vm.deleteAccount()
        runCurrent()

        gate.complete(ApiResult.Success(Unit))
        advanceUntilIdle()

        coVerify(exactly = 1) { repository.deleteAccount() }
        assertEquals(ActionState.Idle, vm.deleteState.value)
    }
}
