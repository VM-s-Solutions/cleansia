package cz.cleansia.customer.features.disputes

import android.content.Context
import androidx.lifecycle.SavedStateHandle
import app.cash.turbine.test
import cz.cleansia.core.network.ApiError
import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.customer.R
import cz.cleansia.customer.core.disputes.DisputeDetailsDto
import cz.cleansia.customer.core.disputes.DisputeRepository
import cz.cleansia.customer.core.disputes.UploadDisputeEvidenceResponse
import cz.cleansia.customer.testing.MainDispatcherRule
import cz.cleansia.customer.ui.state.ActionState
import io.mockk.coEvery
import io.mockk.coVerify
import io.mockk.every
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
class DisputeDetailViewModelTest {

    @get:Rule
    val mainRule = MainDispatcherRule()

    private lateinit var repository: DisputeRepository
    private lateinit var snackbar: SnackbarController
    private lateinit var appContext: Context

    private val loaded = DisputeDetailsDto(id = "dispute-1")

    @Before
    fun setUp() {
        repository = mockk(relaxed = true)
        snackbar = mockk(relaxed = true)
        appContext = mockk(relaxed = true)
        every { appContext.getString(R.string.dispute_evidence_too_large) } returns "too large"
        every { appContext.getString(R.string.dispute_evidence_unsupported_type) } returns "bad type"
        coEvery { repository.getById("dispute-1") } returns ApiResult.Success(loaded)
    }

    private fun viewModel(disputeId: String? = "dispute-1") =
        DisputeDetailViewModel(
            disputeRepository = repository,
            snackbar = snackbar,
            appContext = appContext,
            savedStateHandle = SavedStateHandle(mapOf("disputeId" to disputeId)),
        )

    private val pngBytes = ByteArray(10)

    // ── sendMessage ──

    @Test
    fun `send actions start Idle`() = runTest {
        val vm = viewModel()
        advanceUntilIdle()
        assertEquals(ActionState.Idle, vm.sendState.value)
        assertEquals(ActionState.Idle, vm.uploadState.value)
    }

    @Test
    fun `sendMessage success emits one-shot effect, reloads, returns Idle`() = runTest {
        coEvery { repository.addMessage("dispute-1", "hello") } returns ApiResult.Success(Unit)

        val vm = viewModel()
        advanceUntilIdle()

        vm.messageSent.test {
            vm.sendMessage("hello")
            advanceUntilIdle()
            awaitItem()
        }
        assertEquals(ActionState.Idle, vm.sendState.value)
        coVerify(atLeast = 2) { repository.getById("dispute-1") }
    }

    @Test
    fun `sendMessage http failure surfaces snackbar and ActionState Error`() = runTest {
        coEvery { repository.addMessage("dispute-1", "hello") } returns
            ApiResult.Error(ApiError.Server(statusCode = 500, message = "server boom"))

        val vm = viewModel()
        advanceUntilIdle()
        vm.sendMessage("hello")
        advanceUntilIdle()

        verify(exactly = 1) { snackbar.showError("server boom") }
        assertTrue(vm.sendState.value is ActionState.Error)
    }

    @Test
    fun `sendMessage network failure stays silent but shows ActionState Error`() = runTest {
        coEvery { repository.addMessage("dispute-1", "hello") } returns
            ApiResult.Error(ApiError.Network("offline"))

        val vm = viewModel()
        advanceUntilIdle()
        vm.sendMessage("hello")
        advanceUntilIdle()

        verify(exactly = 0) { snackbar.showError(any<String>()) }
        assertTrue(vm.sendState.value is ActionState.Error)
    }

    @Test
    fun `sendMessage in-flight gates the send state to Submitting`() = runTest {
        val gate = CompletableDeferred<ApiResult<Unit>>()
        coEvery { repository.addMessage("dispute-1", "hello") } coAnswers { gate.await() }

        val vm = viewModel()
        advanceUntilIdle()
        vm.sendMessage("hello")
        runCurrent()
        assertEquals(ActionState.Submitting, vm.sendState.value)

        gate.complete(ApiResult.Success(Unit))
        advanceUntilIdle()
        assertEquals(ActionState.Idle, vm.sendState.value)
    }

    @Test
    fun `sendMessage out-of-range content does not call repo`() = runTest {
        val vm = viewModel()
        advanceUntilIdle()
        vm.sendMessage("   ")
        advanceUntilIdle()
        coVerify(exactly = 0) { repository.addMessage(any(), any()) }
    }

    // ── uploadEvidence ──

    @Test
    fun `uploadEvidence success emits one-shot effect, reloads, returns Idle`() = runTest {
        coEvery { repository.uploadEvidence("dispute-1", pngBytes, "a.png", "image/png") } returns
            ApiResult.Success(UploadDisputeEvidenceResponse(evidenceId = "ev-1"))

        val vm = viewModel()
        advanceUntilIdle()

        vm.evidenceUploaded.test {
            vm.uploadEvidence(pngBytes, "a.png", "image/png")
            advanceUntilIdle()
            awaitItem()
        }
        assertEquals(ActionState.Idle, vm.uploadState.value)
    }

    @Test
    fun `uploadEvidence http failure surfaces snackbar and ActionState Error`() = runTest {
        coEvery { repository.uploadEvidence("dispute-1", pngBytes, "a.png", "image/png") } returns
            ApiResult.Error(ApiError.Server(statusCode = 500, message = "server boom"))

        val vm = viewModel()
        advanceUntilIdle()
        vm.uploadEvidence(pngBytes, "a.png", "image/png")
        advanceUntilIdle()

        verify(exactly = 1) { snackbar.showError("server boom") }
        assertTrue(vm.uploadState.value is ActionState.Error)
    }

    @Test
    fun `uploadEvidence in-flight gates the upload state to Submitting`() = runTest {
        val gate = CompletableDeferred<ApiResult<UploadDisputeEvidenceResponse>>()
        coEvery { repository.uploadEvidence("dispute-1", pngBytes, "a.png", "image/png") } coAnswers { gate.await() }

        val vm = viewModel()
        advanceUntilIdle()
        vm.uploadEvidence(pngBytes, "a.png", "image/png")
        runCurrent()
        assertEquals(ActionState.Submitting, vm.uploadState.value)

        gate.complete(ApiResult.Success(UploadDisputeEvidenceResponse(evidenceId = "ev-1")))
        advanceUntilIdle()
        assertEquals(ActionState.Idle, vm.uploadState.value)
    }

    @Test
    fun `uploadEvidence rejects oversized file without calling repo`() = runTest {
        val vm = viewModel()
        advanceUntilIdle()
        vm.uploadEvidence(ByteArray(11 * 1024 * 1024), "big.png", "image/png")
        advanceUntilIdle()

        verify(exactly = 1) { snackbar.showError("too large") }
        coVerify(exactly = 0) { repository.uploadEvidence(any(), any(), any(), any()) }
    }

    @Test
    fun `uploadEvidence rejects unsupported type without calling repo`() = runTest {
        val vm = viewModel()
        advanceUntilIdle()
        vm.uploadEvidence(pngBytes, "a.txt", "text/plain")
        advanceUntilIdle()

        verify(exactly = 1) { snackbar.showError("bad type") }
        coVerify(exactly = 0) { repository.uploadEvidence(any(), any(), any(), any()) }
    }
}
