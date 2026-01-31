package cz.cleansia.partner.domain.models.invoices

import kotlinx.serialization.Serializable

/**
 * Invoice status enum - maps to backend EmployeeInvoiceStatus (integer-based)
 * 1=Pending, 2=Approved, 3=Paid, 4=Disputed, 5=Rejected, 6=Cancelled
 */
enum class InvoiceStatus(val apiValue: Int, val displayName: String) {
    PENDING(1, "Pending"),
    APPROVED(2, "Approved"),
    PAID(3, "Paid"),
    DISPUTED(4, "Disputed"),
    REJECTED(5, "Rejected"),
    CANCELLED(6, "Cancelled");

    companion object {
        fun fromApiValue(value: Int?): InvoiceStatus =
            entries.find { it.apiValue == value } ?: PENDING

        fun fromApiValue(value: String): InvoiceStatus =
            entries.find { it.displayName.equals(value, ignoreCase = true) } ?: PENDING
    }
}

@Serializable
data class Invoice(
    val id: String,
    val invoiceNumber: String? = null,
    val employeeId: String? = null,
    val employeeName: String? = null,
    val payPeriodId: String? = null,
    val payPeriodLabel: String? = null,
    val variableSymbol: String? = null,
    val totalOrders: Int? = null,
    val subTotal: Double? = null,
    val bonusAmount: Double? = null,
    val deductionAmount: Double? = null,
    val totalAmount: Double? = null,
    val currencyCode: String? = null,
    val status: Int? = null,
    val pdfBlobName: String? = null,
    val generatedAt: String? = null,
    val approvedAt: String? = null,
    val approvedBy: String? = null,
    val paidAt: String? = null,
    val adminNotes: String? = null,
    val bankTransferNote: String? = null
) {
    // Helper properties for UI compatibility
    val invoiceStatusEnum: InvoiceStatus get() = InvoiceStatus.fromApiValue(status)
    val period: String get() = payPeriodLabel ?: ""
    val issueDate: String get() = generatedAt ?: ""
    val dueDate: String get() = "" // Not available in the API
    val currency: String get() = currencyCode ?: "CZK"
    val orderCount: Int get() = totalOrders ?: 0
}

@Serializable
data class InvoiceDetail(
    val id: String,
    val invoiceNumber: String = "",
    val statusValue: String = "Draft",
    val periodStart: String? = null,
    val periodEnd: String? = null,
    val issueDateValue: String? = null,
    val dueDateValue: String? = null,
    val paidDateValue: String? = null,
    val subtotal: Double = 0.0,
    val taxAmount: Double = 0.0,
    val totalAmount: Double = 0.0,
    val currency: String = "CZK",
    val orders: List<InvoiceOrderItem> = emptyList(),
    val employeeId: String? = null,
    val employeeName: String? = null,
    val notes: String? = null,
    val createdAt: String? = null,
    val updatedAt: String? = null,
    // Additional fields matching the Invoice model
    val variableSymbol: String? = null,
    val payPeriodLabel: String? = null,
    val bonusAmount: Double? = null,
    val deductionAmount: Double? = null,
    val adminNotes: String? = null,
    val bankTransferNote: String? = null,
    val approvedAt: String? = null,
    val approvedBy: String? = null,
    val totalOrders: Int? = null
) {
    val status: InvoiceStatus get() = InvoiceStatus.fromApiValue(statusValue)

    val period: String
        get() = if (periodStart != null && periodEnd != null) {
            "$periodStart - $periodEnd"
        } else {
            periodStart ?: periodEnd ?: ""
        }

    val issueDate: String get() = issueDateValue ?: ""
    val dueDate: String get() = dueDateValue ?: ""
    val paidDate: String get() = paidDateValue ?: ""
}

@Serializable
data class InvoiceOrderItem(
    val orderId: String,
    val orderNumber: String = "",
    val completedDate: String = "",
    val serviceName: String? = null,
    val amount: Double = 0.0,
    val currency: String = "CZK"
)

@Serializable
data class PagedInvoiceResponse(
    val data: List<Invoice> = emptyList(),
    val pageNumber: Int = 1,
    val pageSize: Int = 20,
    val total: Int = 0
) {
    // Compatibility helpers
    val items: List<Invoice> get() = data
    val totalCount: Int get() = total
    val totalPages: Int get() = if (pageSize > 0) (total + pageSize - 1) / pageSize else 0
    val hasNextPage: Boolean get() = pageNumber < totalPages
    val hasPreviousPage: Boolean get() = pageNumber > 1
}

/**
 * Filter parameters for invoices
 */
data class InvoiceFilter(
    val statuses: List<InvoiceStatus> = emptyList(),
    val searchTerm: String? = null,
    val startDate: String? = null,
    val endDate: String? = null
)
