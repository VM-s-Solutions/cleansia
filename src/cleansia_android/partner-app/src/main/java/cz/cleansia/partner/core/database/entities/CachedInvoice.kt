package cz.cleansia.partner.core.database.entities

import androidx.room.Entity
import androidx.room.PrimaryKey
import cz.cleansia.partner.domain.models.invoices.Invoice

/**
 * Room entity for caching invoices locally.
 * This allows the app to display invoices when offline.
 */
@Entity(tableName = "cached_invoices")
data class CachedInvoice(
    @PrimaryKey
    val id: String,
    val invoiceNumber: String?,
    val employeeId: String?,
    val employeeName: String?,
    val payPeriodId: String?,
    val payPeriodLabel: String?,
    val variableSymbol: String?,
    val totalOrders: Int?,
    val subTotal: Double?,
    val bonusAmount: Double?,
    val deductionAmount: Double?,
    val totalAmount: Double?,
    val currencyCode: String?,
    val status: Int?,
    val pdfBlobName: String?,
    val generatedAt: String?,
    val approvedAt: String?,
    val approvedBy: String?,
    val paidAt: String?,
    val adminNotes: String?,
    val bankTransferNote: String?,
    val paymentReference: String?,
    val cachedAt: Long = System.currentTimeMillis()
) {
    /**
     * Convert cached entity back to domain model
     */
    fun toDomainModel(): Invoice {
        return Invoice(
            id = id,
            invoiceNumber = invoiceNumber,
            employeeId = employeeId,
            employeeName = employeeName,
            payPeriodId = payPeriodId,
            payPeriodLabel = payPeriodLabel,
            variableSymbol = variableSymbol,
            totalOrders = totalOrders,
            subTotal = subTotal,
            bonusAmount = bonusAmount,
            deductionAmount = deductionAmount,
            totalAmount = totalAmount,
            currencyCode = currencyCode,
            status = status,
            pdfBlobName = pdfBlobName,
            generatedAt = generatedAt,
            approvedAt = approvedAt,
            approvedBy = approvedBy,
            paidAt = paidAt,
            adminNotes = adminNotes,
            bankTransferNote = bankTransferNote,
            paymentReference = paymentReference
        )
    }

    companion object {
        /**
         * Create a cached entity from domain model
         */
        fun fromDomainModel(invoice: Invoice): CachedInvoice {
            return CachedInvoice(
                id = invoice.id,
                invoiceNumber = invoice.invoiceNumber,
                employeeId = invoice.employeeId,
                employeeName = invoice.employeeName,
                payPeriodId = invoice.payPeriodId,
                payPeriodLabel = invoice.payPeriodLabel,
                variableSymbol = invoice.variableSymbol,
                totalOrders = invoice.totalOrders,
                subTotal = invoice.subTotal,
                bonusAmount = invoice.bonusAmount,
                deductionAmount = invoice.deductionAmount,
                totalAmount = invoice.totalAmount,
                currencyCode = invoice.currencyCode,
                status = invoice.status,
                pdfBlobName = invoice.pdfBlobName,
                generatedAt = invoice.generatedAt,
                approvedAt = invoice.approvedAt,
                approvedBy = invoice.approvedBy,
                paidAt = invoice.paidAt,
                adminNotes = invoice.adminNotes,
                bankTransferNote = invoice.bankTransferNote,
                paymentReference = invoice.paymentReference
            )
        }
    }
}
