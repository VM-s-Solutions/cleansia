package cz.cleansia.partner.features.orders

import android.content.res.Resources
import androidx.annotation.PluralsRes
import androidx.annotation.StringRes
import cz.cleansia.core.money.CurrencySymbols
import cz.cleansia.partner.R
import cz.cleansia.partner.api.model.HomeDistanceBand
import cz.cleansia.partner.api.model.OrderListItem
import cz.cleansia.partner.data.orders.PendingOffer
import cz.cleansia.partner.data.payroll.CashHeld
import java.time.DayOfWeek
import java.time.Duration
import java.time.Instant
import java.time.LocalDate
import java.time.ZoneId
import java.time.format.DateTimeFormatter
import java.time.temporal.ChronoUnit
import java.time.temporal.TemporalAdjusters
import java.util.Locale
import kotlin.math.roundToInt

/**
 * A piece of board copy as a resource id with its arguments, so the rules below stay testable without a
 * Compose host. An argument may itself be a [BoardText]; [resolve] resolves it first.
 */
sealed interface BoardText {
    data class Res(@StringRes val id: Int, val args: List<Any> = emptyList()) : BoardText
    data class Plural(@PluralsRes val id: Int, val count: Int, val args: List<Any> = listOf(count)) : BoardText
    data class Literal(val text: String) : BoardText
    data class Joined(val parts: List<BoardText>, val separator: String, val prefix: String = "") : BoardText
}

fun BoardText.resolve(resources: Resources): String = when (this) {
    is BoardText.Res ->
        if (args.isEmpty()) resources.getString(id) else resources.getString(id, *args.map { it.resolveArg(resources) }.toTypedArray())
    is BoardText.Plural -> resources.getQuantityString(id, count, *args.map { it.resolveArg(resources) }.toTypedArray())
    is BoardText.Literal -> text
    is BoardText.Joined -> prefix + parts.map { it.resolve(resources) }.filter { it.isNotBlank() }.joinToString(separator)
}

private fun Any.resolveArg(resources: Resources): Any = if (this is BoardText) resolve(resources) else this

/** Whether a row in someone's day is already running, or can still be joined until it ends. */
sealed interface LiveFlag {
    data class UnderWay(val until: Instant) : LiveFlag
    data class TakeUntil(val until: Instant) : LiveFlag
}

/** What the take sheet says about cancelling, before the take and once taken. */
sealed interface CancelLine {
    data class FreeUntil(val start: Instant) : CancelLine
    data object UnderWay : CancelLine
    data object StartPassed : CancelLine
}

enum class TakePhase { BeforeTake, Taken }

/** The Active hero's one control. There is no Complete: an in-progress job opens its detail. */
sealed interface HeroControl {
    data class SetOff(val opensAt: Instant?, val enabled: Boolean) : HeroControl
    data object Start : HeroControl
    data object OpenJob : HeroControl
}

data class HistoryTotals(val earnings: String, val jobs: Int, val hours: Int)

/** Minutes from the day's midnight, the end exclusive; a job running past midnight ends after 24:00. */
data class DaySpan(val start: Int, val end: Int)

/** The day line's hours, in minutes from midnight; one range for every day line on a page. */
data class DayRange(val from: Int, val to: Int)

/** An open job overlapping another open job takes a half-height lane, so each keeps its own outline. */
enum class SlotLane { Full, Top, Bottom }

data class OpenSlot(val span: DaySpan, val clash: DaySpan?, val lane: SlotLane)

data class DayMark(val mine: Boolean, val span: DaySpan, val lane: SlotLane)

/** [open] is null until Available has answered: the strip prints no count it does not know. */
data class WeekCell(val day: LocalDate, val open: Int?, val mine: Boolean)

/** Where an own job stands: the current step of its run, or closed. */
enum class RunStep { SetOff, GetIn, Clean, Finish, Done, Cancelled }

/**
 * The Orders board's pure rules (spec §3). `now`, the zone and the locale are injected; text rules return
 * resource ids with their arguments and the composable layer resolves them.
 */
object OrdersBoardRules {

    /**
     * A cleaner may set off at most this long before the start: the server refuses `NotifyOnTheWay` earlier.
     * Mirrors `BookingPolicy` → docs/product/business-rules.md (start grace window).
     */
    const val START_GRACE_WINDOW_MINUTES = 60L

    /** The server's page cap (`Limit` 1–100). */
    const val PAGE_SIZE = 100

    /** The board's " · " with a no-break space before the dot, so a wrapped line never starts with "·". */
    const val SEP = " · "

    /** Contract versions whose clauses the four summary lines are true to (spec D4). */
    val SUMMARY_VERSIONS: Set<String> = setOf("2026-10-05", "2026-10-27")

    /** Contract versions carrying the release and cash clauses (spec D4a). */
    val RELEASE_VERSIONS: Set<String> = setOf("2026-09-29", "2026-10-05", "2026-10-27")

    private const val CASH_PAYMENT_TYPE = 1

    private val KNOWN_EXTRAS = listOf("inside-oven", "inside-fridge", "interior-windows", "laundry-ironing", "pet-hair-supplement")

    private val HERO_RANK = mapOf(4 to 3, 3 to 2, 2 to 1)

    fun start(row: OrderListItem): Instant? =
        row.cleaningDateTime?.takeIf { it.isNotBlank() }?.let { runCatching { Instant.parse(it) }.getOrNull() }

    fun minutes(row: OrderListItem): Int = (row.estimatedTime ?: 0).coerceAtLeast(0)

    fun status(row: OrderListItem): Int? = row.orderStatus?.value

    fun isCash(row: OrderListItem): Boolean = row.paymentType?.value == CASH_PAYMENT_TYPE

    fun timeText(instant: Instant, zone: ZoneId, locale: Locale): String =
        DateTimeFormatter.ofPattern("HH:mm", locale).format(instant.atZone(zone))

    /** "09:00–13:00"; the start alone when there are no minutes. */
    fun window(start: Instant?, minutes: Int, zone: ZoneId, locale: Locale): String? {
        start ?: return null
        val from = timeText(start, zone, locale)
        if (minutes <= 0) return from
        return "$from–${timeText(start.plusSeconds(minutes * 60L), zone, locale)}"
    }

    /** A window that never breaks at its dash. */
    fun holdDash(window: String): String = window.replace("–", "\u2060–\u2060")

    fun bookedEnd(row: OrderListItem): Instant? = start(row)?.plusSeconds(minutes(row) * 60L)

    /** The server stops offering a row once its booked end has passed; the board hides it at render. */
    fun isExpired(row: OrderListItem, now: Instant): Boolean = bookedEnd(row)?.let { it <= now } ?: false

    fun liveFlag(row: OrderListItem, now: Instant): LiveFlag? {
        val end = bookedEnd(row) ?: return null
        return when (status(row)) {
            3, 4 -> LiveFlag.UnderWay(end)
            0, 2 -> if (start(row)!! <= now) LiveFlag.TakeUntil(end) else null
            else -> null
        }
    }

    /**
     * The server's clash rule (`LiveCommitmentsInWindow`): an own job A clashes with candidate C iff
     * `A.start < C.start + C.minutes` and `A.start + A.minutes > C.start`. Touching windows do not clash and
     * there is no buffer. Returns the earliest clashing own job.
     */
    fun clashes(candidate: OrderListItem, own: List<OrderListItem>): OrderListItem? {
        val cStart = start(candidate) ?: return null
        val cEnd = cStart.plusSeconds(minutes(candidate) * 60L)
        return own.asSequence()
            .filter { it.id != candidate.id }
            .mapNotNull { a -> start(a)?.let { a to it } }
            .filter { (a, aStart) -> aStart < cEnd && aStart.plusSeconds(minutes(a) * 60L) > cStart }
            .minByOrNull { it.second }
            ?.first
    }

    /** Status first (in progress, then on the way, then confirmed), then the earliest start. */
    fun heroPick(own: List<OrderListItem>): OrderListItem? =
        own.filter { HERO_RANK[status(it)] != null }
            .sortedWith(
                compareByDescending<OrderListItem> { HERO_RANK.getValue(status(it)!!) }
                    .thenBy(nullsLast()) { start(it) },
            )
            .firstOrNull()

    fun setOffOpensAt(row: OrderListItem): Instant? = start(row)?.minusSeconds(START_GRACE_WINDOW_MINUTES * 60)

    fun heroControl(row: OrderListItem, now: Instant): HeroControl? = when (status(row)) {
        2 -> setOffOpensAt(row).let { opensAt -> HeroControl.SetOff(opensAt, enabled = opensAt == null || now >= opensAt) }
        3 -> HeroControl.Start
        4 -> HeroControl.OpenJob
        else -> null
    }

    /**
     * The locked set-off's second line, on the board's hero and the detail alike: "in 3 h 40 min" when the set-off
     * opens today, else its day ("Monday · 16 Nov").
     */
    fun setOffWhen(opensAt: Instant, now: Instant, zone: ZoneId, locale: Locale, dayMonthPattern: String = "d MMM"): BoardText {
        val opens = opensAt.atZone(zone)
        if (opens.toLocalDate() == now.atZone(zone).toLocalDate()) {
            return BoardText.Res(R.string.active_hero_in, listOf(durationText(minutesUntil(now, opensAt))))
        }
        return BoardText.Literal(
            DateTimeFormatter.ofPattern("EEEE", locale).format(opens).replaceFirstChar { it.titlecase(locale) } + SEP +
                DateTimeFormatter.ofPattern(dayMonthPattern, locale).format(opens),
        )
    }

    /** The hero's right-hand time: booked-until in progress, "in 4 h 40 min" within a day, "was due at" once passed. */
    fun heroRightText(row: OrderListItem, now: Instant, zone: ZoneId, locale: Locale): BoardText? {
        val status = status(row)
        if (status == 4) return bookedEnd(row)?.let { BoardText.Res(R.string.active_hero_until, listOf(timeText(it, zone, locale))) }
        if (status != 2 && status != 3) return null
        val start = start(row) ?: return null
        if (start <= now) return BoardText.Res(R.string.active_hero_due, listOf(timeText(start, zone, locale)))
        val until = Duration.between(now, start)
        if (until >= Duration.ofHours(24)) return null
        val minutes = ((until.seconds + 59) / 60).toInt()
        return BoardText.Res(R.string.active_hero_in, listOf(durationText(minutes)))
    }

    fun cancelLine(status: Int?, start: Instant?, now: Instant, version: String?): CancelLine? = when (status) {
        3, 4 -> CancelLine.UnderWay
        0, 2 -> when {
            start == null -> null
            start > now -> if (version in RELEASE_VERSIONS) CancelLine.FreeUntil(start) else null
            else -> CancelLine.StartPassed
        }
        else -> null
    }

    /** The two "once you take it" sentences belong before the take only; once taken a passed start reads "can't cancel". */
    @StringRes
    fun cancelText(line: CancelLine?, phase: TakePhase): Int? = when (line) {
        null -> null
        is CancelLine.FreeUntil -> R.string.take_cancel_free_until
        CancelLine.UnderWay -> if (phase == TakePhase.BeforeTake) R.string.take_cancel_under_way else R.string.take_done_cannot_cancel
        CancelLine.StartPassed -> if (phase == TakePhase.BeforeTake) R.string.take_cancel_start_passed else R.string.take_done_cannot_cancel
    }

    fun summaryVisible(request: WorkContractRequest, version: String?): Boolean =
        (request is WorkContractRequest.Take || request is WorkContractRequest.Accept) && version in SUMMARY_VERSIONS

    /** The server writes `"{Street}, {City}, {ZipCode}"`; the street is everything before the last two parts. */
    fun streetLine(customerAddress: String?): String? {
        val text = customerAddress?.trim()?.takeIf { it.isNotEmpty() } ?: return null
        val parts = text.split(", ")
        return if (parts.size >= 3) parts.dropLast(2).joinToString(", ") else text
    }

    fun areaLine(row: OrderListItem): String? = row.customerAddressApproximate?.trim()?.takeIf { it.isNotEmpty() }

    /** The row's extras map has no stable order; only `true` values count. */
    fun extraSlugsOf(row: OrderListItem): List<String> = orderedExtras(row.extras.orEmpty().filterValues { it }.keys)

    /** Oven, fridge, interior windows, ironing, pet hair, then any other slug alphabetically. */
    fun orderedExtras(slugs: Collection<String>): List<String> {
        val distinct = slugs.distinct()
        return KNOWN_EXTRAS.filter { it in distinct } + distinct.filter { it !in KNOWN_EXTRAS }.sorted()
    }

    fun extraLabel(slug: String): BoardText = when (slug) {
        "inside-oven" -> BoardText.Res(R.string.extra_short_inside_oven)
        "inside-fridge" -> BoardText.Res(R.string.extra_short_inside_fridge)
        "interior-windows" -> BoardText.Res(R.string.extra_short_interior_windows)
        "laundry-ironing" -> BoardText.Res(R.string.extra_short_laundry_ironing)
        "pet-hair-supplement" -> BoardText.Res(R.string.extra_name_pet_hair_supplement)
        // As the detail's nameForExtraSlug: untranslated but readable, never blank.
        else -> BoardText.Literal(slug.replace('-', ' ').replaceFirstChar { it.uppercase() })
    }

    fun extrasLine(slugs: Collection<String>): BoardText? =
        orderedExtras(slugs).takeIf { it.isNotEmpty() }?.let { ordered ->
            BoardText.Joined(ordered.map(::extraLabel), separator = ", ", prefix = "+ ")
        }

    fun scopeLead(row: OrderListItem, lang: String?): String? =
        row.selectedPackages.orEmpty().firstNotNullOfOrNull { resolveTranslatedName(it.translations, lang, it.name)?.takeIf(String::isNotBlank) }
            ?: row.selectedServices.orEmpty().firstNotNullOfOrNull { resolveTranslatedName(it.translations, lang, it.name)?.takeIf(String::isNotBlank) }

    /** "Standard clean", "2 rooms", "1 bath", "+ oven, fridge": the scope's separator-flow items. */
    fun scopeItems(row: OrderListItem, lang: String?): List<BoardText> = listOfNotNull(
        scopeLead(row, lang)?.let { BoardText.Literal(it) },
        row.rooms?.takeIf { it > 0 }?.let { BoardText.Plural(R.plurals.scope_rooms, it) },
        row.bathrooms?.takeIf { it > 0 }?.let { BoardText.Plural(R.plurals.scope_baths, it) },
        extrasLine(extraSlugsOf(row)),
    )

    /** "Standard clean · 2 rooms · 1 bath · + oven, fridge". */
    fun scopeLine(row: OrderListItem, lang: String?): BoardText? =
        scopeItems(row, lang).takeIf { it.isNotEmpty() }?.let { BoardText.Joined(it, separator = SEP) }

    /** The snapped straight-line band from the cleaner's stored home (spec D2); nothing at all when null. */
    fun bandText(band: HomeDistanceBand?): BoardText? = when (band) {
        HomeDistanceBand._1 -> BoardText.Res(R.string.distance_band_under_3)
        HomeDistanceBand._2 -> BoardText.Res(R.string.distance_band_3_6)
        HomeDistanceBand._3 -> BoardText.Res(R.string.distance_band_6_10)
        HomeDistanceBand._4 -> BoardText.Res(R.string.distance_band_10_20)
        HomeDistanceBand._5 -> BoardText.Res(R.string.distance_band_over_20)
        null -> null
    }

    fun durationText(minutes: Int): BoardText = when {
        minutes < 60 -> BoardText.Res(R.string.orders_duration_min, listOf(minutes))
        minutes % 60 == 0 -> BoardText.Res(R.string.orders_duration_h, listOf(minutes / 60))
        else -> BoardText.Res(R.string.orders_duration_h_min, listOf(minutes / 60, minutes % 60))
    }

    /**
     * "2 cleaners needed · 1 already on it". Counts `assignedEmployeesCount`, never `assignedEmployees` (those
     * are seat ids). Available cards and the take sheet only, where "needed" is true.
     */
    fun teamFlag(row: OrderListItem): BoardText? {
        val required = row.requiredEmployees ?: return null
        if (required <= 1) return null
        val taken = row.assignedEmployeesCount ?: 0
        return BoardText.Joined(
            listOf(
                BoardText.Plural(R.plurals.crew_size, required),
                if (taken > 0) BoardText.Plural(R.plurals.orders_flag_crew_taken, taken) else BoardText.Res(R.string.orders_flag_crew_nobody),
            ),
            separator = SEP,
        )
    }

    /** "team of 2" on the cleaner's own job: "needed" is false once their seat is filled. */
    fun heroTeam(row: OrderListItem): BoardText? =
        row.requiredEmployees?.takeIf { it > 1 }?.let { BoardText.Plural(R.plurals.active_hero_team, it) }

    /** Orders' money format with every space a no-break space, so a figure never splits from its unit. */
    fun money(amount: Double, symbol: String?, locale: Locale): String = formatMoney(amount, symbol, locale).replace(' ', ' ')

    /** The row's own currency symbol, else the one its code names: the unit of every money figure on the board and the detail. */
    fun symbolOf(row: OrderListItem): String =
        row.currency?.symbol?.trim()?.takeIf { it.isNotEmpty() } ?: CurrencySymbols.forCode(row.currency?.code)

    /** The cleaner's own seat pay; unknown pay is hidden rather than printed as 0. */
    fun payText(row: OrderListItem, locale: Locale): String? = row.estimatedCleanerPay?.let { money(it, symbolOf(row), locale) }

    fun cashFlag(row: OrderListItem, locale: Locale): BoardText? {
        if (!isCash(row)) return null
        val due = row.amountDueOnCard ?: return null
        val words = if (cashCollected(row)) R.string.history_cash_collected else R.string.orders_flag_cash_collect
        return BoardText.Res(words, listOf(money(due, symbolOf(row), locale)))
    }

    /** A cash job whose payment is Paid: the cash is already collected for the company. */
    fun cashCollected(row: OrderListItem): Boolean = isCash(row) && row.paymentStatus?.value == PAYMENT_PAID

    /** Live offers by deadline, each with its board row; a null row is fetched by id, never invented. */
    fun reservedCards(offers: List<PendingOffer>, rows: List<OrderListItem>, now: Instant): List<Pair<PendingOffer, OrderListItem?>> =
        offers.mapNotNull { offer -> runCatching { Instant.parse(offer.respondByUtc) }.getOrNull()?.let { offer to it } }
            .filter { (_, respondBy) -> respondBy > now }
            .sortedBy { it.second }
            .map { (offer, _) -> offer to rows.firstOrNull { it.id == offer.id } }

    /** The reserved cards on show (§6.1 item 1): live offers joined to a loaded row that has not ended. */
    fun reservedShown(offers: List<PendingOffer>, rows: List<OrderListItem>, now: Instant): List<Pair<PendingOffer, OrderListItem>> =
        reservedCards(offers, rows, now).mapNotNull { (offer, row) -> row?.takeUnless { isExpired(it, now) }?.let { offer to it } }

    /** The job cards (§6.1 item 4): expired rows and rows shown as reserved cards left out, in the chosen order. */
    fun visibleCards(rows: List<OrderListItem>, reservedIds: Set<String?>, sort: AvailableSort, now: Instant): List<OrderListItem> =
        sorted(rows.filter { !isExpired(it, now) && it.id !in reservedIds }, sort)

    fun cashNotice(cashHeld: List<CashHeld>): CashHeld? = cashHeld.firstOrNull { it.cashJobsHidden }

    /** Held and limit, to the haléř when not whole. Null when the company sets no limit. */
    fun cashAmounts(cash: CashHeld, locale: Locale): Pair<String, String>? {
        val cap = cash.floatCap ?: return null
        val symbol = CurrencySymbols.forCode(cash.currencyCode)
        return money(cash.amount, symbol, locale) to money(cap, symbol, locale)
    }

    /** Per-currency pay of the period's completed rows; jobs is the server's total; hours are booked windows. */
    fun historyTotals(rows: List<OrderListItem>, total: Int?): HistoryTotals = HistoryTotals(
        earnings = formatEarningsTotal(rows),
        jobs = total ?: rows.size,
        hours = (rows.sumOf { minutes(it) } / 60.0).roundToInt(),
    )

    fun badge(count: Int?): String? = when {
        count == null || count <= 0 -> null
        count > 99 -> "99+"
        else -> count.toString()
    }

    /** Best pay is honest only as a local sort over every row (the server sorts pay within a page). */
    fun sortOptions(loaded: Int, total: Int?): List<AvailableSort> =
        if (total != null && loaded >= total) listOf(AvailableSort.SoonestFirst, AvailableSort.EarningsHighToLow) else listOf(AvailableSort.SoonestFirst)

    fun sorted(rows: List<OrderListItem>, sort: AvailableSort): List<OrderListItem> = when (sort) {
        AvailableSort.SoonestFirst -> rows
        AvailableSort.EarningsHighToLow -> rows.sortedWith(
            compareBy<OrderListItem> { it.estimatedCleanerPay == null }
                .thenByDescending { it.estimatedCleanerPay ?: 0.0 }
                .thenBy(nullsLast()) { start(it) },
        )
    }

    @StringRes
    fun relativeDayRes(start: Instant, now: Instant, zone: ZoneId): Int? {
        val day = start.atZone(zone).toLocalDate()
        val today = now.atZone(zone).toLocalDate()
        return when (day) {
            today -> R.string.day_today
            today.plusDays(1) -> R.string.day_tomorrow
            else -> null
        }
    }

    /** Standalone abbreviated month (`LLL`), never the genitive `MMM` ("МАЯ"). */
    fun tileMonth(date: LocalDate, locale: Locale): String =
        DateTimeFormatter.ofPattern("LLL", locale).format(date)

    /** "10:14" when the last success was today, else "Fri 30 Oct, 10:14" from the locale's day pattern. */
    fun staleWhen(lastSuccess: Instant, now: Instant, zone: ZoneId, locale: Locale, dayPattern: String): String {
        val time = timeText(lastSuccess, zone, locale)
        val local = lastSuccess.atZone(zone)
        if (local.toLocalDate() == now.atZone(zone).toLocalDate()) return time
        return DateTimeFormatter.ofPattern(dayPattern, locale).format(local) + ", " + time
    }

    /** Appends a page, keeping the row already held for an id seen twice. */
    fun mergePage(rows: List<OrderListItem>, page: List<OrderListItem>): List<OrderListItem> {
        val held = rows.mapNotNull { it.id }.toHashSet()
        return rows + page.filter { it.id == null || held.add(it.id!!) }
    }

    fun day(row: OrderListItem, zone: ZoneId): LocalDate? = start(row)?.atZone(zone)?.toLocalDate()

    /** One panel per day, in the order each day first appears, its rows in the list's order: Best pay leads with the best day. */
    fun dayGroups(rows: List<OrderListItem>, zone: ZoneId): List<Pair<LocalDate?, List<OrderListItem>>> {
        val groups = mutableListOf<Pair<LocalDate?, MutableList<OrderListItem>>>()
        rows.forEach { row ->
            val day = day(row, zone)
            groups.firstOrNull { it.first == day }?.second?.add(row) ?: groups.add(day to mutableListOf(row))
        }
        return groups
    }

    fun daySpan(row: OrderListItem, day: LocalDate, zone: ZoneId): DaySpan? {
        val local = start(row)?.atZone(zone) ?: return null
        if (local.toLocalDate() != day) return null
        val from = local.hour * 60 + local.minute
        return DaySpan(from, from + minutes(row))
    }

    /**
     * 07:00–19:00, widened to the whole hours that hold the earliest block and the latest end. Bookings start
     * 08:00–19:45 and run up to four hours, so a day can reach 24:00.
     */
    fun dayRange(spans: List<DaySpan>): DayRange {
        var from = 7 * 60
        var to = 19 * 60
        spans.forEach { span ->
            from = minOf(from, span.start / 60 * 60)
            to = maxOf(to, (span.end + 59) / 60 * 60)
        }
        return DayRange(from, to)
    }

    fun dayLineHours(range: DayRange): List<Int> {
        val step = if (range.to - range.from > 14 * 60) 3 else 2
        val first = ((range.from / 60 + 1) + step - 1) / step * step
        return generateSequence(first) { it + step }.takeWhile { it * 60 < range.to }.toList()
    }

    fun ownOfDay(own: List<OrderListItem>, day: LocalDate, zone: ZoneId): List<OrderListItem> =
        own.filter { day(it, zone) == day }.sortedBy { start(it) }

    /** Each open job of [day] as a slot; one overlapping her job carries the overlap, the server's clash rule. */
    fun openSlots(open: List<OrderListItem>, own: List<OrderListItem>, day: LocalDate, zone: ZoneId): List<OpenSlot> {
        val mine = ownOfDay(own, day, zone).mapNotNull { daySpan(it, day, zone) }
        val spans = open.mapNotNull { daySpan(it, day, zone) }
        val lanes = mutableListOf<Pair<DaySpan, Int>>()
        spans.forEach { span ->
            val lower = lanes.any { (other, lane) -> lane == 0 && other.start < span.end && other.end > span.start }
            lanes += span to if (lower) 1 else 0
        }
        val split = lanes.any { it.second == 1 }
        return lanes.map { (span, lane) ->
            val hit = mine.firstOrNull { it.start < span.end && it.end > span.start }
            OpenSlot(
                span = span,
                clash = hit?.let { DaySpan(maxOf(it.start, span.start), minOf(it.end, span.end)) },
                lane = when {
                    !split -> SlotLane.Full
                    lane == 0 -> SlotLane.Top
                    else -> SlotLane.Bottom
                },
            )
        }
    }

    fun dayName(day: LocalDate, today: LocalDate, locale: Locale): BoardText = when (day) {
        today -> BoardText.Res(R.string.day_today)
        today.plusDays(1) -> BoardText.Res(R.string.day_tomorrow)
        else -> BoardText.Literal(DateTimeFormatter.ofPattern("EEEE", locale).format(day).replaceFirstChar { it.titlecase(locale) })
    }

    /** "your job 15:00–18:00" or "your jobs 08:00–10:00, 14:00–16:00", by the count of her jobs that day. */
    fun dayMineLabel(mine: List<OrderListItem>, short: Boolean, zone: ZoneId, locale: Locale): BoardText? {
        if (mine.isEmpty()) return null
        val windows = mine.sortedBy { start(it) }.mapNotNull { window(start(it), minutes(it), zone, locale) }.joinToString(", ")
        return BoardText.Plural(if (short) R.plurals.orders_day_yours_short else R.plurals.orders_day_your_job, mine.size, listOf(windows))
    }

    /**
     * Open jobs paired with the own job each overlaps: one day names the day and her job ("Tomorrow: 1 job
     * overlaps your 15:00–18:00 job"); several days fall back to the count.
     */
    fun overlapFinding(clashing: List<Pair<OrderListItem, OrderListItem>>, today: LocalDate, zone: ZoneId, locale: Locale): BoardText? {
        if (clashing.isEmpty()) return null
        val days = clashing.mapNotNull { day(it.first, zone) }.distinct()
        if (days.size != 1) return BoardText.Plural(R.plurals.available_overlap_count, clashing.size)
        val own = clashing.first().second
        return BoardText.Plural(
            R.plurals.orders_overlap_day,
            clashing.size,
            listOf(dayName(days.single(), today, locale), clashing.size, window(start(own), minutes(own), zone, locale).orEmpty()),
        )
    }

    /** Active's *Soonest open job*: the earliest open row not ended that does not overlap her own, so it can be taken. */
    fun soonestOpen(open: List<OrderListItem>, own: List<OrderListItem>, now: Instant): OrderListItem? =
        open.filter { !isExpired(it, now) && clashes(it, own) == null }.minWithOrNull(compareBy(nullsLast()) { start(it) })

    /**
     * The Available badge, and *See N open jobs*: the server's total once Available has answered, less the loaded rows
     * whose booked slot has ended (the board no longer shows them); none at 0.
     */
    fun availableBadge(loaded: Boolean, total: Int?, rows: List<OrderListItem>, now: Instant): Int? =
        total?.minus(rows.count { isExpired(it, now) })?.takeIf { loaded && it > 0 }

    /** Today and the six days after it: the loaded open jobs of each, and whether she has her own job there. */
    fun weekCells(today: LocalDate, open: List<OrderListItem>, own: List<OrderListItem>, zone: ZoneId, loaded: Boolean): List<WeekCell> =
        (0L..6L).map { offset ->
            val day = today.plusDays(offset)
            WeekCell(day = day, open = open.count { day(it, zone) == day }.takeIf { loaded }, mine = own.any { day(it, zone) == day })
        }

    fun weekCellLabel(cell: WeekCell, longDate: String): BoardText = BoardText.Joined(
        listOfNotNull(
            BoardText.Literal(longDate),
            BoardText.Res(R.string.active_week_legend_your_job).takeIf { cell.mine },
            cell.open?.let { BoardText.Plural(R.plurals.active_week_open_count, it) },
        ),
        separator = ", ",
    )

    /** Cleaning closes at the first after photo on a solo job and at her own mark on a crew job, never a crew-mate's. */
    fun runStep(status: Int?, crew: Boolean, myMarkAt: Instant?, firstAfterPhotoAt: Instant?, afterPhoto: Boolean = firstAfterPhotoAt != null): RunStep = when (status) {
        2 -> RunStep.SetOff
        3 -> RunStep.GetIn
        4 -> if (if (crew) myMarkAt != null else afterPhoto) RunStep.Finish else RunStep.Clean
        5 -> RunStep.Done
        else -> RunStep.Cancelled
    }

    /**
     * The one status line of the Active hero and the job detail: the run's current step and its time, as the
     * items of a separator flow ("Setting off · in 3 h 40 min", "Cleaning · 48 min left").
     */
    fun runStatus(
        status: Int?,
        start: Instant,
        minutes: Int,
        now: Instant,
        zone: ZoneId,
        locale: Locale,
        crew: Boolean = false,
        myMarkAt: Instant? = null,
        firstAfterPhotoAt: Instant? = null,
        lockoutAt: Instant? = null,
        finishedAt: Instant? = null,
        cancelledAt: Instant? = null,
        cancelReason: String? = null,
        dayPattern: String = "EEE d MMM",
        afterPhoto: Boolean = firstAfterPhotoAt != null,
    ): List<BoardText> {
        val end = start.plusSeconds(minutes * 60L)
        val clock = { at: Instant -> timeText(at, zone, locale) }
        val inTime = { until: Instant -> BoardText.Res(R.string.active_hero_in, listOf(durationText(minutesUntil(now, until)))) }
        val left = {
            if (now < end) BoardText.Res(R.string.detail_left, listOf(durationText(minutesUntil(now, end))))
            else BoardText.Res(R.string.active_hero_until, listOf(clock(end)))
        }
        return when (runStep(status, crew, myMarkAt, firstAfterPhotoAt, afterPhoto)) {
            RunStep.SetOff -> {
                val days = ChronoUnit.DAYS.between(now.atZone(zone).toLocalDate(), start.atZone(zone).toLocalDate()).toInt()
                val setOff = start.minusSeconds(START_GRACE_WINDOW_MINUTES * 60)
                listOf(
                    BoardText.Res(R.string.run_step_set_off),
                    when {
                        days == 1 -> BoardText.Res(R.string.day_tomorrow)
                        days > 1 -> BoardText.Plural(R.plurals.detail_in_days, days)
                        now < setOff -> inTime(setOff)
                        now < start -> BoardText.Res(R.string.status_set_off_now)
                        else -> BoardText.Res(R.string.active_hero_due, listOf(clock(start)))
                    },
                )
            }
            RunStep.GetIn -> when {
                lockoutAt != null -> listOf(BoardText.Res(R.string.detail_waiting_company), BoardText.Res(R.string.detail_reported_at, listOf(clock(lockoutAt))))
                now < start -> listOf(BoardText.Res(R.string.status_on_the_way), inTime(start))
                else -> listOf(BoardText.Res(R.string.run_step_get_in), BoardText.Res(R.string.active_hero_due, listOf(clock(start))))
            }
            RunStep.Clean -> listOf(BoardText.Res(R.string.run_step_clean), left())
            RunStep.Finish -> listOf(BoardText.Res(R.string.run_step_finish), left())
            RunStep.Done -> listOfNotNull(
                BoardText.Res(R.string.tracker_headline_done),
                finishedAt?.let {
                    val stamp = if (Duration.between(it, now) >= Duration.ofHours(24)) {
                        DateTimeFormatter.ofPattern(dayPattern, locale).format(it.atZone(zone)) + ", " + clock(it)
                    } else {
                        clock(it)
                    }
                    BoardText.Res(R.string.detail_finished_at, listOf(stamp))
                },
            )
            RunStep.Cancelled -> listOfNotNull(
                cancelledAt?.let { BoardText.Res(R.string.detail_cancelled_at, listOf(clock(it))) },
                CANCEL_REASONS[cancelReason]?.let { BoardText.Res(it) },
            )
        }
    }

    private val CANCEL_REASONS = mapOf(
        "order.cancelled.customer_lockout" to R.string.order_cancelled_customer_lockout,
        "order.cancelled.recurring_not_confirmed" to R.string.order_cancelled_recurring_not_confirmed,
        "order.cancelled.payment_not_completed" to R.string.order_cancelled_payment_not_completed,
        "order.cancelled.no_cleaner_available" to R.string.order_cancelled_no_cleaner_available,
        "order.cancelled.company_wind_down" to R.string.order_cancelled_company_wind_down,
    )

    private const val CUSTOMER_LOCKOUT = "order.cancelled.customer_lockout"

    /** What a cancelled job pays: a system reason decides it; any other cancel keeps the general fee rule. */
    @StringRes
    fun cancelPayLine(reason: String?): Int = when {
        reason == CUSTOMER_LOCKOUT -> R.string.cancel_lockout_paid
        reason != null && reason in CANCEL_REASONS -> R.string.cancel_no_fee
        else -> R.string.work_contract_summary_late_cancel
    }

    /** How far the booked window has run at [now]: 0 at the start, 1 at the booked end. */
    fun windowRatio(start: Instant, minutes: Int, now: Instant): Float =
        if (minutes <= 0) 0f else (Duration.between(start, now).seconds / (minutes * 60f)).coerceIn(0f, 1f)

    /** "Tomorrow · 15:00–18:00 · Seifertova 21": the job the removal page names, from the store's row. */
    fun removalJobLine(row: OrderListItem, now: Instant, zone: ZoneId, locale: Locale, dayPattern: String = "EEEE d MMM"): List<BoardText> {
        val start = start(row) ?: return emptyList()
        val day = relativeDayRes(start, now, zone)?.let { BoardText.Res(it) }
            ?: BoardText.Literal(DateTimeFormatter.ofPattern(dayPattern, locale).format(start.atZone(zone)).replaceFirstChar { it.titlecase(locale) })
        return listOfNotNull(
            day,
            window(start, minutes(row), zone, locale)?.let { BoardText.Literal(it) },
            streetLine(row.customerAddress)?.let { BoardText.Literal(it) },
        )
    }

    /** Her own rows that have not ended, soonest first, never the job the page is about. */
    fun nextOwn(own: List<OrderListItem>, exceptId: String?, now: Instant): List<OrderListItem> =
        own.filter { it.id != exceptId && !isExpired(it, now) }.sortedWith(compareBy(nullsLast()) { start(it) })

    /** What a cancellation frees on its day line: from its start, or now once that has passed, to its end; none once ended. */
    fun freedSlot(row: OrderListItem, now: Instant, zone: ZoneId): DaySpan? {
        val start = start(row) ?: return null
        val end = bookedEnd(row) ?: return null
        if (maxOf(start, now) >= end) return null
        val day = start.atZone(zone).toLocalDate()
        val span = daySpan(row, day, zone) ?: return null
        if (now <= start) return span
        val local = now.atZone(zone)
        return DaySpan(ChronoUnit.DAYS.between(day, local.toLocalDate()).toInt() * 24 * 60 + local.hour * 60 + local.minute, span.end)
    }

    /** The board's open rows of [day] that have not ended, less [exceptId]: *Also open that day*, *Open today*. */
    fun openOfDay(available: List<OrderListItem>, day: LocalDate, exceptId: String?, now: Instant, zone: ZoneId): List<OrderListItem> =
        available.filter { it.id != exceptId && day(it, zone) == day && !isExpired(it, now) }

    private fun minutesUntil(now: Instant, until: Instant): Int = ((Duration.between(now, until).seconds + 59) / 60).toInt()

    /** Three words or more: the last two are tied with a no-break space, so no line ends on one word. */
    fun tie(text: String): String {
        val last = text.lastIndexOf(' ')
        if (last <= 0 || text.indexOf(' ') >= last || text[last - 1] == '·') return text
        return text.substring(0, last) + ' ' + text.substring(last + 1)
    }

    /**
     * The separator flow's lines, as item indices: an item joins the line while it fits after a separator,
     * so a separator only ever stands between two items of one line. [tieLast] makes the last separator a
     * no-break one: the item before it joins a line only with the last item beside it, unless the two fit no line.
     */
    fun flowLines(widths: List<Int>, separator: Int, maxWidth: Int, tieLast: Boolean = false): List<List<Int>> {
        val lines = mutableListOf<MutableList<Int>>()
        var used = 0
        widths.forEachIndexed { index, width ->
            val line = lines.lastOrNull()
            val tied = if (tieLast && index == widths.size - 2) width + separator + widths.last() else width
            val needs = if (tied <= maxWidth) tied else width
            if (line != null && used + separator + needs <= maxWidth) {
                line += index
                used += separator + width
            } else {
                lines += mutableListOf(index)
                used = width
            }
        }
        return lines
    }

    /** An Available row's scope always runs full width; the reserved card's goes there only when that makes it shorter. */
    fun scopeFullWidth(besideHeight: Int, wideHeight: Int, measured: Boolean): Boolean = !measured || wideHeight < besideHeight

    /** Paid: the cash was collected for the company; Pending: the customer did not pay, and her pay stands. */
    fun historyCash(row: OrderListItem, locale: Locale): List<BoardText> {
        if (!isCash(row)) return emptyList()
        val due = row.amountDueOnCard?.let { money(it, symbolOf(row), locale) } ?: return emptyList()
        return when (row.paymentStatus?.value) {
            PAYMENT_PAID -> listOf(BoardText.Res(R.string.history_cash_collected, listOf(due)))
            PAYMENT_PENDING -> listOf(BoardText.Res(R.string.history_cash_not_paid, listOf(due)), BoardText.Res(R.string.history_paid_in_full))
            else -> emptyList()
        }
    }

    private const val PAYMENT_PENDING = 1
    private const val PAYMENT_PAID = 2

    /** `GetCashHeld` returns non-zero balances only: none is no panel; a hidden one leads. */
    fun cashPanelRow(cashHeld: List<CashHeld>): CashHeld? = cashHeld.firstOrNull { it.cashJobsHidden } ?: cashHeld.firstOrNull()

    /** The meter's fill; no cap (the company default) draws no meter. */
    fun cashRatio(held: Double, cap: Double?): Float? {
        if (cap == null || cap <= 0.0) return null
        return (held / cap).coerceIn(0.0, 1.0).toFloat()
    }

    /** A period that holds today stops at the end of today's week; the days after it have no data yet. */
    fun historyGridEnd(periodEnd: LocalDate, today: LocalDate): LocalDate =
        minOf(periodEnd, today.with(TemporalAdjusters.nextOrSame(DayOfWeek.SUNDAY)))

    /** Whole Monday-first weeks covering [from]..[to], whatever the device's first weekday. */
    fun historyGrid(from: LocalDate, to: LocalDate): List<LocalDate> {
        val first = from.with(TemporalAdjusters.previousOrSame(DayOfWeek.MONDAY))
        val last = to.with(TemporalAdjusters.nextOrSame(DayOfWeek.SUNDAY))
        return generateSequence(first) { it.plusDays(1) }.takeWhile { it <= last }.toList()
    }

    /**
     * The half of the day line each mark takes once drawn: two marks whose drawn boxes touch take the upper and the lower
     * half, her own job on top, else the one further left, so two short jobs never read as one. An open job that overlaps
     * hers in time is the clash and keeps the full track. A mark the board already laned keeps its half, and a mark drawn
     * against it takes the other.
     */
    fun dayLineLanes(marks: List<DayMark>, range: DayRange, trackPx: Float, minPx: Float, insetPx: Float): List<SlotLane> {
        val span = (range.to - range.from).coerceAtLeast(1).toFloat()
        val x = { minutes: Int -> ((minutes.coerceIn(range.from, range.to) - range.from) / span) * trackPx }
        val boxes = marks.map { markBounds(x(it.span.start) + insetPx, x(it.span.end) - insetPx, trackPx, minPx).let { (left, width) -> left to left + width } }
        val given = marks.map { it.lane }
        val lanes = given.toMutableList()
        for (i in marks.indices) for (k in i + 1 until marks.size) {
            val (a, c) = marks[i] to marks[k]
            if (lanes[i] != SlotLane.Full && lanes[k] != SlotLane.Full) continue
            val drawn = boxes[i].first < boxes[k].second + 1f && boxes[k].first < boxes[i].second + 1f
            val timed = a.span.start < c.span.end && c.span.start < a.span.end
            if (!drawn || (timed && a.mine != c.mine)) continue
            val iOnTop = when {
                given[i] != SlotLane.Full -> given[i] == SlotLane.Top
                given[k] != SlotLane.Full -> given[k] == SlotLane.Bottom
                a.mine != c.mine -> a.mine
                else -> boxes[i].first <= boxes[k].first
            }
            if (given[i] == SlotLane.Full) lanes[i] = if (iOnTop) SlotLane.Top else SlotLane.Bottom
            if (given[k] == SlotLane.Full) lanes[k] = if (iOnTop) SlotLane.Bottom else SlotLane.Top
        }
        return lanes
    }

    /** The period's earnings as the formatter writes them, each figure held whole; nothing earned is zero in her currency. */
    fun historyTotalText(rows: List<OrderListItem>, total: Int?, fallbackSymbol: String?, locale: Locale): String {
        if (rows.isEmpty()) return money(0.0, fallbackSymbol, locale)
        return historyTotals(rows, total).earnings.split(" · ").joinToString(" · ") { it.replace(' ', '\u00A0') }
    }

    /**
     * The left edges of the marks over the day line, each given as its moment's x and its width: a mark over its moment,
     * one near an end starting or ending there, all kept on the track. The now mark ([nowIndex]) is left out (null) when it
     * would come within [gap] of another, since its tick already says now; every other mark keeps its place.
     */
    fun dayLineLabelLefts(labels: List<Pair<Float, Float>>, nowIndex: Int?, track: Float, gap: Float): List<Float?> {
        val lefts = labels.map { (at, width) ->
            val left = when {
                at < track * 0.08f -> at
                at > track * 0.92f -> at - width
                else -> at - width / 2f
            }
            left.coerceIn(0f, (track - width).coerceAtLeast(0f))
        }
        val nowTouches = nowIndex != null && labels.indices.any { i ->
            i != nowIndex && lefts[i] < lefts[nowIndex] + labels[nowIndex].second + gap && lefts[nowIndex] < lefts[i] + labels[i].second + gap
        }
        return lefts.mapIndexed { i, left -> left.takeUnless { nowTouches && i == nowIndex } }
    }

    /** The offer teaser puts See offer in a full-width bar under the job when a run cannot sit whole beside the tile, or at large text. */
    fun offerStacks(runWidths: List<Int>, column: Int, fontScale: Float): Boolean =
        fontScale >= 1.3f || runWidths.any { it > column }

    /** A count held to the word after it ("3 rooms" never breaks after the 3). */
    fun tieCount(text: String): String = text.replaceFirst(LEADING_COUNT, "$1\u00A0")

    private val LEADING_COUNT = Regex("""^(\d+) """)

    /** A day-line mark's left edge and width: never under [min], a shorter one centred on its span and kept on the track. */
    fun markBounds(left: Float, right: Float, track: Float, min: Float): Pair<Float, Float> {
        if (right - left >= min) return left to (right - left)
        return ((left + right) / 2f - min / 2f).coerceIn(0f, (track - min).coerceAtLeast(0f)) to min
    }
}
