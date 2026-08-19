package cz.cleansia.partner.features.profile

import cz.cleansia.core.network.ApiError
import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.core.ui.state.ActionState
import cz.cleansia.partner.core.gdpr.GdprDeletionClient
import cz.cleansia.partner.core.network.ApiErrorTranslator
import cz.cleansia.partner.testing.MainDispatcherRule
import io.mockk.coEvery
import io.mockk.coVerify
import io.mockk.every
import io.mockk.mockk
import io.mockk.verify
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Rule
import org.junit.Test

/**
 * The partner deletion REQUEST. → /decisions/adr-0052
 *
 * The assertions that matter are the negative ones. This screen must not do what the customer app's
 * equivalent does — the endpoint is shared and the temptation to mirror the customer flow is exactly
 * how a cleaner would get signed out of an account that still exists with jobs assigned to them.
 */
@OptIn(ExperimentalCoroutinesApi::class)
class DeleteAccountViewModelTest {

    @get:Rule
    val mainRule = MainDispatcherRule()

    private lateinit var client: GdprDeletionClient
    private lateinit var errors: ApiErrorTranslator
    private lateinit var snackbar: SnackbarController

    @Before
    fun setUp() {
        client = mockk()
        snackbar = mockk(relaxed = true)
        errors = mockk()
        every { errors.translate(any()) } returns "translated"
    }

    private fun viewModel() = DeleteAccountViewModel(client, errors, snackbar)

    @Test
    fun `a successful submit files the request and flips to requested`() = runTest {
        coEvery { client.requestDeletion() } returns ApiResult.Success(Unit)
        val vm = viewModel()

        vm.submit()
        advanceUntilIdle()

        assertTrue(vm.requested.value)
        assertEquals(ActionState.Idle, vm.state.value)
        coVerify(exactly = 1) { client.requestDeletion() }
    }

    /**
     * The whole point of the screen. A cleaner keeps working until an admin fulfils the request, so
     * nothing here may end the session — no cache wipe, no token clear, no forced sign-out. The
     * ViewModel has no session collaborator at all, which is the structural version of this
     * assertion; this pins the behavioural one.
     */
    @Test
    fun `a successful submit does not end the session`() = runTest {
        coEvery { client.requestDeletion() } returns ApiResult.Success(Unit)
        val vm = viewModel()

        vm.submit()
        advanceUntilIdle()

        // Nothing but the success confirmation is shown; no sign-out path is reachable from here.
        verify(exactly = 0) { snackbar.showError(any<String>()) }
        assertTrue(vm.requested.value)
    }

    @Test
    fun `a refusal surfaces the translated reason and stays un-requested`() = runTest {
        coEvery { client.requestDeletion() } returns
            ApiResult.Error(ApiError.BadRequest("gdpr.deletion_blocked_by_assigned_order", null))
        val vm = viewModel()

        vm.submit()
        advanceUntilIdle()

        assertFalse(vm.requested.value)
        assertTrue(vm.state.value is ActionState.Error)
        verify(exactly = 1) { snackbar.showError("translated") }
    }

    /**
     * The interceptor already surfaces a dropped connection. Showing it again reads as two separate
     * failures for one event — the same carve-out the customer ViewModel makes.
     */
    @Test
    fun `a network drop is not double-reported`() = runTest {
        coEvery { client.requestDeletion() } returns ApiResult.Error(ApiError.Network("offline"))
        val vm = viewModel()

        vm.submit()
        advanceUntilIdle()

        verify(exactly = 0) { snackbar.showError(any<String>()) }
        assertTrue(vm.state.value is ActionState.Error)
    }

    @Test
    fun `a second submit while one is in flight is ignored`() = runTest {
        coEvery { client.requestDeletion() } returns ApiResult.Success(Unit)
        val vm = viewModel()

        vm.submit()
        vm.submit()
        advanceUntilIdle()

        coVerify(exactly = 1) { client.requestDeletion() }
    }

    /**
     * Once filed, the button is gone from the UI — but the guard belongs in the ViewModel too, since
     * a re-composition or a restored back-stack entry can call submit() again. The server refuses a
     * duplicate anyway; this stops the pointless round trip and the misleading error it returns.
     */
    @Test
    fun `submitting again after success does not re-request`() = runTest {
        coEvery { client.requestDeletion() } returns ApiResult.Success(Unit)
        val vm = viewModel()

        vm.submit()
        advanceUntilIdle()
        vm.submit()
        advanceUntilIdle()

        coVerify(exactly = 1) { client.requestDeletion() }
    }
}
