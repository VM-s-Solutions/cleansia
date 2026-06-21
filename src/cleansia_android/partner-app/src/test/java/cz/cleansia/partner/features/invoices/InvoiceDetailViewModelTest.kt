package cz.cleansia.partner.features.invoices

import android.content.Context
import androidx.lifecycle.SavedStateHandle
import cz.cleansia.core.network.ApiError
import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.partner.api.model.EmployeeInvoiceDetailDto
import cz.cleansia.partner.core.network.ApiErrorTranslator
import cz.cleansia.partner.data.invoices.InvoicesRepository
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
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Rule
import org.junit.Test

@OptIn(ExperimentalCoroutinesApi::class)
class InvoiceDetailViewModelTest {

    @get:Rule
    val mainRule = MainDispatcherRule()

    private lateinit var invoicesRepository: InvoicesRepository
    private lateinit var errorTranslator: ApiErrorTranslator
    private lateinit var snackbar: SnackbarController
    private lateinit var appContext: Context

    private val invoiceId = "invoice-1"
    private val invoice = mockk<EmployeeInvoiceDetailDto>()

    @Before
    fun setUp() {
        invoicesRepository = mockk(relaxed = true)
        errorTranslator = mockk()
        snackbar = mockk(relaxed = true)
        appContext = mockk(relaxed = true)
        every { errorTranslator.translate(any()) } returns "translated error"
    }

    private fun viewModel() = InvoiceDetailViewModel(
        SavedStateHandle(mapOf("invoiceId" to invoiceId)),
        invoicesRepository,
        errorTranslator,
        snackbar,
        appContext,
    )

    @Test
    fun `init loads invoice transitioning Loading to Loaded`() = runTest {
        coEvery { invoicesRepository.getById(invoiceId) } returns ApiResult.Success(invoice)

        val vm = viewModel()
        assertEquals(InvoiceDetailUiState.Loading, vm.uiState.value)

        advanceUntilIdle()
        assertEquals(InvoiceDetailUiState.Loaded(invoice), vm.uiState.value)
        assertEquals(DownloadState.Idle, vm.downloadState.value)
    }

    @Test
    fun `init failure transitions to Error and snackbars`() = runTest {
        coEvery { invoicesRepository.getById(invoiceId) } returns ApiResult.Error(ApiError.Network("down"))

        val vm = viewModel()
        advanceUntilIdle()

        assertTrue(vm.uiState.value is InvoiceDetailUiState.Error)
        verify { snackbar.showError("translated error") }
    }

    @Test
    fun `refresh failure while loaded keeps the loaded invoice`() = runTest {
        coEvery { invoicesRepository.getById(invoiceId) } returnsMany listOf(
            ApiResult.Success(invoice),
            ApiResult.Error(ApiError.Network("down")),
        )

        val vm = viewModel()
        advanceUntilIdle()
        assertEquals(InvoiceDetailUiState.Loaded(invoice), vm.uiState.value)

        vm.refresh()
        advanceUntilIdle()
        assertEquals(InvoiceDetailUiState.Loaded(invoice), vm.uiState.value)
        verify { snackbar.showError("translated error") }
    }

    @Test
    fun `download failure surfaces snackbar and returns to Idle`() = runTest {
        coEvery { invoicesRepository.getById(invoiceId) } returns ApiResult.Success(invoice)
        coEvery { invoicesRepository.downloadPdf(invoiceId) } returns ApiResult.Error(ApiError.Network("down"))

        val vm = viewModel()
        advanceUntilIdle()

        vm.download()
        advanceUntilIdle()

        assertEquals(DownloadState.Idle, vm.downloadState.value)
        verify { snackbar.showError("translated error") }
    }

    @Test
    fun `clearDownloadedUri resets download state to Idle`() = runTest {
        coEvery { invoicesRepository.getById(invoiceId) } returns ApiResult.Success(invoice)

        val vm = viewModel()
        advanceUntilIdle()

        vm.clearDownloadedUri()
        assertEquals(DownloadState.Idle, vm.downloadState.value)
        coVerify(exactly = 0) { invoicesRepository.downloadPdf(any()) }
    }
}
