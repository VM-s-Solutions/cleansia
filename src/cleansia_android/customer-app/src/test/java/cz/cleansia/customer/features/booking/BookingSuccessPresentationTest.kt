package cz.cleansia.customer.features.booking

import cz.cleansia.core.network.ApiResult
import cz.cleansia.customer.R
import cz.cleansia.customer.core.booking.CreateOrderResponse
import cz.cleansia.customer.core.booking.DirtinessLevel
import cz.cleansia.customer.core.booking.QuoteOrderResponse
import cz.cleansia.customer.core.catalog.CategoryDto
import cz.cleansia.customer.core.catalog.ExtraListItem
import cz.cleansia.customer.core.catalog.PackageListItem
import cz.cleansia.customer.core.catalog.ServiceListItem
import cz.cleansia.customer.core.catalog.TranslationDto
import cz.cleansia.customer.core.orders.OrderCurrencyDetailDto
import cz.cleansia.customer.core.orders.OrderApi
import cz.cleansia.customer.core.orders.OrderDetailDto
import cz.cleansia.customer.core.orders.OrderRepository
import cz.cleansia.customer.core.user.CodeDto
import cz.cleansia.customer.testing.MainDispatcherRule
import io.mockk.coEvery
import io.mockk.coVerify
import io.mockk.mockk
import java.io.ByteArrayInputStream
import java.io.ByteArrayOutputStream
import java.io.IOException
import java.io.ObjectInputStream
import java.io.File
import java.io.ObjectOutputStream
import java.time.ZoneId
import java.util.Locale
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.runTest
import kotlinx.datetime.Instant
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test
import retrofit2.Response

/** What Android's ICU answers for the two skeletons the sheet asks for, in the two locales it is checked in. */
internal val icuPatterns: (Locale, String) -> String = { locale, skeleton ->
    when (locale.language to skeleton) {
        "en" to "EEEEMMMMd" -> "EEEE, MMMM d"
        "cs" to "EEEEMMMMd" -> "EEEE d. MMMM"
        "en" to "EEEMMMdHHmm" -> "EEE, MMM d, HH:mm"
        "cs" to "EEEMMMdHHmm" -> "EEE d. M. H:mm"
        else -> error("no pattern pinned for $locale $skeleton")
    }
}

class BookingSuccessPresentationTest {
    @Test
    fun `unloaded and unknown status never claim cleaner assignment`() {
        assertEquals(listOf(BookingStepState.Done, BookingStepState.Pending, BookingStepState.Pending, BookingStepState.Pending), bookingSuccessSteps(null, false))
        assertEquals(bookingSuccessSteps(null, false), bookingSuccessSteps(99, true))
    }

    @Test
    fun `a new booking is received and finding a cleaner`() {
        listOf(0, 1).forEach { status ->
            assertEquals(listOf(BookingStepState.Done, BookingStepState.Active, BookingStepState.Pending, BookingStepState.Pending), bookingSuccessSteps(status, false))
        }
    }

    @Test
    fun `assignment and confirmation use the known order stage`() {
        listOf(0, 1).forEach { status ->
            assertEquals(listOf(BookingStepState.Done, BookingStepState.Done, BookingStepState.Pending, BookingStepState.Pending), bookingSuccessSteps(status, true))
        }
        listOf(2, 3).forEach { status ->
            assertEquals(listOf(BookingStepState.Done, BookingStepState.Done, BookingStepState.Active, BookingStepState.Pending), bookingSuccessSteps(status, true))
        }
        assertEquals(bookingSuccessSteps(2, true), bookingSuccessSteps(2, false))
    }

    @Test
    fun `cleaning completion and cancellation do not retain a false active stage`() {
        assertEquals(listOf(BookingStepState.Done, BookingStepState.Done, BookingStepState.Done, BookingStepState.Active), bookingSuccessSteps(4, true))
        assertEquals(List(4) { BookingStepState.Done }, bookingSuccessSteps(5, true))
        assertEquals(listOf(BookingStepState.Done, BookingStepState.Pending, BookingStepState.Pending, BookingStepState.Pending), bookingSuccessSteps(6, true))
    }

    private val prague = ZoneId.of("Europe/Prague")

    @Test
    fun `appointment uses the actual local date and estimate`() {
        val date = bookingAppointmentDate("2026-10-31T08:00:00Z", 180, Locale.US, prague, icuPatterns)!!
        assertEquals("Oct", date.month)
        assertEquals("31", date.day)
        assertEquals("Saturday, October 31", date.dayLine)
        assertEquals("09:00–12:00", date.time)
    }

    @Test
    fun `locale changes the appointment labels and missing dates stay absent`() {
        val date = bookingAppointmentDate("2026-10-31T08:00:00Z", 0, Locale.forLanguageTag("cs"), prague, icuPatterns)!!
        assertEquals("sobota 31. října", date.dayLine)
        assertEquals("09:00", date.time)
        assertNull(bookingAppointmentDate(null, 120, bestPattern = icuPatterns))
        assertNull(bookingAppointmentDate("invalid", 120, bestPattern = icuPatterns))
    }

    @Test
    fun `a visit across midnight includes the end date`() {
        val date = bookingAppointmentDate("2026-10-31T22:00:00Z", 180, Locale.UK, prague) { _, _ -> "d MMMM" }!!
        assertEquals("23:00–1 Nov 02:00", date.time)
    }

    private val submitted = BookingState(
        street = "Vinohradská 46",
        city = "Praha 2",
        selectedInstant = Instant.parse("2026-10-31T08:00:00Z"),
        paymentMethod = BookingViewModel.PAYMENT_CARD,
    )

    private fun quote(creditBalance: Double = 0.0, creditShare: Double = 0.0) = QuoteOrderResponse(
        totalPrice = 1660.0,
        finalPriceAfterDiscount = 1660.0,
        originalSubtotal = 1660.0,
        appliedDiscountSource = 0,
        currencyId = "cur-1",
        currencyCode = "CZK",
        servicesSubtotal = 1660.0,
        packagesSubtotal = 0.0,
        extrasSubtotal = 0.0,
        expressSurchargeApplied = false,
        expressSurchargeAmount = 0.0,
        expressSurchargeWaivedByMembership = false,
        estimatedDurationMinutes = 180,
        requiredEmployees = 1,
        creditBalance = creditBalance,
        creditMaxShareOfOrder = creditShare,
    )

    @Test
    fun `the confirmation is filled from the booking just submitted`() {
        val booked = bookingSuccessOf(CreateOrderResponse(id = "ord-1", confirmationCode = "CLN-48271"), submitted, quote(), 0.0)
        assertEquals(
            BookingSuccess(
                confirmationCode = "CLN-48271",
                orderId = "ord-1",
                cleaningDateTime = "2026-10-31T08:00:00Z",
                estimatedMinutes = 180,
                street = "Vinohradská 46",
                city = "Praha 2",
                creditApplied = false,
                requiredEmployees = 1,
                rooms = 1,
                bathrooms = 1,
            ),
            booked,
        )
        val time = booked.appointment(null, Locale.US, prague, icuPatterns)!!
        assertEquals("Saturday, October 31" to "09:00–12:00", time.dayLine to time.time)
    }

    @Test
    fun `credit rows are reserved only for a card booking the quote lets credit settle`() {
        val response = CreateOrderResponse(id = "ord-1", confirmationCode = "CLN-48271")
        assertTrue(bookingSuccessOf(response, submitted, quote(creditBalance = 300.0, creditShare = 0.5), 0.0).creditApplied)
        assertFalse(bookingSuccessOf(response, submitted, quote(), 0.0).creditApplied)
        val cash = submitted.copy(paymentMethod = BookingViewModel.PAYMENT_CASH)
        assertFalse(bookingSuccessOf(response, cash, quote(creditBalance = 300.0, creditShare = 0.5), 0.0).creditApplied)
        assertFalse(bookingSuccessOf(response, submitted, null, 0.0).creditApplied)
    }

    @Test
    fun `the server's order replaces the submitted appointment only where it says something`() {
        val booked = BookingSuccess("CLN-48271", "ord-1", "2026-10-31T08:00:00Z", 180, "Vinohradská 46", "Praha 2")
        val sameMoment = order.copy(cleaningDateTime = "2026-10-31T08:00:00Z", estimatedTime = 180)
        assertEquals(booked.appointment(null, Locale.UK, prague) { _, _ -> "d MMMM" }, booked.appointment(sameMoment, Locale.UK, prague) { _, _ -> "d MMMM" })
        val moved = order.copy(cleaningDateTime = "2026-10-31T09:00:00Z", estimatedTime = 120)
        assertEquals("10:00–12:00", booked.appointment(moved, Locale.UK, prague) { _, _ -> "d MMMM" }!!.time)
        assertEquals("09:00–12:00", booked.appointment(order, Locale.UK, prague) { _, _ -> "d MMMM" }!!.time)
    }

    private fun named(cs: String) = mapOf("cs" to TranslationDto(name = cs))
    private val category = CategoryDto(id = "cat", slug = "home", name = "Home")
    private val services = listOf(
        ServiceListItem("svc-window", "Window cleaning", basePrice = 300.0, perRoomPrice = 0.0, category = category, translations = named("Mytí oken")),
        ServiceListItem("svc-general", "General Cleaning", basePrice = 500.0, perRoomPrice = 150.0, category = category, translations = named("Obecný úklid")),
    )
    private val packages = listOf(PackageListItem("pkg-move", "Move-out package", price = 2490.0, translations = named("Balíček při stěhování")))
    private val extras = listOf(
        ExtraListItem("x-fridge", "inside-fridge", "Inside fridge cleaning", price = 150.0, displayOrder = 2, translations = named("Čištění vnitřku lednice")),
        ExtraListItem("x-oven", "inside-oven", "Inside oven cleaning", price = 200.0, displayOrder = 1, translations = named("Čištění vnitřku trouby")),
    )
    private val fullDraft = submitted.copy(
        selectedServiceIds = setOf("svc-general", "svc-window"),
        selectedPackageIds = setOf("pkg-move"),
        selectedExtraSlugs = setOf("inside-fridge", "inside-oven", "retired-extra"),
        rooms = 2,
        bathrooms = 1,
        dirtinessLevel = DirtinessLevel.Increased,
        paymentMethod = BookingViewModel.PAYMENT_CASH,
    )

    @Test
    fun `the hand-over captures every new field of the draft`() {
        val booked = bookingSuccessOf(
            CreateOrderResponse(id = "ord-1", confirmationCode = "130AA4"), fullDraft, quote().copy(requiredEmployees = 2), 0.0,
            services, packages, extras, "cs",
        )
        assertEquals(2, booked.requiredEmployees)
        assertEquals(2 to 1, booked.rooms to booked.bathrooms)
        assertEquals(DirtinessLevel.Increased, booked.dirtiness)
        assertEquals(listOf("Mytí oken", "Obecný úklid", "Balíček při stěhování"), booked.basketNames)
        assertEquals(listOf("Čištění vnitřku trouby", "Čištění vnitřku lednice"), booked.extraNames)
        assertTrue(booked.paysCash)
    }

    @Test
    fun `basket names are services then packages, in the app language, falling back to the catalogue name`() {
        val booked = bookingSuccessOf(CreateOrderResponse(id = "ord-1", confirmationCode = "130AA4"), fullDraft, quote(), 0.0, services, packages, extras, "uk")
        assertEquals(listOf("Window cleaning", "General Cleaning", "Move-out package"), booked.basketNames)
        assertEquals("Window cleaning · General Cleaning · Move-out package", booked.basketNames.joinToString(" · "))
    }

    @Test
    fun `an extra is named from the catalogue and never by its slug`() {
        val booked = bookingSuccessOf(CreateOrderResponse(id = "ord-1", confirmationCode = "130AA4"), fullDraft, quote(), 0.0, services, packages, extras, "en")
        assertEquals(listOf("Inside oven cleaning", "Inside fridge cleaning"), booked.extraNames)
        assertTrue(booked.extraNames.none { "-" in it })
        val noCatalogue = bookingSuccessOf(CreateOrderResponse(id = "ord-1", confirmationCode = "130AA4"), fullDraft, quote(), 0.0)
        assertEquals(emptyList<String>(), noCatalogue.extraNames)
    }

    @Test
    fun `the dirtiness joins the basket line only when it is not Normal`() {
        val booked = BookingSuccess("130AA4", "ord-1")
        assertNull(booked.dirtinessShown())
        assertNull(booked.copy(dirtiness = DirtinessLevel.Normal).dirtinessShown())
        assertEquals(DirtinessLevel.Heavy, booked.copy(dirtiness = DirtinessLevel.Heavy).dirtinessShown())
    }

    @Test
    fun `the booking just made survives process death with every captured field`() {
        val booked = bookingSuccessOf(
            CreateOrderResponse(id = "ord-1", confirmationCode = "130AA4"), fullDraft, quote().copy(requiredEmployees = 2), 0.0,
            services, packages, extras, "cs",
        )
        val bytes = ByteArrayOutputStream().also { ObjectOutputStream(it).use { out -> out.writeObject(booked) } }.toByteArray()
        assertEquals(booked, ObjectInputStream(ByteArrayInputStream(bytes)).use { it.readObject() })
        val single = booked.copy(basketNames = listOf("Obecný úklid"), extraNames = emptyList())
        val again = ByteArrayOutputStream().also { ObjectOutputStream(it).use { out -> out.writeObject(single) } }.toByteArray()
        assertEquals(single, ObjectInputStream(ByteArrayInputStream(again)).use { it.readObject() })
    }

    @Test
    fun `the house number and the district never split from their word`() {
        assertEquals("Zenklova\u00A06, Praha\u00A08", appointmentAddress("Zenklova 6", "Praha 8"))
        assertEquals("Vinohradská\u00A01024/46, Praha\u00A02", appointmentAddress("Vinohradská 1024/46", "Praha 2"))
        assertEquals("Zenklova\u00A06", appointmentAddress("Zenklova 6", " "))
        assertNull(appointmentAddress(null, null))
    }

    @Test
    fun `the remedy follows the payment type in every state`() {
        val card = BookingSuccess("130AA4", "ord-1")
        val cash = card.copy(paysCash = true)
        listOf(BookingSuccessUiState.Loaded(order), BookingSuccessUiState.Loading, BookingSuccessUiState.Error).forEach { state ->
            assertEquals("card, $state", R.string.booking_success_no_cleaner_card, card.remedyRes())
            assertEquals("cash, $state", R.string.booking_success_no_cleaner_cash, cash.remedyRes())
        }
    }

    private val booked = java.time.Instant.parse("2026-10-28T19:41:00Z")

    @Test
    fun `the standard window ends a day before the slot`() {
        val until = freeCancellationUntil("2026-10-31T08:00:00Z", 24, booked)!!
        assertEquals(java.time.Instant.parse("2026-10-30T08:00:00Z"), until)
        assertEquals("Fri, Oct 30, 09:00", deadlineLabel(until, Locale.US, prague, icuPatterns))
        assertEquals("pá 30. 10. 9:00", deadlineLabel(until, Locale.forLanguageTag("cs"), prague, icuPatterns))
    }

    @Test
    fun `a Plus member's four hour window ends at five on the day`() {
        val until = freeCancellationUntil("2026-10-31T08:00:00Z", 4, booked)!!
        assertEquals("Sat, Oct 31, 05:00", deadlineLabel(until, Locale.US, prague, icuPatterns))
    }

    @Test
    fun `a deadline already behind, or one the order does not state, falls back to the sentence`() {
        val late = java.time.Instant.parse("2026-10-30T17:00:00Z")
        assertNull(freeCancellationUntil("2026-10-31T08:00:00Z", 24, late))
        assertNull(freeCancellationUntil("2026-10-31T08:00:00Z", 24, java.time.Instant.parse("2026-10-30T08:00:00Z")))
        assertNull(freeCancellationUntil("2026-10-31T08:00:00Z", null, booked))
        assertNull(freeCancellationUntil(null, 24, booked))
        assertNull(freeCancellationUntil("not a date", 24, booked))
    }

    @Test
    fun `while loading, the sentence's start shows only for a slot a day or more ahead`() {
        assertTrue(slotADayAhead("2026-10-31T08:00:00Z", booked))
        assertTrue(slotADayAhead("2026-10-31T08:00:00Z", java.time.Instant.parse("2026-10-30T08:00:00Z")))
        assertFalse(slotADayAhead("2026-10-31T08:00:00Z", java.time.Instant.parse("2026-10-30T17:00:00Z")))
        assertFalse(slotADayAhead(null, booked))
    }

    @Test
    fun `no code path assumes a 24 hour window`() {
        val main = sequenceOf(File("."), File("customer-app"), File("src/cleansia_android/customer-app"))
            .map { File(it, "src/main/java/cz/cleansia/customer/features/booking") }
            .first { it.isDirectory }
        listOf("BookingSuccessPresentation.kt", "BookingSuccessScreen.kt", "BookingSuccessViewModel.kt").forEach { name ->
            val source = File(main, name).readText()
            listOf(
                Regex("\\?:\\s*24\\b"), Regex("\\b24L?\\s*\\*"), Regex("\\*\\s*24\\b"), Regex("[Hh]ours\\(\\s*24"),
                Regex("minus[A-Za-z]*\\(\\s*24"), Regex("\\b86_?400\\b"), Regex("\\b1440\\b"),
            ).forEach { pattern ->
                assertNull("$name assumes a 24 h window: ${pattern.find(source)?.value}", pattern.find(source))
            }
        }
    }

    private val plain: (Double, String?) -> String = { amount, code -> "${amount.toInt()} $code" }
    private val czk = OrderCurrencyDetailDto(code = "CZK", symbol = "Kč")
    private val cardOrder get() = order.copy(totalPrice = 1150.0, creditAppliedAmount = 250.0, amountDueOnCard = 900.0, currency = czk)

    @Test
    fun `card with credit reads three rows on one right edge`() {
        val money = BookingSuccess("130AA4", "ord-1", creditApplied = true).money(BookingSuccessUiState.Loaded(cardOrder), plain)
        assertEquals(
            listOf(
                R.string.booking_success_total_label to "1150 CZK",
                R.string.order_paid_with_credit to "−250 CZK",
                R.string.order_paid_by_card to "900 CZK",
            ),
            money.rows,
        )
        assertNull(money.note)
        val failed = cardOrder.copy(paymentStatus = CodeDto(type = "PaymentStatus", name = "Failed", value = 3))
        assertEquals(R.string.booking_summary_due_on_card, BookingSuccess("130AA4", "ord-1").money(BookingSuccessUiState.Loaded(failed), plain).rows[2].first)
    }

    @Test
    fun `card without credit states the total once and says how it was paid`() {
        val noCredit = cardOrder.copy(creditAppliedAmount = 0.0, amountDueOnCard = 1150.0)
        val money = BookingSuccess("130AA4", "ord-1").money(BookingSuccessUiState.Loaded(noCredit), plain)
        assertEquals(listOf(R.string.booking_success_total_label to "1150 CZK"), money.rows)
        assertEquals(R.string.order_paid_by_card, money.note)
        val failed = noCredit.copy(paymentStatus = CodeDto(type = "PaymentStatus", name = "Failed", value = 3))
        assertEquals(R.string.booking_summary_due_on_card, BookingSuccess("130AA4", "ord-1").money(BookingSuccessUiState.Loaded(failed), plain).note)
    }

    @Test
    fun `cash states the amount due once, with the cash line under it`() {
        val cashOrder = order.copy(totalPrice = 1000.0, currency = czk)
        val money = BookingSuccess("130AA4", "ord-1", paysCash = true).money(BookingSuccessUiState.Loaded(cashOrder), plain)
        assertEquals(listOf(R.string.booking_success_total_label to "1000 CZK"), money.rows)
        assertEquals(R.string.booking_success_cash_on_day, money.note)
    }

    @Test
    fun `unavailable details keep the total's label and guess no credit or card rows`() {
        listOf(BookingSuccess("130AA4", "ord-1", creditApplied = true), BookingSuccess("130AA4", "ord-1", paysCash = true)).forEach { booked ->
            val money = booked.money(BookingSuccessUiState.Error, plain)
            assertEquals(listOf(R.string.booking_success_total_label to null), money.rows)
            assertEquals(R.string.booking_success_details_unavailable, money.note)
        }
    }

    @Test
    fun `while loading, bars stand only where the draft says a row exists`() {
        val credit = BookingSuccess("130AA4", "ord-1", creditApplied = true).money(BookingSuccessUiState.Loading, plain)
        assertEquals(
            listOf(R.string.booking_success_total_label to null, R.string.order_paid_with_credit to null, R.string.order_paid_by_card to null),
            credit.rows,
        )
        assertNull(credit.note)
        val noCredit = BookingSuccess("130AA4", "ord-1").money(BookingSuccessUiState.Loading, plain)
        assertEquals(listOf(R.string.booking_success_total_label to null), noCredit.rows)
        assertEquals(R.string.order_paid_by_card, noCredit.note)
        val cash = BookingSuccess("130AA4", "ord-1", paysCash = true).money(BookingSuccessUiState.Loading, plain)
        assertEquals(listOf(R.string.booking_success_total_label to null), cash.rows)
        assertEquals(R.string.booking_success_cash_on_day, cash.note)
    }

    private val regular = ConfirmationSize.Regular.tiers
    private fun near(expected: Double, actual: Double) = assertEquals(expected, actual, 0.001)

    @Test
    fun `the size class follows the room the header and the ticket may use`() {
        assertEquals(ConfirmationSize.Compact, confirmationSize(512.0, 1f))
        assertEquals(ConfirmationSize.Compact, confirmationSize(559.9, 1f))
        assertEquals(ConfirmationSize.Regular, confirmationSize(560.0, 1f))
        assertEquals(ConfirmationSize.Regular, confirmationSize(659.0, 1f))
        assertEquals(ConfirmationSize.Regular, confirmationSize(699.9, 1f))
        assertEquals(ConfirmationSize.Tall, confirmationSize(700.0, 1f))
        assertEquals(ConfirmationSize.Tall, confirmationSize(717.0, 1.3f))
        assertEquals(ConfirmationSize.Ax, confirmationSize(717.0, 1.31f))
        assertEquals(ConfirmationSize.Ax, confirmationSize(512.0, 1.5f))
    }

    @Test
    fun `the regular phone's spare height lands as measured on the mock`() {
        val spare = distribute(available = 659.0, natural = 593.0, tiers = regular)
        near(116.0, spare.header)
        near(15.75, spare.rowPad)
        near(0.0, spare.rest)
        val tall = distribute(available = 717.0, natural = 639.0, tiers = ConfirmationSize.Tall.tiers)
        near(136.0, tall.header)
        near(15.75, tall.rowPad)
        near(0.0, tall.rest)
    }

    @Test
    fun `each tier fills in order and nothing rests until the rows are at their over-cap`() {
        fun at(slack: Double) = distribute(available = 593.0 + slack, natural = 593.0, tiers = regular)
        at(16.0).let { near(12.0, it.rowPad); near(96.0, it.header); near(0.0, it.rest) }
        at(24.0).let { near(13.0, it.rowPad); near(96.0, it.header); near(0.0, it.rest) }
        at(34.0).let { near(13.0, it.rowPad); near(106.0, it.header); near(0.0, it.rest) }
        at(44.0).let { near(13.0, it.rowPad); near(116.0, it.header); near(0.0, it.rest) }
        at(100.0).let { near(20.0, it.rowPad); near(116.0, it.header); near(0.0, it.rest) }
        at(132.0).let { near(24.0, it.rowPad); near(116.0, it.header); near(0.0, it.rest) }
        at(140.0).let { near(24.0, it.rowPad); near(116.0, it.header); near(8.0, it.rest) }
        at(-40.0).let { near(10.0, it.rowPad); near(96.0, it.header); near(0.0, it.rest) }
    }

    @Test
    fun `a compact phone's rows reach their cap only after its header, and stop at the over-cap`() {
        val compact = ConfirmationSize.Compact.tiers
        val natural = 500.0
        distribute(natural + 24 + 32, natural, compact).let { near(10.0, it.rowPad); near(96.0, it.header) }
        distribute(natural + 24 + 32 + 32, natural, compact).let { near(14.0, it.rowPad); near(0.0, it.rest) }
        distribute(natural + 400, natural, compact).let { near(16.0, it.rowPad); near(96.0, it.header); assertTrue(it.rest > 0) }
    }

    @Test
    fun `large text ignores the tiers`() {
        val ax = ConfirmationSize.Ax.tiers
        listOf(-100.0, 0.0, 300.0).forEach { slack ->
            val spare = distribute(800.0 + slack, 800.0, ax)
            near(96.0, spare.header)
            near(14.0, spare.rowPad)
        }
    }

    private val order = OrderDetailDto(
        id = "ord-1", totalPrice = 1660.0, originalSubtotal = 1660.0, appliedDiscountSource = 0,
        orderStatus = CodeDto(type = "OrderStatus", name = "New", value = 0),
    )
}

@OptIn(ExperimentalCoroutinesApi::class)
class BookingSuccessViewModelTest {
    @get:Rule
    val mainRule = MainDispatcherRule()

    private val orderApi = mockk<OrderApi>()
    private val orderRepository = mockk<OrderRepository> { coEvery { refresh() } returns ApiResult.Success(Unit) }
    private val order = OrderDetailDto(
        id = "ord-1", totalPrice = 1660.0, originalSubtotal = 1660.0, appliedDiscountSource = 0,
        orderStatus = CodeDto(type = "OrderStatus", name = "New", value = 0),
    )

    @Test
    fun `nothing is fetched until the sheet starts it`() = runTest {
        val vm = BookingSuccessViewModel(orderApi, orderRepository)
        advanceUntilIdle()
        assertEquals(BookingSuccessUiState.Loading, vm.state.value)
        coVerify(exactly = 0) { orderApi.getById(any()) }
    }

    @Test
    fun `start loads the order once however often the sheet recomposes`() = runTest {
        coEvery { orderApi.getById("ord-1") } returns Response.success(order)
        val vm = BookingSuccessViewModel(orderApi, orderRepository)
        vm.start("ord-1")
        vm.start("ord-1")
        advanceUntilIdle()
        assertEquals(BookingSuccessUiState.Loaded(order), vm.state.value)
        coVerify(exactly = 1) { orderApi.getById("ord-1") }
    }

    @Test
    fun `try again after a failure loads the same order again and lands it`() = runTest {
        var calls = 0
        coEvery { orderApi.getById("ord-1") } answers {
            calls++
            if (calls == 1) throw IOException("offline") else Response.success(order)
        }
        val vm = BookingSuccessViewModel(orderApi, orderRepository)
        vm.start("ord-1")
        advanceUntilIdle()
        assertEquals(BookingSuccessUiState.Error, vm.state.value)
        vm.retry()
        assertEquals(BookingSuccessUiState.Loading, vm.state.value)
        advanceUntilIdle()
        assertEquals(BookingSuccessUiState.Loaded(order), vm.state.value)
        coVerify(exactly = 2) { orderApi.getById("ord-1") }
    }

    @Test
    fun `try again that fails again reads as unavailable again`() = runTest {
        coEvery { orderApi.getById("ord-1") } throws IOException("offline")
        val vm = BookingSuccessViewModel(orderApi, orderRepository)
        vm.start("ord-1")
        advanceUntilIdle()
        vm.retry()
        assertEquals(BookingSuccessUiState.Loading, vm.state.value)
        advanceUntilIdle()
        assertEquals(BookingSuccessUiState.Error, vm.state.value)
        coVerify(exactly = 2) { orderApi.getById("ord-1") }
    }

    @Test
    fun `try again before the sheet started anything fetches nothing`() = runTest {
        val vm = BookingSuccessViewModel(orderApi, orderRepository)
        vm.retry()
        advanceUntilIdle()
        assertEquals(BookingSuccessUiState.Loading, vm.state.value)
        coVerify(exactly = 0) { orderApi.getById(any()) }
    }

    @Test
    fun `a blank id or a failed fetch reads as unavailable`() = runTest {
        val blank = BookingSuccessViewModel(orderApi, orderRepository)
        blank.start("")
        advanceUntilIdle()
        assertEquals(BookingSuccessUiState.Error, blank.state.value)
        coVerify(exactly = 0) { orderApi.getById(any()) }

        coEvery { orderApi.getById("ord-1") } throws IOException("offline")
        val failed = BookingSuccessViewModel(orderApi, orderRepository)
        failed.start("ord-1")
        advanceUntilIdle()
        assertEquals(BookingSuccessUiState.Error, failed.state.value)
    }
}
