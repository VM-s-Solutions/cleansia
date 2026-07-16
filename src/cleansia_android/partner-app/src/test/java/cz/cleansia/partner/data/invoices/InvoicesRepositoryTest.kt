package cz.cleansia.partner.data.invoices

import cz.cleansia.core.auth.SessionScopedCache
import cz.cleansia.partner.api.client.EmployeePayrollApi
import cz.cleansia.partner.api.model.PagedDataOfEmployeeInvoiceDto
import io.mockk.coEvery
import io.mockk.mockk
import kotlinx.coroutines.test.runTest
import kotlinx.serialization.json.Json
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test
import retrofit2.Response

/**
 * Pins the [SessionScopedCache] contract of [InvoicesRepositoryImpl]: the
 * my-invoices watermark resets on sign-out so the next account's first entry
 * isn't gated out as "fresh" by the prior user's fetch.
 */
class InvoicesRepositoryTest {

    private lateinit var payrollApi: EmployeePayrollApi
    private val json = Json { ignoreUnknownKeys = true; isLenient = true }

    @Before
    fun setUp() {
        payrollApi = mockk()
    }

    private fun newRepo() = InvoicesRepositoryImpl(payrollApi, json)

    @Test
    fun clear_resetsMyInvoicesWatermark() = runTest {
        coEvery {
            payrollApi.employeePayrollGetPagedInvoices(
                any(), any(), any(), any(), any(), any(), any(), any(), any(), any(), any(),
            )
        } returns Response.success(mockk<PagedDataOfEmployeeInvoiceDto>(relaxed = true))
        val repo = newRepo()
        repo.getMyInvoices("emp-1")
        assertFalse("watermark should be fresh after a successful fetch", repo.getMyInvoicesStaleness().isStale())

        (repo as SessionScopedCache).clear()

        assertTrue("watermark must be stale again after clear()", repo.getMyInvoicesStaleness().isStale())
    }
}
