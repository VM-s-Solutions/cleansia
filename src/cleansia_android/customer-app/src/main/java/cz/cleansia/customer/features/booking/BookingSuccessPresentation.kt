package cz.cleansia.customer.features.booking

import android.text.format.DateFormat
import cz.cleansia.core.format.formatOrderPrice
import cz.cleansia.customer.R
import cz.cleansia.customer.core.booking.CreateOrderResponse
import cz.cleansia.customer.core.booking.DirtinessLevel
import cz.cleansia.customer.core.booking.QuoteOrderResponse
import cz.cleansia.customer.core.catalog.ExtraListItem
import cz.cleansia.customer.core.catalog.PackageListItem
import cz.cleansia.customer.core.catalog.ServiceListItem
import cz.cleansia.customer.core.orders.OrderDetailDto
import java.time.Duration
import java.time.Instant
import java.time.ZoneId
import java.time.format.DateTimeFormatter
import java.util.Locale

internal enum class BookingStepState { Done, Active, Pending }

internal fun bookingSuccessSteps(statusValue: Int?, hasCleaner: Boolean): List<BookingStepState> {
    if (statusValue in 0..1 && hasCleaner) {
        return listOf(BookingStepState.Done, BookingStepState.Done, BookingStepState.Pending, BookingStepState.Pending)
    }
    val activeStep = when (statusValue) {
        0, 1 -> 1
        2, 3 -> 2
        4 -> 3
        5 -> 4
        else -> 0
    }
    return List(4) { index ->
        when {
            index == 0 || index < activeStep -> BookingStepState.Done
            index == activeStep && activeStep > 0 -> BookingStepState.Active
            else -> BookingStepState.Pending
        }
    }
}

internal val androidBestPattern: (Locale, String) -> String = { locale, skeleton -> DateFormat.getBestDateTimePattern(locale, skeleton) }

internal data class BookingAppointmentDate(
    val month: String,
    val day: String,
    val dayLine: String,
    val time: String,
)

internal fun bookingAppointmentDate(
    iso: String?,
    estimatedMinutes: Int,
    locale: Locale = Locale.getDefault(),
    zoneId: ZoneId = ZoneId.systemDefault(),
    bestPattern: (Locale, String) -> String = androidBestPattern,
): BookingAppointmentDate? {
    if (iso.isNullOrBlank()) return null
    val start = runCatching { Instant.parse(iso).atZone(zoneId) }.getOrNull() ?: return null
    fun format(pattern: String) = start.format(DateTimeFormatter.ofPattern(pattern, locale))
    val startTime = format("HH:mm")
    val time = if (estimatedMinutes > 0) {
        val end = start.plusMinutes(estimatedMinutes.toLong())
        val endPattern = if (end.toLocalDate() == start.toLocalDate()) "HH:mm" else "d MMM HH:mm"
        "$startTime–${end.format(DateTimeFormatter.ofPattern(endPattern, locale))}"
    } else startTime
    return BookingAppointmentDate(format("MMM"), format("d"), format(bestPattern(locale, "EEEEMMMMd")), time)
}

/**
 * The booking that just went through, as the confirmation shows it before the server's order lands: what the
 * review step already knew, names resolved in the app's language at hand-over. No amounts — a concurrent
 * booking can spend the credit first, so only the order says what was paid; [creditApplied] only reserves the
 * payment rows. Serializable so the shell keeps it across process death.
 */
data class BookingSuccess(
    val confirmationCode: String,
    val orderId: String,
    val cleaningDateTime: String? = null,
    val estimatedMinutes: Int = 0,
    val street: String? = null,
    val city: String? = null,
    val creditApplied: Boolean = false,
    val requiredEmployees: Int = 0,
    val rooms: Int = 0,
    val bathrooms: Int = 0,
    val dirtiness: DirtinessLevel? = null,
    val basketNames: List<String> = emptyList(),
    val extraNames: List<String> = emptyList(),
    val paysCash: Boolean = false,
) : java.io.Serializable

internal fun bookingSuccessOf(
    response: CreateOrderResponse,
    draft: BookingState,
    quote: QuoteOrderResponse?,
    discount: Double,
    services: List<ServiceListItem> = emptyList(),
    packages: List<PackageListItem> = emptyList(),
    extras: List<ExtraListItem> = emptyList(),
    language: String? = null,
): BookingSuccess = BookingSuccess(
    confirmationCode = response.confirmationCode,
    orderId = response.id,
    cleaningDateTime = draft.selectedInstant?.toString(),
    estimatedMinutes = quote?.estimatedDurationMinutes ?: 0,
    street = draft.street.takeIf { it.isNotBlank() },
    city = draft.city.takeIf { it.isNotBlank() },
    creditApplied = draft.paymentMethod == BookingViewModel.PAYMENT_CARD &&
        BookingPriceSummary.resolve(quote, discount, payByCard = true).creditApplied > 0.0,
    requiredEmployees = quote?.requiredEmployees ?: 0,
    rooms = draft.rooms,
    bathrooms = draft.bathrooms,
    dirtiness = draft.dirtinessLevel,
    basketNames = services.filter { it.id in draft.selectedServiceIds }.map { pickTranslatedName(it.translations, language, it.name) } +
        packages.filter { it.id in draft.selectedPackageIds }.map { pickTranslatedName(it.translations, language, it.name) },
    extraNames = extras.filter { it.slug in draft.selectedExtraSlugs }.sortedBy { it.displayOrder }
        .map { pickTranslatedName(it.translations, language, it.name) },
    paysCash = draft.paymentMethod == BookingViewModel.PAYMENT_CASH,
)

internal fun BookingSuccess.appointment(
    order: OrderDetailDto?,
    locale: Locale,
    zoneId: ZoneId = ZoneId.systemDefault(),
    bestPattern: (Locale, String) -> String = androidBestPattern,
): BookingAppointmentDate? = bookingAppointmentDate(
    order?.cleaningDateTime?.takeIf { it.isNotBlank() } ?: cleaningDateTime,
    order?.estimatedTime?.takeIf { it > 0 } ?: estimatedMinutes,
    locale,
    zoneId,
    bestPattern,
)

/** "Zenklova 6, Praha 8", with the house number and the district bound to the word before them. */
internal fun appointmentAddress(street: String?, city: String?): String? =
    listOfNotNull(
        street?.trim()?.takeIf { it.isNotEmpty() }?.replace(Regex(" (?=\\S+$)"), "\u00A0"),
        city?.trim()?.takeIf { it.isNotEmpty() }?.replace(' ', '\u00A0'),
    ).takeIf { it.isNotEmpty() }?.joinToString(", ")

internal fun BookingSuccess.dirtinessShown(): DirtinessLevel? = dirtiness?.takeIf { it != DirtinessLevel.Normal }

internal fun BookingSuccess.remedyRes(): Int =
    if (paysCash) R.string.booking_success_no_cleaner_cash else R.string.booking_success_no_cleaner_card

/**
 * The free-cancellation deadline from the order's own window, or null when the order states none or the
 * deadline is already behind: the sheet then says cancelling is free while no cleaner has taken it.
 */
internal fun freeCancellationUntil(cleaningAt: String?, freeCancellationHours: Int?, now: Instant): Instant? {
    val hours = freeCancellationHours ?: return null
    val start = cleaningAt?.let { runCatching { Instant.parse(it) }.getOrNull() } ?: return null
    return start.minus(Duration.ofHours(hours.toLong())).takeIf { it.isAfter(now) }
}

internal fun deadlineLabel(
    deadline: Instant,
    locale: Locale,
    zoneId: ZoneId = ZoneId.systemDefault(),
    bestPattern: (Locale, String) -> String = androidBestPattern,
): String = deadline.atZone(zoneId).format(DateTimeFormatter.ofPattern(bestPattern(locale, "EEEMMMdHHmm"), locale))

/**
 * Before the order lands its window is unknown, but a slot a day or more ahead has its deadline ahead under
 * every plan's window, so the sentence's start can already stand beside a bar.
 */
internal fun slotADayAhead(cleaningAt: String?, now: Instant): Boolean {
    val start = cleaningAt?.let { runCatching { Instant.parse(it) }.getOrNull() } ?: return false
    return !start.isBefore(now.plus(Duration.ofDays(1)))
}

/** Label → figure, Total first; a null figure is a bar while loading and a dash when unavailable. */
internal data class ConfirmationMoney(val rows: List<Pair<Int, String?>>, val note: Int?)

internal fun BookingSuccess.money(
    uiState: BookingSuccessUiState,
    format: (Double, String?) -> String = { amount, code -> formatOrderPrice(amount, code) },
): ConfirmationMoney {
    val total = R.string.booking_success_total_label
    if (uiState == BookingSuccessUiState.Error) {
        return ConfirmationMoney(listOf(total to null), R.string.booking_success_details_unavailable)
    }
    val order = (uiState as? BookingSuccessUiState.Loaded)?.order
    fun price(amount: Double) = format(amount, order?.currency?.code)
    if (paysCash) {
        return ConfirmationMoney(listOf(total to order?.let { price(it.totalPrice - it.creditAppliedAmount) }), R.string.booking_success_cash_on_day)
    }
    val card = if (order != null && order.paymentStatus?.value == 3) R.string.booking_summary_due_on_card else R.string.order_paid_by_card
    val credit = if (order != null) order.creditAppliedAmount > 0.0 else creditApplied
    if (!credit) return ConfirmationMoney(listOf(total to order?.let { price(it.totalPrice) }), card)
    return ConfirmationMoney(
        listOf(
            total to order?.let { price(it.totalPrice) },
            R.string.order_paid_with_credit to order?.let { "−" + price(it.creditAppliedAmount) },
            card to order?.let { price(it.amountDueOnCard) },
        ),
        null,
    )
}

internal data class Tiers(
    val pMin: Double,
    val pNat: Double,
    val pMax: Double,
    val pOver: Double,
    val hdMin: Double,
    val hdCap: Double,
)

internal data class Spare(val header: Double, val rowPad: Double, val rest: Double)

internal enum class ConfirmationSize(val tiers: Tiers) {
    Compact(Tiers(pMin = 7.0, pNat = 10.0, pMax = 14.0, pOver = 16.0, hdMin = 64.0, hdCap = 96.0)),
    Regular(Tiers(pMin = 10.0, pNat = 13.0, pMax = 20.0, pOver = 24.0, hdMin = 96.0, hdCap = 116.0)),
    Tall(Tiers(pMin = 10.0, pNat = 14.0, pMax = 20.0, pOver = 24.0, hdMin = 104.0, hdCap = 136.0)),
    Ax(Tiers(pMin = 14.0, pNat = 14.0, pMax = 14.0, pOver = 14.0, hdMin = 96.0, hdCap = 96.0)),
}

internal fun confirmationSize(room: Double, fontScale: Float): ConfirmationSize = when {
    fontScale > 1.3f -> ConfirmationSize.Ax
    room < 560.0 -> ConfirmationSize.Compact
    room >= 700.0 -> ConfirmationSize.Tall
    else -> ConfirmationSize.Regular
}

internal const val TicketRowSides = 8

/**
 * Spends the sheet's spare height in fixed tiers so it never collects into one band: rows to their
 * natural padding, the header to its cap, rows to their cap, rows to their over-cap, then a rest.
 * [natural] is the content laid out at `pMin` and `hdMin`; eight row sides share each row tier.
 */
internal fun distribute(available: Double, natural: Double, tiers: Tiers): Spare {
    var slack = (available - natural).coerceAtLeast(0.0)
    var pad = tiers.pMin
    fun rowsTo(cap: Double) {
        val take = minOf(slack, (cap - pad).coerceAtLeast(0.0) * TicketRowSides)
        pad += take / TicketRowSides
        slack -= take
    }
    rowsTo(tiers.pNat)
    val header = tiers.hdMin + minOf(slack, (tiers.hdCap - tiers.hdMin).coerceAtLeast(0.0))
    slack -= header - tiers.hdMin
    rowsTo(tiers.pMax)
    rowsTo(tiers.pOver)
    return Spare(header = header, rowPad = pad, rest = slack)
}
