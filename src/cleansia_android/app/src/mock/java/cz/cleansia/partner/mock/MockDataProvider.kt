package cz.cleansia.partner.mock

import cz.cleansia.partner.domain.models.auth.LoginResponse
import cz.cleansia.partner.domain.models.auth.RegisterResponse
import cz.cleansia.partner.domain.models.dashboard.DashboardStats
import cz.cleansia.partner.domain.models.dashboard.EarningsAnalytics
import cz.cleansia.partner.domain.models.dashboard.EarningsDataPoint
import cz.cleansia.partner.domain.models.dashboard.EarningsSummary
import cz.cleansia.partner.domain.models.dashboard.UpcomingOrder
import cz.cleansia.partner.domain.models.invoices.Invoice
import cz.cleansia.partner.domain.models.invoices.InvoiceDetail
import cz.cleansia.partner.domain.models.invoices.InvoiceFilter
import cz.cleansia.partner.domain.models.invoices.InvoiceOrderItem
import cz.cleansia.partner.domain.models.invoices.InvoiceStatus
import cz.cleansia.partner.domain.models.invoices.PagedInvoiceResponse
import cz.cleansia.partner.domain.models.orders.CodeValue
import cz.cleansia.partner.domain.models.orders.CurrencyDetail
import cz.cleansia.partner.domain.models.orders.CurrencyInfo
import cz.cleansia.partner.domain.models.orders.Order
import cz.cleansia.partner.domain.models.orders.OrderAddressInfo
import cz.cleansia.partner.domain.models.orders.OrderDetail
import cz.cleansia.partner.domain.models.orders.OrderFilter
import cz.cleansia.partner.domain.models.orders.OrderStatus
import cz.cleansia.partner.domain.models.orders.PagedOrderResponse
import cz.cleansia.partner.domain.models.orders.PaymentStatus
import cz.cleansia.partner.domain.models.orders.ServiceDetail
import cz.cleansia.partner.domain.models.orders.ServiceInfo
import cz.cleansia.partner.domain.models.profile.EmployeeDocument
import cz.cleansia.partner.domain.models.profile.EmployeeProfile
import java.time.LocalDate
import java.time.LocalDateTime
import java.time.format.DateTimeFormatter

object MockDataProvider {

    private val CZECH_FIRST_NAMES = listOf(
        "Jan", "Petr", "Martin", "Tomáš", "Pavel", "Jaroslav", "Jiří",
        "Eva", "Jana", "Marie", "Anna", "Kateřina", "Lucie", "Tereza"
    )

    private val CZECH_LAST_NAMES = listOf(
        "Novák", "Svoboda", "Novotný", "Dvořák", "Černý", "Procházka",
        "Kučera", "Veselý", "Horák", "Němec", "Pokorný", "Marek"
    )

    private val PRAGUE_STREETS = listOf(
        "Vodičkova 12", "Vinohradská 48", "Na Příkopě 23", "Václavské náměstí 5",
        "Bělohorská 67", "Komunardů 15", "Korunní 34", "Seifertova 9",
        "Jugoslávská 22", "Londýnská 8", "Italská 31", "Francouzská 44"
    )

    private val CZECH_CITIES = listOf(
        "Praha 1", "Praha 2", "Praha 3", "Praha 4", "Praha 5",
        "Praha 6", "Praha 7", "Praha 8", "Praha 10", "Brno", "Plzeň"
    )

    private val CZECH_ZIP_CODES = listOf(
        "110 00", "120 00", "130 00", "140 00", "150 00",
        "160 00", "170 00", "180 00", "100 00", "602 00", "301 00"
    )

    private val CLEANING_SERVICES = listOf(
        ServiceInfo(id = "svc-1", name = "Běžný úklid", description = "Standardní úklid bytu", price = 450.0),
        ServiceInfo(id = "svc-2", name = "Generální úklid", description = "Hloubkový úklid celého bytu", price = 1200.0),
        ServiceInfo(id = "svc-3", name = "Mytí oken", description = "Čištění oken včetně rámů", price = 350.0),
        ServiceInfo(id = "svc-4", name = "Úklid po rekonstrukci", description = "Úklid po stavebních pracích", price = 2500.0),
        ServiceInfo(id = "svc-5", name = "Žehlení", description = "Žehlení prádla", price = 250.0),
        ServiceInfo(id = "svc-6", name = "Úklid kanceláří", description = "Profesionální úklid kancelářských prostor", price = 800.0)
    )

    const val MOCK_EMPLOYEE_ID = "emp-mock-001"
    const val MOCK_USER_ID = "user-mock-001"
    const val MOCK_EMAIL = "jan.novak@cleansia.cz"
    const val MOCK_TOKEN = "mock-jwt-token-for-testing-only"
    const val MOCK_FIRST_NAME = "Jan"
    const val MOCK_LAST_NAME = "Novák"

    // =========================================================================
    // Auth
    // =========================================================================

    fun loginResponse() = LoginResponse(
        token = MOCK_TOKEN,
        userId = MOCK_USER_ID,
        id = MOCK_EMPLOYEE_ID,
        email = MOCK_EMAIL,
        firstName = MOCK_FIRST_NAME,
        lastName = MOCK_LAST_NAME,
        isEmailConfirmed = true
    )

    fun registerResponse() = RegisterResponse(
        userId = MOCK_USER_ID,
        id = MOCK_EMPLOYEE_ID,
        email = MOCK_EMAIL,
        message = "Registrace úspěšná."
    )

    // =========================================================================
    // Dashboard
    // =========================================================================

    fun dashboardStats() = DashboardStats(
        availableOrders = 8,
        myActiveOrders = 3,
        completedThisMonth = 22,
        completedLastMonth = 18,
        pendingEarnings = 15_400.0,
        currency = "CZK"
    )

    fun earningsSummary() = EarningsSummary(
        thisWeek = 4_200.0,
        thisMonth = 18_600.0,
        lastMonth = 21_350.0,
        currency = "CZK"
    )

    fun earningsAnalytics(startDate: String?, endDate: String?) = EarningsAnalytics(
        totalEarnings = 18_600.0,
        previousPeriodEarnings = 21_350.0,
        percentageChange = -12.9,
        currency = "CZK",
        dataPoints = generateEarningsDataPoints()
    )

    private fun generateEarningsDataPoints(): List<EarningsDataPoint> {
        val today = LocalDate.now()
        val amounts = listOf(1200.0, 1800.0, 950.0, 2100.0, 1650.0, 2300.0, 1400.0,
            1900.0, 2050.0, 1750.0, 2200.0, 1100.0, 1850.0, 2400.0)
        return amounts.indices.map { i ->
            val date = today.minusDays((amounts.size - 1 - i).toLong())
            EarningsDataPoint(
                date = date.format(DateTimeFormatter.ISO_LOCAL_DATE),
                amount = amounts[i],
                label = date.format(DateTimeFormatter.ofPattern("dd.MM"))
            )
        }
    }

    fun upcomingOrders(limit: Int = 5): List<UpcomingOrder> {
        val today = LocalDate.now()
        val hours = listOf("08:00", "09:30", "11:00", "13:00", "14:30")
        return (0 until limit).map { i ->
            val date = today.plusDays(i.toLong() + 1)
            UpcomingOrder(
                id = "order-upcoming-$i",
                orderNumber = "CLN-2024-${1000 + i}",
                scheduledDate = date.format(DateTimeFormatter.ISO_LOCAL_DATE),
                scheduledTime = hours[i % hours.size],
                customerName = "${CZECH_FIRST_NAMES[i % CZECH_FIRST_NAMES.size]} ${CZECH_LAST_NAMES[i % CZECH_LAST_NAMES.size]}",
                address = PRAGUE_STREETS[i % PRAGUE_STREETS.size],
                city = CZECH_CITIES[i % CZECH_CITIES.size],
                totalAmount = listOf(450.0, 800.0, 1200.0, 1800.0, 2500.0)[i % 5],
                currency = "CZK",
                status = "Confirmed",
                servicesPreview = CLEANING_SERVICES[i % CLEANING_SERVICES.size].name
            )
        }
    }

    // =========================================================================
    // Orders
    // =========================================================================

    private val allOrders: List<Order> by lazy { generateOrders(45) }

    private fun generateOrders(count: Int): List<Order> {
        val today = LocalDate.now()
        return (0 until count).map { i ->
            val status = when {
                i < 5 -> OrderStatus.PENDING
                i < 12 -> OrderStatus.CONFIRMED
                i < 18 -> OrderStatus.IN_PROGRESS
                i < 38 -> OrderStatus.COMPLETED
                else -> OrderStatus.CANCELLED
            }
            val paymentStatus = when {
                status == OrderStatus.COMPLETED -> if (i % 3 == 0) PaymentStatus.PAID else PaymentStatus.PENDING
                status == OrderStatus.CANCELLED -> if (i % 2 == 0) PaymentStatus.REFUNDED else PaymentStatus.FAILED
                else -> PaymentStatus.PENDING
            }
            val dateOffset = when (status) {
                OrderStatus.COMPLETED -> -(1..(i % 30 + 1)).last.toLong()
                OrderStatus.CANCELLED -> -(1..(i % 15 + 1)).last.toLong()
                OrderStatus.IN_PROGRESS -> 0L
                OrderStatus.CONFIRMED -> (1..(i % 7 + 1)).last.toLong()
                OrderStatus.PENDING -> (1..(i % 14 + 1)).last.toLong()
            }
            val date = today.plusDays(dateOffset)
            val services = CLEANING_SERVICES.let { list ->
                val start = i % list.size
                val count2 = (i % 3) + 1
                (0 until count2).map { list[(start + it) % list.size] }
            }
            val totalPrice = services.sumOf { it.price ?: 0.0 }
            val cityIndex = i % CZECH_CITIES.size
            val timeSlots = listOf("08:00", "09:30", "11:00", "13:00", "14:30", "16:00")

            Order(
                id = "order-${1000 + i}",
                displayOrderNumber = "CLN-2024-${1001 + i}",
                orderStatus = CodeValue(type = "OrderStatus", name = status.apiName, value = status.apiValue),
                paymentStatus = CodeValue(type = "PaymentStatus", name = paymentStatus.apiName, value = paymentStatus.apiValue),
                cleaningDateTime = "${date}T${timeSlots[i % timeSlots.size]}:00",
                customerName = "${CZECH_FIRST_NAMES[i % CZECH_FIRST_NAMES.size]} ${CZECH_LAST_NAMES[i % CZECH_LAST_NAMES.size]}",
                customerEmail = "customer${i}@example.cz",
                customerPhone = "+420 ${600 + i % 200} ${100 + i % 900} ${100 + (i * 7) % 900}",
                customerAddress = "${PRAGUE_STREETS[i % PRAGUE_STREETS.size]}, ${CZECH_CITIES[cityIndex]}, ${CZECH_ZIP_CODES[cityIndex]}",
                totalPrice = totalPrice,
                currency = CurrencyInfo(id = "czk", code = "CZK", name = "Česká koruna", symbol = "Kč"),
                selectedServices = services,
                estimatedTime = (3..4).random(), // Short duration for testing (3-4 minutes)
                rooms = (i % 5) + 1,
                bathrooms = (i % 2) + 1,
                hasAvailableSpots = status == OrderStatus.PENDING || status == OrderStatus.CONFIRMED,
                assignedEmployeesCount = if (status in listOf(OrderStatus.IN_PROGRESS, OrderStatus.COMPLETED)) (i % 2) + 1 else 0,
                requiredEmployees = (i % 3) + 1
            )
        }
    }

    fun getAvailableOrders(
        page: Int,
        pageSize: Int = 20,
        searchTerm: String? = null,
        sortBy: String? = null,
        sortDescending: Boolean? = null
    ): Pair<List<Order>, Boolean> {
        var filtered = allOrders.filter {
            it.hasAvailableSpots == true &&
                    it.status in listOf(OrderStatus.PENDING, OrderStatus.CONFIRMED)
        }
        if (!searchTerm.isNullOrBlank()) {
            filtered = filtered.filter {
                it.customerName?.contains(searchTerm, ignoreCase = true) == true ||
                        it.displayOrderNumber?.contains(searchTerm, ignoreCase = true) == true ||
                        it.customerAddress?.contains(searchTerm, ignoreCase = true) == true
            }
        }
        filtered = applySorting(filtered, sortBy, sortDescending)
        val offset = (page - 1) * pageSize
        val paged = filtered.drop(offset).take(pageSize)
        val hasMore = offset + pageSize < filtered.size
        return paged to hasMore
    }

    fun getMyOrders(
        page: Int,
        pageSize: Int = 20,
        statuses: List<OrderStatus>,
        searchTerm: String? = null,
        sortBy: String? = null,
        sortDescending: Boolean? = null
    ): Pair<List<Order>, Boolean> {
        var filtered = if (statuses.isEmpty()) allOrders else allOrders.filter { it.status in statuses }
        if (!searchTerm.isNullOrBlank()) {
            filtered = filtered.filter {
                it.customerName?.contains(searchTerm, ignoreCase = true) == true ||
                        it.displayOrderNumber?.contains(searchTerm, ignoreCase = true) == true ||
                        it.customerAddress?.contains(searchTerm, ignoreCase = true) == true
            }
        }
        filtered = applySorting(filtered, sortBy, sortDescending)
        val offset = (page - 1) * pageSize
        val paged = filtered.drop(offset).take(pageSize)
        val hasMore = offset + pageSize < filtered.size
        return paged to hasMore
    }

    private fun applySorting(orders: List<Order>, sortBy: String?, sortDescending: Boolean?): List<Order> {
        if (sortBy == null) return orders
        return when (sortBy) {
            "cleaningDateTime" -> if (sortDescending == true) orders.sortedByDescending { it.cleaningDateTime } else orders.sortedBy { it.cleaningDateTime }
            "totalPrice" -> if (sortDescending == true) orders.sortedByDescending { it.totalPrice } else orders.sortedBy { it.totalPrice }
            else -> orders
        }
    }

    fun getPagedOrders(page: Int, pageSize: Int, filter: OrderFilter?): PagedOrderResponse {
        var filtered = allOrders.toList()
        filter?.status?.let { status ->
            filtered = filtered.filter { it.status == status }
        }
        filter?.searchTerm?.let { term ->
            if (term.isNotBlank()) {
                filtered = filtered.filter {
                    it.customerName?.contains(term, ignoreCase = true) == true
                }
            }
        }
        val offset = (page - 1) * pageSize
        val paged = filtered.drop(offset).take(pageSize)
        return PagedOrderResponse(
            data = paged,
            pageNumber = page,
            pageSize = pageSize,
            total = filtered.size
        )
    }

    fun getOrderDetail(orderId: String): OrderDetail {
        val order = allOrders.find { it.id == orderId } ?: allOrders.first()
        val cityIndex = allOrders.indexOf(order) % CZECH_CITIES.size
        return OrderDetail(
            id = order.id,
            displayOrderNumber = order.displayOrderNumber,
            orderStatus = order.orderStatus,
            paymentStatus = order.paymentStatus,
            paymentType = CodeValue(type = "PaymentType", name = "Card", value = 1),
            cleaningDateTime = order.cleaningDateTime,
            customerName = order.customerName,
            customerEmail = order.customerEmail,
            customerPhone = order.customerPhone,
            address = OrderAddressInfo(
                street = PRAGUE_STREETS[cityIndex],
                city = CZECH_CITIES[cityIndex],
                zipCode = CZECH_ZIP_CODES[cityIndex],
                country = "Česká republika"
            ),
            totalPrice = order.totalPrice,
            currency = CurrencyDetail(
                id = "czk", code = "CZK", name = "Česká koruna",
                symbol = "Kč", exchangeRate = 1.0, isDefault = true
            ),
            selectedServices = order.selectedServices?.map {
                ServiceDetail(id = it.id, name = it.name, description = it.description, price = it.price, estimatedTime = 1) // 1 minute per service for testing
            },
            estimatedTime = order.estimatedTime,
            rooms = order.rooms,
            bathrooms = order.bathrooms,
            notes = "Prosím použijte vlastní čisticí prostředky.",
            specialInstructions = "Kočka se bojí vysavače - prosím buďte opatrní.",
            accessInstructions = "Kód ke vchodu: 1234#, 3. patro vlevo",
            createdOn = LocalDateTime.now().minusDays(5).format(DateTimeFormatter.ISO_LOCAL_DATE_TIME),
            updatedOn = LocalDateTime.now().format(DateTimeFormatter.ISO_LOCAL_DATE_TIME)
        )
    }

    // =========================================================================
    // Invoices
    // =========================================================================

    private val allInvoices: List<Invoice> by lazy { generateInvoices(25) }

    private fun generateInvoices(count: Int): List<Invoice> {
        val today = LocalDate.now()
        return (0 until count).map { i ->
            val status = when {
                i < 3 -> InvoiceStatus.PENDING
                i < 6 -> InvoiceStatus.APPROVED
                i < 20 -> InvoiceStatus.PAID
                i < 22 -> InvoiceStatus.DISPUTED
                i < 24 -> InvoiceStatus.REJECTED
                else -> InvoiceStatus.CANCELLED
            }
            val periodStart = today.minusMonths(i.toLong()).withDayOfMonth(1)
            val periodEnd = periodStart.plusMonths(1).minusDays(1)
            val totalOrders = (i % 11) + 5
            val subTotal = totalOrders * listOf(450.0, 800.0, 1200.0)[i % 3]
            val bonus = if (totalOrders > 10) 500.0 else 0.0

            Invoice(
                id = "inv-${2000 + i}",
                invoiceNumber = "INV-2024-${2001 + i}",
                employeeId = MOCK_EMPLOYEE_ID,
                employeeName = "$MOCK_FIRST_NAME $MOCK_LAST_NAME",
                payPeriodId = "period-$i",
                payPeriodLabel = periodStart.format(DateTimeFormatter.ofPattern("MM/yyyy")),
                variableSymbol = "${3000 + i}",
                totalOrders = totalOrders,
                subTotal = subTotal,
                bonusAmount = bonus,
                deductionAmount = 0.0,
                totalAmount = subTotal + bonus,
                currencyCode = "CZK",
                status = status.apiValue,
                generatedAt = periodEnd.plusDays(1).format(DateTimeFormatter.ISO_LOCAL_DATE),
                approvedAt = if (status in listOf(InvoiceStatus.APPROVED, InvoiceStatus.PAID))
                    periodEnd.plusDays(3).format(DateTimeFormatter.ISO_LOCAL_DATE) else null,
                paidAt = if (status == InvoiceStatus.PAID)
                    periodEnd.plusDays(7).format(DateTimeFormatter.ISO_LOCAL_DATE) else null
            )
        }
    }

    fun getPagedInvoices(
        page: Int,
        pageSize: Int,
        filter: InvoiceFilter?,
        sortBy: String?,
        sortDescending: Boolean?
    ): PagedInvoiceResponse {
        var filtered = allInvoices.toList()
        filter?.statuses?.let { statuses ->
            if (statuses.isNotEmpty()) {
                filtered = filtered.filter { inv ->
                    InvoiceStatus.fromApiValue(inv.status) in statuses
                }
            }
        }
        filter?.searchTerm?.let { term ->
            if (term.isNotBlank()) {
                filtered = filtered.filter {
                    it.invoiceNumber?.contains(term, ignoreCase = true) == true ||
                            it.variableSymbol?.contains(term, ignoreCase = true) == true
                }
            }
        }
        if (sortBy == "generatedAt") {
            filtered = if (sortDescending == true) filtered.sortedByDescending { it.generatedAt } else filtered.sortedBy { it.generatedAt }
        } else if (sortBy == "totalAmount") {
            filtered = if (sortDescending == true) filtered.sortedByDescending { it.totalAmount } else filtered.sortedBy { it.totalAmount }
        }
        val offset = (page - 1) * pageSize
        val paged = filtered.drop(offset).take(pageSize)
        return PagedInvoiceResponse(
            data = paged,
            pageNumber = page,
            pageSize = pageSize,
            total = filtered.size
        )
    }

    fun getInvoiceDetail(invoiceId: String): InvoiceDetail {
        val invoice = allInvoices.find { it.id == invoiceId } ?: allInvoices.first()
        val orderCount = invoice.totalOrders ?: 5
        val orderItems = (1..orderCount).map { i ->
            InvoiceOrderItem(
                orderId = "order-inv-$i",
                orderNumber = "CLN-2024-${3000 + i}",
                completedDate = LocalDate.now().minusDays(i.toLong()).format(DateTimeFormatter.ISO_LOCAL_DATE),
                serviceName = CLEANING_SERVICES[i % CLEANING_SERVICES.size].name,
                amount = listOf(450.0, 800.0, 1200.0)[i % 3],
                currency = "CZK"
            )
        }
        val statusEnum = InvoiceStatus.fromApiValue(invoice.status)
        return InvoiceDetail(
            id = invoice.id,
            invoiceNumber = invoice.invoiceNumber ?: "",
            statusValue = statusEnum.displayName,
            periodStart = invoice.payPeriodLabel,
            periodEnd = null,
            issueDateValue = invoice.generatedAt,
            dueDateValue = invoice.generatedAt,
            paidDateValue = invoice.paidAt,
            subtotal = invoice.subTotal ?: 0.0,
            taxAmount = 0.0,
            totalAmount = invoice.totalAmount ?: 0.0,
            currency = "CZK",
            orders = orderItems,
            employeeId = MOCK_EMPLOYEE_ID,
            employeeName = "$MOCK_FIRST_NAME $MOCK_LAST_NAME",
            variableSymbol = invoice.variableSymbol,
            payPeriodLabel = invoice.payPeriodLabel,
            bonusAmount = invoice.bonusAmount,
            deductionAmount = invoice.deductionAmount,
            totalOrders = invoice.totalOrders
        )
    }

    // =========================================================================
    // Profile
    // =========================================================================

    fun employeeProfile() = EmployeeProfile(
        id = MOCK_EMPLOYEE_ID,
        userId = MOCK_USER_ID,
        email = MOCK_EMAIL,
        firstName = MOCK_FIRST_NAME,
        lastName = MOCK_LAST_NAME,
        phoneNumber = "+420 608 123 456",
        dateOfBirth = "1990-03-15",
        nationality = "Česká republika",
        nationalId = "900315/1234",
        taxId = "CZ9003151234",
        street = "Vinohradská 48",
        city = "Praha 2",
        zipCode = "120 00",
        country = "Česká republika",
        countryId = "cz",
        bankName = "Česká spořitelna",
        accountNumber = "1234567890/0800",
        iban = "CZ65 0800 0000 0012 3456 7890",
        swiftCode = "GIBACZPX",
        emergencyContactName = "Marie Nováková",
        emergencyContactRelationship = "Spouse",
        emergencyContactPhone = "+420 607 654 321",
        emergencyContactEmail = "marie.novakova@email.cz",
        termsAccepted = true,
        termsAcceptedAt = "2024-01-15T10:00:00",
        isActive = true,
        isVerified = true,
        createdAt = "2024-01-15T10:00:00",
        updatedAt = "2024-06-20T14:30:00"
    )

    fun employeeDocuments() = listOf(
        EmployeeDocument(
            id = "doc-1",
            employeeId = MOCK_EMPLOYEE_ID,
            type = "IdCard",
            status = "Approved",
            fileName = "obcanka_predni.jpg",
            mimeType = "image/jpeg",
            fileSize = 245_000,
            uploadedAt = "2024-01-15T10:05:00",
            reviewedAt = "2024-01-16T09:00:00"
        ),
        EmployeeDocument(
            id = "doc-2",
            employeeId = MOCK_EMPLOYEE_ID,
            type = "TaxDocument",
            status = "Approved",
            fileName = "zivnostensky_list.pdf",
            mimeType = "application/pdf",
            fileSize = 512_000,
            uploadedAt = "2024-01-15T10:10:00",
            reviewedAt = "2024-01-16T09:05:00"
        ),
        EmployeeDocument(
            id = "doc-3",
            employeeId = MOCK_EMPLOYEE_ID,
            type = "Other",
            status = "Pending",
            fileName = "certifikat_hygiena.pdf",
            mimeType = "application/pdf",
            fileSize = 380_000,
            uploadedAt = "2024-06-15T14:00:00"
        )
    )
}
