package cz.cleansia.partner.features.invoices

import cz.cleansia.partner.api.model.EmployeeInvoiceStatus
import cz.cleansia.partner.data.invoices.Invoice
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

/** "Total earned" on the invoice list: what was paid, never what is still owed or was withdrawn, and never two currencies in one sum. */
class TotalEarnedTest {

    @Test
    fun `only paid invoices count`() {
        val invoices = listOf(
            invoice("paid-1", 1000.00, EmployeeInvoiceStatus._3),
            invoice("pending", 700.00, EmployeeInvoiceStatus._1),
            invoice("approved", 650.00, EmployeeInvoiceStatus._2),
            invoice("disputed", 300.00, EmployeeInvoiceStatus._4),
            invoice("rejected", 200.00, EmployeeInvoiceStatus._5),
            invoice("cancelled", 900.00, EmployeeInvoiceStatus._6),
            invoice("paid-2", 500.50, EmployeeInvoiceStatus._3),
        )

        val paid = paidInvoices(invoices)

        assertEquals(listOf("paid-1", "paid-2"), paid.map { it.id })
        assertEquals(listOf<Pair<String?, Double>>("CZK" to 1500.50), totalsByCurrency(paid).toList())
    }

    @Test
    fun `a koruna and a euro invoice are two totals, in the order the list first names each`() {
        val paid = listOf(
            invoice("eur-1", 100.00, EmployeeInvoiceStatus._3, currencyCode = "EUR"),
            invoice("czk-1", 1200.00, EmployeeInvoiceStatus._3),
            invoice("eur-2", 20.25, EmployeeInvoiceStatus._3, currencyCode = "EUR"),
        )

        assertEquals(listOf<Pair<String?, Double>>("EUR" to 120.25, "CZK" to 1200.00), totalsByCurrency(paid).toList())
    }

    @Test
    fun `nothing paid yet is no total, not a zero`() {
        val paid = paidInvoices(listOf(invoice("pending", 700.00, EmployeeInvoiceStatus._1)))

        assertTrue(paid.isEmpty())
        assertTrue(totalsByCurrency(paid).isEmpty())
    }

    private fun invoice(
        id: String,
        totalAmount: Double,
        status: EmployeeInvoiceStatus,
        currencyCode: String = "CZK",
    ) = Invoice(
        id = id,
        employeeId = "emp-7",
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
        currencyCode = currencyCode,
        currencyId = "cur-${currencyCode.lowercase()}",
        status = status,
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
}
