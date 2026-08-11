package cz.cleansia.partner.features.invoices

import cz.cleansia.core.freshness.Staleness
import cz.cleansia.core.network.ApiError
import cz.cleansia.core.network.ApiResult
import cz.cleansia.core.snackbar.SnackbarController
import cz.cleansia.partner.api.model.EmployeeInvoiceStatus
import cz.cleansia.partner.core.auth.EmployeeIdResolver
import cz.cleansia.partner.core.network.ApiErrorTranslator
import cz.cleansia.partner.data.invoices.Invoice
import cz.cleansia.partner.data.invoices.InvoicesRepository
import cz.cleansia.partner.testing.MainDispatcherRule
import io.mockk.coEvery
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
class InvoicesListViewModelTest {

    @get:Rule
    val mainRule = MainDispatcherRule()

    private lateinit var invoicesRepository: InvoicesRepository
    private lateinit var employeeIdResolver: EmployeeIdResolver
    private lateinit var errorTranslator: ApiErrorTranslator
    private lateinit var snackbar: SnackbarController

    @Before
    fun setUp() {
        invoicesRepository = mockk(relaxed = true)
        employeeIdResolver = mockk()
        errorTranslator = mockk()
        snackbar = mockk(relaxed = true)
        coEvery { employeeIdResolver.resolve() } returns EMPLOYEE_ID
        every { invoicesRepository.getMyInvoicesStaleness() } returns Staleness()
        every { errorTranslator.translate(any()) } returns "translated error"
    }

    private fun viewModel() = InvoicesListViewModel(
        invoicesRepository,
        employeeIdResolver,
        errorTranslator,
        snackbar,
    )

    @Test
    fun `init loads the page and the rollup sums every invoice`() = runTest {
        coEvery { invoicesRepository.getMyInvoices(EMPLOYEE_ID) } returns
            ApiResult.Success(listOf(invoice("inv-1", 1500.25), invoice("inv-2", 2400.75)))

        val vm = viewModel()
        advanceUntilIdle()

        assertEquals(2, vm.uiState.value.invoices.size)
        assertEquals(3901.00, vm.uiState.value.invoices.sumOf { it.totalAmount }, 0.0001)
        assertTrue(vm.uiState.value.hasLoadedOnce)
    }

    @Test
    fun `a page the repository refused leaves no invoices to roll up`() = runTest {
        coEvery { invoicesRepository.getMyInvoices(EMPLOYEE_ID) } returns
            ApiResult.Error(ApiError.Server(200, "totalAmount is null but the mobile API contract declares it non-nullable"))

        val vm = viewModel()
        advanceUntilIdle()

        assertEquals(emptyList<Invoice>(), vm.uiState.value.invoices)
        assertTrue(vm.uiState.value.hasLoadedOnce)
        verify { snackbar.showError("translated error") }
    }

    @Test
    fun `a refresh the repository refuses keeps the last good page rather than a partial one`() = runTest {
        val good = listOf(invoice("inv-1", 1500.25), invoice("inv-2", 2400.75))
        coEvery { invoicesRepository.getMyInvoices(EMPLOYEE_ID) } returnsMany listOf(
            ApiResult.Success(good),
            ApiResult.Error(ApiError.Server(200, "totalAmount is null but the mobile API contract declares it non-nullable")),
        )

        val vm = viewModel()
        advanceUntilIdle()
        assertEquals(3901.00, vm.uiState.value.invoices.sumOf { it.totalAmount }, 0.0001)

        vm.refresh()
        advanceUntilIdle()

        assertEquals(good, vm.uiState.value.invoices)
        assertEquals(3901.00, vm.uiState.value.invoices.sumOf { it.totalAmount }, 0.0001)
        verify { snackbar.showError("translated error") }
    }

    @Test
    fun `no employee id yields an empty list rather than a stale rollup`() = runTest {
        coEvery { employeeIdResolver.resolve() } returns null

        val vm = viewModel()
        advanceUntilIdle()

        assertEquals(emptyList<Invoice>(), vm.uiState.value.invoices)
        assertTrue(vm.uiState.value.hasLoadedOnce)
    }

    private fun invoice(id: String, totalAmount: Double) = Invoice(
        id = id,
        employeeId = EMPLOYEE_ID,
        employeeName = "Jana Novak",
        payPeriodId = "pp-9",
        payPeriodLabel = "1 - 15 Aug 2026",
        invoiceNumber = "2026-0042",
        variableSymbol = "20260042",
        paymentReference = "CLS-2026-0042",
        totalOrders = 3,
        subTotal = totalAmount,
        bonusAmount = 0.0,
        deductionAmount = 0.0,
        totalAmount = totalAmount,
        currencyCode = "CZK",
        status = EmployeeInvoiceStatus._3,
        pdfBlobName = "invoices/2026-0042.pdf",
        pdfGenerationFailed = false,
        pdfGenerationError = null,
        generatedAt = "2026-08-16T06:00:00Z",
        approvedAt = null,
        approvedBy = null,
        paidAt = null,
        adminNotes = null,
        bankTransferNote = null,
    )

    private companion object {
        const val EMPLOYEE_ID = "emp-7"
    }
}
