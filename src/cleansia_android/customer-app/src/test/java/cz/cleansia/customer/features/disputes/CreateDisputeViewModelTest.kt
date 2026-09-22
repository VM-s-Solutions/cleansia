package cz.cleansia.customer.features.disputes

import android.content.Context
import androidx.lifecycle.SavedStateHandle
import app.cash.turbine.test
import cz.cleansia.customer.R
import cz.cleansia.customer.core.disputes.DisputeRepository
import cz.cleansia.customer.core.disputes.UploadDisputeEvidenceResponse
import cz.cleansia.customer.core.orders.OrderRepository
import cz.cleansia.customer.testing.MainDispatcherRule
import cz.cleansia.customer.ui.state.ActionState
import cz.cleansia.core.network.ApiError
import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.snackbar.SnackbarController
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
class CreateDisputeViewModelTest {

    @get:Rule
    val mainRule = MainDispatcherRule()

    private lateinit var repository: DisputeRepository

    private lateinit var orderRepository: OrderRepository
    private lateinit var snackbar: SnackbarController
    private lateinit var appContext: Context

    @Before
    fun setUp() {
        repository = mockk(relaxed = true)
        orderRepository = mockk(relaxed = true)
        snackbar = mockk(relaxed = true)
        appContext = mockk(relaxed = true)
        every { appContext.getString(R.string.dispute_create_missing_order) } returns "missing order"
        every { appContext.getString(R.string.dispute_create_retry_hint) } returns "retry hint"
        every { appContext.getString(R.string.dispute_evidence_too_large) } returns "too large"
        every { appContext.getString(R.string.dispute_evidence_unsupported_type) } returns "bad type"
        every { appContext.getString(R.string.dispute_create_evidence_partial, any()) } answers {
            "not uploaded: ${secondArg<Array<Any?>>()[0]}"
        }
    }

    private fun viewModel(orderId: String? = "order-1") =
        CreateDisputeViewModel(
            disputeRepository = repository,
            // relaxed, so the order fetch answers a mock detail and the item list stays empty. These
            // cases are about the submit path; the item list has its own test.
            orderRepository = orderRepository,
            snackbar = snackbar,
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
        coEvery { repository.create("order-1", 3, validDescription) } returns ApiResult.Success("dispute-9")

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
    fun `submit http failure surfaces parsed snackbar and inline retry hint`() = runTest {
        coEvery { repository.create("order-1", 3, validDescription) } returns
            ApiResult.Error(ApiError.Server(statusCode = 500, message = "server boom"))

        val vm = viewModel()
        vm.submit(3, validDescription)
        advanceUntilIdle()

        verify(exactly = 1) { snackbar.showError(match<ApiError> { it.getUserMessage() == "server boom" }) }
        assertTrue(vm.submitState.value is ActionState.Error)
        assertEquals("retry hint", (vm.submitState.value as ActionState.Error).message)
    }

    @Test
    fun `submit network failure stays silent but shows inline retry hint`() = runTest {
        coEvery { repository.create("order-1", 3, validDescription) } returns
            ApiResult.Error(ApiError.Network("offline"))

        val vm = viewModel()
        vm.submit(3, validDescription)
        advanceUntilIdle()

        verify(exactly = 0) { snackbar.showError(any<String>()) }
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
            ApiResult.Success("dispute-9")
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
        coEvery { repository.create("order-1", 3, validDescription) } returns
            ApiResult.Error(ApiError.Server(statusCode = 500, message = "server boom"))

        val vm = viewModel()
        vm.submit(3, validDescription)
        advanceUntilIdle()
        assertTrue(vm.submitState.value is ActionState.Error)

        vm.clearError()
        assertEquals(ActionState.Idle, vm.submitState.value)
    }

    // ── evidence ──

    private val photo = ByteArray(10)
    private val receipt = ByteArray(20)

    private fun uploaded(id: String) = ApiResult.Success(UploadDisputeEvidenceResponse(evidenceId = id))

    private fun failed() = ApiResult.Error(ApiError.Server(statusCode = 500, message = "blob down"))

    @Test
    fun `addEvidence keeps a valid file as Pending`() = runTest {
        val vm = viewModel()
        vm.addEvidence(photo, "a.png", "image/png")

        val picked = vm.pickedEvidence.value
        assertEquals(listOf("a.png"), picked.map { it.fileName })
        assertEquals(EvidenceUploadState.Pending, picked.single().upload)
    }

    @Test
    fun `addEvidence refuses an oversized file`() = runTest {
        val vm = viewModel()
        vm.addEvidence(ByteArray(DisputeFormConstants.EVIDENCE_MAX_BYTES + 1), "big.png", "image/png")

        verify(exactly = 1) { snackbar.showError("too large") }
        assertTrue(vm.pickedEvidence.value.isEmpty())
    }

    @Test
    fun `addEvidence refuses an unsupported type`() = runTest {
        val vm = viewModel()
        vm.addEvidence(photo, "a.txt", "text/plain")

        verify(exactly = 1) { snackbar.showError("bad type") }
        assertTrue(vm.pickedEvidence.value.isEmpty())
    }

    @Test
    fun `removeEvidence drops only that file`() = runTest {
        val vm = viewModel()
        vm.addEvidence(photo, "a.png", "image/png")
        vm.addEvidence(receipt, "r.pdf", "application/pdf")

        vm.removeEvidence(vm.pickedEvidence.value.first().key)

        assertEquals(listOf("r.pdf"), vm.pickedEvidence.value.map { it.fileName })
    }

    @Test
    fun `submit creates the dispute, then uploads each file in order, then emits the id`() = runTest {
        val calls = mutableListOf<String>()
        coEvery { repository.create("order-1", 3, validDescription) } coAnswers {
            calls += "create"
            ApiResult.Success("dispute-9")
        }
        coEvery { repository.uploadEvidence("dispute-9", any(), any(), any()) } coAnswers {
            calls += "upload:${thirdArg<String>()}"
            uploaded("ev")
        }

        val vm = viewModel()
        vm.addEvidence(photo, "a.png", "image/png")
        vm.addEvidence(receipt, "r.pdf", "application/pdf")
        vm.createdDisputeId.test {
            vm.submit(3, validDescription)
            advanceUntilIdle()
            assertEquals("dispute-9", awaitItem())
        }

        assertEquals(listOf("create", "upload:a.png", "upload:r.pdf"), calls)
        assertEquals(
            listOf(EvidenceUploadState.Uploaded, EvidenceUploadState.Uploaded),
            vm.pickedEvidence.value.map { it.upload },
        )
        assertEquals(ActionState.Idle, vm.submitState.value)
        verify(exactly = 0) { snackbar.showError(any<String>()) }
    }

    @Test
    fun `a file is Uploading while its request is in flight`() = runTest {
        val gate = CompletableDeferred<ApiResult<UploadDisputeEvidenceResponse>>()
        coEvery { repository.create("order-1", 3, validDescription) } returns ApiResult.Success("dispute-9")
        coEvery { repository.uploadEvidence("dispute-9", photo, "a.png", "image/png") } coAnswers { gate.await() }

        val vm = viewModel()
        vm.addEvidence(photo, "a.png", "image/png")
        vm.submit(3, validDescription)
        runCurrent()

        assertEquals(EvidenceUploadState.Uploading, vm.pickedEvidence.value.single().upload)
        assertEquals(ActionState.Submitting, vm.submitState.value)

        gate.complete(uploaded("ev"))
        advanceUntilIdle()
        assertEquals(EvidenceUploadState.Uploaded, vm.pickedEvidence.value.single().upload)
        assertEquals(ActionState.Idle, vm.submitState.value)
    }

    @Test
    fun `one failed upload keeps the dispute, marks that file, names it, and never creates twice`() = runTest {
        var creates = 0
        coEvery { repository.create("order-1", 3, validDescription) } coAnswers {
            creates++
            ApiResult.Success("dispute-9")
        }
        coEvery { repository.uploadEvidence("dispute-9", photo, "a.png", "image/png") } returns failed()
        coEvery { repository.uploadEvidence("dispute-9", receipt, "r.pdf", "application/pdf") } returns uploaded("ev-2")

        val vm = viewModel()
        vm.addEvidence(photo, "a.png", "image/png")
        vm.addEvidence(receipt, "r.pdf", "application/pdf")
        vm.createdDisputeId.test {
            vm.submit(3, validDescription)
            advanceUntilIdle()
            assertEquals("dispute-9", awaitItem())
        }

        assertEquals(
            listOf(EvidenceUploadState.Failed, EvidenceUploadState.Uploaded),
            vm.pickedEvidence.value.map { it.upload },
        )
        verify(exactly = 1) { snackbar.showError("not uploaded: a.png") }
        assertEquals(ActionState.Idle, vm.submitState.value)

        coEvery { repository.uploadEvidence("dispute-9", photo, "a.png", "image/png") } returns uploaded("ev-1")
        vm.submit(3, validDescription)
        advanceUntilIdle()

        assertEquals(1, creates)
        coVerify(exactly = 1) { repository.uploadEvidence("dispute-9", receipt, "r.pdf", "application/pdf") }
        assertEquals(
            listOf(EvidenceUploadState.Uploaded, EvidenceUploadState.Uploaded),
            vm.pickedEvidence.value.map { it.upload },
        )
    }

    @Test
    fun `a failed create uploads nothing and leaves the files Pending`() = runTest {
        coEvery { repository.create("order-1", 3, validDescription) } returns
            ApiResult.Error(ApiError.Network("offline"))

        val vm = viewModel()
        vm.addEvidence(photo, "a.png", "image/png")
        vm.submit(3, validDescription)
        advanceUntilIdle()

        coVerify(exactly = 0) { repository.uploadEvidence(any(), any(), any(), any()) }
        assertEquals(EvidenceUploadState.Pending, vm.pickedEvidence.value.single().upload)
        assertTrue(vm.submitState.value is ActionState.Error)
    }

    @Test
    fun `the file list is frozen while submitting`() = runTest {
        val gate = CompletableDeferred<ApiResult<String>>()
        coEvery { repository.create("order-1", 3, validDescription) } coAnswers { gate.await() }

        val vm = viewModel()
        vm.addEvidence(photo, "a.png", "image/png")
        vm.submit(3, validDescription)
        runCurrent()

        vm.addEvidence(receipt, "r.pdf", "application/pdf")
        vm.removeEvidence(vm.pickedEvidence.value.first().key)
        assertEquals(listOf("a.png"), vm.pickedEvidence.value.map { it.fileName })

        gate.complete(ApiResult.Error(ApiError.Network("offline")))
        advanceUntilIdle()
    }
}
