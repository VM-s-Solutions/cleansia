package cz.cleansia.partner.features.orders

import cz.cleansia.partner.R
import cz.cleansia.partner.api.model.Code
import cz.cleansia.partner.api.model.CurrencyListItem
import cz.cleansia.partner.api.model.HomeDistanceBand
import cz.cleansia.partner.api.model.OrderListItem
import cz.cleansia.partner.api.model.PackageListItem
import cz.cleansia.partner.api.model.ServiceListItem
import cz.cleansia.partner.api.model.Translation
import cz.cleansia.partner.data.orders.PendingOffer
import cz.cleansia.partner.data.payroll.CashHeld
import java.time.Instant
import java.time.LocalDate
import java.time.LocalTime
import java.time.ZoneId
import java.util.Locale
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * The board's pure rules (spec §3): the server's clash rule, the hero pick, the cancel and summary gates, the
 * fixed extras order and the rest. Text rules return resource ids with their arguments; the composable layer
 * resolves them.
 */
class OrdersBoardRulesTest {

    private val rules = OrdersBoardRules
    private val utc: ZoneId = ZoneId.of("UTC")
    private val en = Locale.US
    private val czk = CurrencyListItem(id = "cur-czk", code = "CZK", symbol = "Kč")

    private fun row(
        id: String = "o1",
        start: String? = "2026-11-02T09:00:00Z",
        minutes: Int? = 240,
        status: Int? = 0,
        required: Int? = 1,
        assigned: Int? = 0,
        pay: Double? = 780.0,
        cash: Boolean = false,
        due: Double? = null,
        address: String? = null,
        approximate: String? = null,
        extras: Map<String, Boolean>? = null,
        rooms: Int? = null,
        baths: Int? = null,
        services: List<ServiceListItem>? = null,
        packages: List<PackageListItem>? = null,
    ) = OrderListItem(
        id = id,
        cleaningDateTime = start,
        estimatedTime = minutes,
        orderStatus = status?.let { Code(value = it) },
        requiredEmployees = required,
        assignedEmployeesCount = assigned,
        estimatedCleanerPay = pay,
        paymentType = Code(value = if (cash) 1 else 0),
        amountDueOnCard = due,
        customerAddress = address,
        customerAddressApproximate = approximate,
        extras = extras,
        rooms = rooms,
        bathrooms = baths,
        selectedServices = services,
        selectedPackages = packages,
        currency = czk,
    )

    private fun at(iso: String): Instant = Instant.parse(iso)

    // ── window / booked end / expiry ─────────────────────────────────────

    @Test
    fun `the window is the start and the booked end, or the start alone without minutes`() {
        assertEquals("09:00–13:00", rules.window(at("2026-11-02T09:00:00Z"), 240, utc, en))
        assertEquals("09:00", rules.window(at("2026-11-02T09:00:00Z"), 0, utc, en))
        assertNull(rules.window(null, 240, utc, en))
        assertEquals(at("2026-11-02T13:00:00Z"), rules.bookedEnd(row()))
    }

    @Test
    fun `a row whose booked end has passed is expired, one still running is not`() {
        assertTrue(rules.isExpired(row(), at("2026-11-02T13:00:00Z")))
        assertFalse(rules.isExpired(row(), at("2026-11-02T12:59:00Z")))
        assertFalse(rules.isExpired(row(start = null), at("2030-01-01T00:00:00Z")))
    }

    // ── live flag ─────────────────────────────────────────────────────────

    @Test
    fun `on the way and in progress are under way until the booked end`() {
        val now = at("2026-11-02T10:00:00Z")
        listOf(3, 4).forEach { status ->
            assertEquals(LiveFlag.UnderWay(at("2026-11-02T13:00:00Z")), rules.liveFlag(row(status = status), now))
        }
    }

    @Test
    fun `a new or confirmed row past its start can be taken until the booked end, a future one carries no flag`() {
        listOf(0, 2).forEach { status ->
            assertEquals(
                LiveFlag.TakeUntil(at("2026-11-02T13:00:00Z")),
                rules.liveFlag(row(status = status), at("2026-11-02T09:00:00Z")),
            )
            assertNull(rules.liveFlag(row(status = status), at("2026-11-02T08:59:00Z")))
        }
    }

    // ── clash (the server's LiveCommitmentsInWindow rule) ────────────────

    @Test
    fun `an overlapping own job clashes`() {
        val own = row(id = "a", start = "2026-11-02T12:00:00Z", minutes = 180)
        assertEquals(own, rules.clashes(row(), listOf(own)))
    }

    @Test
    fun `touching windows do not clash in either direction`() {
        val endsAtStart = row(id = "a", start = "2026-11-02T07:00:00Z", minutes = 120)
        val startsAtEnd = row(id = "b", start = "2026-11-02T13:00:00Z", minutes = 60)
        assertNull(rules.clashes(row(), listOf(endsAtStart, startsAtEnd)))
    }

    @Test
    fun `a contained window clashes, and so does a candidate spanning midnight`() {
        val inside = row(id = "a", start = "2026-11-02T10:00:00Z", minutes = 30)
        assertEquals(inside, rules.clashes(row(), listOf(inside)))
        val night = row(id = "c", start = "2026-11-02T22:00:00Z", minutes = 240)
        val earlyMorning = row(id = "d", start = "2026-11-03T01:00:00Z", minutes = 60)
        assertEquals(earlyMorning, rules.clashes(night, listOf(earlyMorning)))
    }

    @Test
    fun `a missing start never clashes and missing minutes count as zero`() {
        assertNull(rules.clashes(row(start = null), listOf(row(id = "a"))))
        assertNull(rules.clashes(row(), listOf(row(id = "a", start = null))))
        val zero = row(id = "z", start = "2026-11-02T10:00:00Z", minutes = null)
        assertEquals("a zero-length own job inside the window still overlaps", zero, rules.clashes(row(), listOf(zero)))
        assertNull(rules.clashes(row(minutes = 0), listOf(row(id = "a", start = "2026-11-02T09:00:00Z", minutes = 60))))
    }

    @Test
    fun `the earliest of several clashing jobs is the one named`() {
        val late = row(id = "late", start = "2026-11-02T12:00:00Z", minutes = 60)
        val early = row(id = "early", start = "2026-11-02T08:00:00Z", minutes = 120)
        assertEquals(early, rules.clashes(row(), listOf(late, early)))
    }

    // ── hero pick and control ─────────────────────────────────────────────

    @Test
    fun `in progress beats an earlier confirmed job, on the way beats confirmed, ties go to the earlier start`() {
        val confirmedEarly = row(id = "c", start = "2026-11-02T07:00:00Z", status = 2)
        val inProgress = row(id = "p", start = "2026-11-02T09:00:00Z", status = 4)
        val onTheWay = row(id = "w", start = "2026-11-02T10:00:00Z", status = 3)
        assertEquals(inProgress, rules.heroPick(listOf(confirmedEarly, onTheWay, inProgress)))
        assertEquals(onTheWay, rules.heroPick(listOf(confirmedEarly, onTheWay)))
        val confirmedLater = row(id = "c2", start = "2026-11-03T07:00:00Z", status = 2)
        assertEquals(confirmedEarly, rules.heroPick(listOf(confirmedLater, confirmedEarly)))
        assertNull(rules.heroPick(listOf(row(status = 5), row(status = 0))))
    }

    @Test
    fun `an Active pane holding only an in-progress job still has a hero`() {
        val only = row(status = 4)
        assertEquals(only, rules.heroPick(listOf(only)))
    }

    @Test
    fun `set off opens sixty minutes before the start`() {
        val confirmed = row(status = 2)
        assertEquals(at("2026-11-02T08:00:00Z"), rules.setOffOpensAt(confirmed))
        assertEquals(
            HeroControl.SetOff(at("2026-11-02T08:00:00Z"), enabled = false),
            rules.heroControl(confirmed, at("2026-11-02T07:59:00Z")),
        )
        assertEquals(
            HeroControl.SetOff(at("2026-11-02T08:00:00Z"), enabled = true),
            rules.heroControl(confirmed, at("2026-11-02T08:00:00Z")),
        )
        assertEquals(HeroControl.Start, rules.heroControl(row(status = 3), at("2026-11-02T08:00:00Z")))
        assertEquals(HeroControl.OpenJob, rules.heroControl(row(status = 4), at("2026-11-02T10:00:00Z")))
    }

    @Test
    fun `no hero control completes a job`() {
        // In progress opens the job (its cash, photo and crew steps live there); nothing else gets a control.
        assertEquals(HeroControl.OpenJob, rules.heroControl(row(status = 4), at("2026-11-02T12:59:00Z")))
        listOf(0, 1, 5, 6).forEach { status ->
            assertNull("status $status", rules.heroControl(row(status = status), at("2026-11-02T10:00:00Z")))
        }
    }

    @Test
    fun `the hero's right text is booked-until, in, was-due, or nothing a day out`() {
        val now = at("2026-11-02T04:20:00Z")
        assertEquals(
            BoardText.Res(R.string.active_hero_until, listOf("13:00")),
            rules.heroRightText(row(status = 4), now, utc, en),
        )
        assertEquals(
            BoardText.Res(R.string.active_hero_in, listOf(BoardText.Res(R.string.orders_duration_h_min, listOf(4, 40)))),
            rules.heroRightText(row(status = 2), now, utc, en),
        )
        assertEquals(
            BoardText.Res(R.string.active_hero_due, listOf("09:00")),
            rules.heroRightText(row(status = 3), at("2026-11-02T09:30:00Z"), utc, en),
        )
        assertNull(rules.heroRightText(row(status = 2), at("2026-11-01T09:00:00Z"), utc, en))
    }

    // ── cancel line, cancel text, summary gate ────────────────────────────

    private val versions = listOf("2026-09-20", "2026-09-29", "2026-10-05", "2026-10-27", "2026-12-01", null)

    @Test
    fun `free to cancel needs a future start and a release version`() {
        val start = at("2026-11-02T09:00:00Z")
        val before = at("2026-11-01T09:00:00Z")
        versions.forEach { version ->
            val expected = if (version in setOf("2026-09-29", "2026-10-05", "2026-10-27")) CancelLine.FreeUntil(start) else null
            listOf(0, 2).forEach { status ->
                assertEquals("status $status, $version", expected, rules.cancelLine(status, start, before, version))
            }
        }
    }

    @Test
    fun `under way and start passed show on every version`() {
        val start = at("2026-11-02T09:00:00Z")
        versions.forEach { version ->
            listOf(3, 4).forEach { status ->
                assertEquals(CancelLine.UnderWay, rules.cancelLine(status, start, at("2026-11-02T10:00:00Z"), version))
            }
            listOf(0, 2).forEach { status ->
                assertEquals(CancelLine.StartPassed, rules.cancelLine(status, start, start, version))
            }
        }
    }

    @Test
    fun `before the take each line has its own sentence`() {
        val free = CancelLine.FreeUntil(at("2026-11-02T09:00:00Z"))
        assertEquals(R.string.take_cancel_free_until, rules.cancelText(free, TakePhase.BeforeTake))
        assertEquals(R.string.take_cancel_under_way, rules.cancelText(CancelLine.UnderWay, TakePhase.BeforeTake))
        assertEquals(R.string.take_cancel_start_passed, rules.cancelText(CancelLine.StartPassed, TakePhase.BeforeTake))
        assertNull(rules.cancelText(null, TakePhase.BeforeTake))
    }

    @Test
    fun `once taken the two 'once you take it' sentences never show`() {
        val free = CancelLine.FreeUntil(at("2026-11-02T09:00:00Z"))
        assertEquals(R.string.take_cancel_free_until, rules.cancelText(free, TakePhase.Taken))
        assertEquals(R.string.take_done_cannot_cancel, rules.cancelText(CancelLine.UnderWay, TakePhase.Taken))
        assertEquals(R.string.take_done_cannot_cancel, rules.cancelText(CancelLine.StartPassed, TakePhase.Taken))
        assertNull(rules.cancelText(null, TakePhase.Taken))
        val taken = listOf(free, CancelLine.UnderWay, CancelLine.StartPassed).map { rules.cancelText(it, TakePhase.Taken) }
        assertFalse(R.string.take_cancel_under_way in taken)
        assertFalse(R.string.take_cancel_start_passed in taken)
    }

    @Test
    fun `the four lines show on take and accept for 10-05 and 10-27 only, never on a read`() {
        val take = WorkContractRequest.Take("o1")
        val accept = WorkContractRequest.Accept("o1")
        listOf("2026-10-05", "2026-10-27").forEach { version ->
            assertTrue(rules.summaryVisible(take, version))
            assertTrue(rules.summaryVisible(accept, version))
            assertFalse(rules.summaryVisible(WorkContractRequest.Read("a1"), version))
        }
        listOf("2026-09-29", "2026-09-20", "2026-12-01", null).forEach { version ->
            assertFalse("$version", rules.summaryVisible(take, version))
        }
    }

    // ── street, area, extras, scope ───────────────────────────────────────

    @Test
    fun `the street is everything before the city and the postcode`() {
        assertEquals("A 1", rules.streetLine("A 1, Praha, 14000"))
        assertEquals("Dvůr, vchod B", rules.streetLine("Dvůr, vchod B, Praha, 14000"))
        assertEquals("Praha, 14000", rules.streetLine("Praha, 14000"))
        assertNull(rules.streetLine("  "))
        assertNull(rules.streetLine(null))
    }

    @Test
    fun `the area is the approximate address when there is one`() {
        assertEquals("Praha 4", rules.areaLine(row(approximate = "Praha 4")))
        assertNull(rules.areaLine(row(approximate = " ")))
    }

    @Test
    fun `extras come in the fixed order whatever the wire order, false values dropped`() {
        val wire = linkedMapOf(
            "zz-unknown" to true,
            "laundry-ironing" to true,
            "pet-hair-supplement" to true,
            "aa-unknown" to true,
            "interior-windows" to true,
            "inside-fridge" to false,
            "inside-oven" to true,
        )
        val slugs = rules.extraSlugsOf(row(extras = wire))
        assertEquals(
            listOf("inside-oven", "interior-windows", "laundry-ironing", "pet-hair-supplement", "aa-unknown", "zz-unknown"),
            slugs,
        )
        assertEquals(
            listOf("inside-oven", "inside-fridge", "interior-windows", "laundry-ironing", "pet-hair-supplement"),
            rules.orderedExtras(listOf("pet-hair-supplement", "laundry-ironing", "interior-windows", "inside-fridge", "inside-oven")),
        )
    }

    @Test
    fun `known extras take their short names, the others the long name or a readable fallback`() {
        assertEquals(BoardText.Res(R.string.extra_short_inside_oven), rules.extraLabel("inside-oven"))
        assertEquals(BoardText.Res(R.string.extra_short_inside_fridge), rules.extraLabel("inside-fridge"))
        assertEquals(BoardText.Res(R.string.extra_short_interior_windows), rules.extraLabel("interior-windows"))
        assertEquals(BoardText.Res(R.string.extra_short_laundry_ironing), rules.extraLabel("laundry-ironing"))
        assertEquals(BoardText.Res(R.string.extra_name_pet_hair_supplement), rules.extraLabel("pet-hair-supplement"))
        assertEquals(BoardText.Literal("Balcony wash"), rules.extraLabel("balcony-wash"))
        assertEquals(
            BoardText.Joined(
                listOf(BoardText.Res(R.string.extra_short_inside_oven), BoardText.Res(R.string.extra_short_inside_fridge)),
                separator = ", ",
                prefix = "+ ",
            ),
            rules.extrasLine(listOf("inside-fridge", "inside-oven")),
        )
        assertNull(rules.extrasLine(emptyList()))
    }

    @Test
    fun `the scope line leads with the translated package, then rooms, baths and extras`() {
        val pkg = PackageListItem(id = "p", name = "Deep clean", translations = mapOf("cs" to Translation(name = "Hloubkový úklid")))
        val svc = ServiceListItem(id = "s", name = "Standard clean")
        val r = row(packages = listOf(pkg), services = listOf(svc), rooms = 2, baths = 1, extras = mapOf("inside-oven" to true))
        assertEquals("Hloubkový úklid", rules.scopeLead(r, "cs"))
        assertEquals("Deep clean", rules.scopeLead(r, "en"))
        assertEquals("Standard clean", rules.scopeLead(row(services = listOf(svc)), "cs"))
        assertEquals(
            BoardText.Joined(
                listOf(
                    BoardText.Literal("Hloubkový úklid"),
                    BoardText.Plural(R.plurals.scope_rooms, 2),
                    BoardText.Plural(R.plurals.scope_baths, 1),
                    BoardText.Joined(listOf(BoardText.Res(R.string.extra_short_inside_oven)), ", ", "+ "),
                ),
                // A no-break space before the dot: a wrapped line never starts with "·".
                separator = " · ",
            ),
            rules.scopeLine(r, "cs"),
        )
        assertNull(rules.scopeLine(row(), "cs"))
    }

    // ── distance band ─────────────────────────────────────────────────────

    @Test
    fun `each band names its distance from home and no band says nothing`() {
        assertEquals(BoardText.Res(R.string.distance_band_under_3), rules.bandText(HomeDistanceBand._1))
        assertEquals(BoardText.Res(R.string.distance_band_3_6), rules.bandText(HomeDistanceBand._2))
        assertEquals(BoardText.Res(R.string.distance_band_6_10), rules.bandText(HomeDistanceBand._3))
        assertEquals(BoardText.Res(R.string.distance_band_10_20), rules.bandText(HomeDistanceBand._4))
        assertEquals(BoardText.Res(R.string.distance_band_over_20), rules.bandText(HomeDistanceBand._5))
        assertNull(rules.bandText(null))
        assertEquals(HomeDistanceBand.entries.size, HomeDistanceBand.entries.mapNotNull { rules.bandText(it) }.toSet().size)
    }

    // ── durations, team ───────────────────────────────────────────────────

    @Test
    fun `durations read 45 min, 2 h and 2 h 30 min`() {
        assertEquals(BoardText.Res(R.string.orders_duration_min, listOf(45)), rules.durationText(45))
        assertEquals(BoardText.Res(R.string.orders_duration_h, listOf(2)), rules.durationText(120))
        assertEquals(BoardText.Res(R.string.orders_duration_h_min, listOf(2, 30)), rules.durationText(150))
    }

    @Test
    fun `the team flag counts taken places from assignedEmployeesCount`() {
        assertEquals(
            BoardText.Joined(
                listOf(BoardText.Plural(R.plurals.crew_size, 2), BoardText.Res(R.string.orders_flag_crew_nobody)),
                " · ",
            ),
            rules.teamFlag(row(required = 2, assigned = 0)),
        )
        assertEquals(
            BoardText.Joined(
                listOf(BoardText.Plural(R.plurals.crew_size, 2), BoardText.Plural(R.plurals.orders_flag_crew_taken, 1)),
                " · ",
            ),
            rules.teamFlag(row(required = 2, assigned = 1)),
        )
        assertNull(rules.teamFlag(row(required = 1)))
    }

    @Test
    fun `the hero says team of n and never cleaners needed`() {
        assertEquals(BoardText.Plural(R.plurals.active_hero_team, 2), rules.heroTeam(row(required = 2, assigned = 2)))
        assertNull(rules.heroTeam(row(required = 1)))
    }

    // ── tile month ────────────────────────────────────────────────────────

    @Test
    fun `the tile month is the standalone abbreviation as written, never the genitive`() {
        val ru = Locale.forLanguageTag("ru")
        assertEquals("май", rules.tileMonth(LocalDate.of(2026, 5, 1), ru))
        assertEquals("март", rules.tileMonth(LocalDate.of(2026, 3, 1), ru))
        // uk: the standalone abbreviation, whatever the platform's CLDR data cuts it to ("лист." per the mock on
        // Android; "жов" in the JDK 21 data these tests run on) — never the genitive "жовтня".
        val ukOctober = rules.tileMonth(LocalDate.of(2026, 10, 31), Locale.forLanguageTag("uk"))
        assertTrue(ukOctober, ukOctober.startsWith("жов") && !ukOctober.startsWith("жовтн"))
        assertEquals("Oct", rules.tileMonth(LocalDate.of(2026, 10, 31), Locale.UK))
    }

    // ── reserved join ─────────────────────────────────────────────────────

    private fun offer(id: String, respondBy: String) = PendingOffer(
        id = id, displayOrderNumber = null, cleaningDateTime = "2026-11-02T09:00:00Z", estimatedTime = 120,
        respondByUtc = respondBy, customerAddressApproximate = null, rooms = 2, bathrooms = 1,
        currencyCode = "CZK", estimatedCleanerPay = 999.0,
    )

    @Test
    fun `live offers are joined to their board rows, soonest deadline first, expired ones hidden`() {
        val now = at("2026-11-01T12:00:00Z")
        val a = row(id = "a")
        val offers = listOf(
            offer("a", "2026-11-01T18:00:00Z"),
            offer("gone", "2026-11-01T11:59:00Z"),
            offer("off-page", "2026-11-01T14:00:00Z"),
        )
        val cards = rules.reservedCards(offers, listOf(row(id = "x"), a), now)
        assertEquals(listOf("off-page", "a"), cards.map { it.first.id })
        assertNull("an off-page offer is fetched by id, not invented", cards[0].second)
        assertEquals(a, cards[1].second)
    }

    @Test
    fun `the reserved cards on show are live offers joined to a row that has not ended`() {
        val now = at("2026-11-02T12:00:00Z")
        val live = row(id = "live", start = "2026-11-03T09:00:00Z")
        val ended = row(id = "ended", start = "2026-11-02T07:00:00Z", minutes = 240)
        val offers = listOf(
            offer("live", "2026-11-02T18:00:00Z"),
            offer("ended", "2026-11-02T18:00:00Z"),
            offer("off-page", "2026-11-02T13:00:00Z"),
            offer("lapsed", "2026-11-02T11:00:00Z"),
        )
        val shown = rules.reservedShown(offers, listOf(live, ended, row(id = "lapsed", start = "2026-11-03T09:00:00Z")), now)
        assertEquals(listOf("live" to live), shown.map { it.first.id to it.second })
    }

    @Test
    fun `a joined reserved row leaves the job cards, as do ended rows, and the rest keep the chosen order`() {
        val now = at("2026-11-02T12:00:00Z")
        val ended = row(id = "ended", start = "2026-11-02T07:00:00Z", minutes = 240)
        val reserved = row(id = "reserved", start = "2026-11-03T08:00:00Z", pay = 2000.0)
        val low = row(id = "low", start = "2026-11-03T10:00:00Z", pay = 500.0)
        val high = row(id = "high", start = "2026-11-04T09:00:00Z", pay = 900.0)
        val rows = listOf(ended, reserved, low, high)

        assertEquals(listOf("low", "high"), rules.visibleCards(rows, setOf("reserved"), AvailableSort.SoonestFirst, now).map { it.id })
        assertEquals(listOf("high", "low"), rules.visibleCards(rows, setOf("reserved"), AvailableSort.EarningsHighToLow, now).map { it.id })
    }

    // ── paging, sort ──────────────────────────────────────────────────────

    @Test
    fun `a loaded page is appended and de-duplicated by id`() {
        val merged = rules.mergePage(listOf(row(id = "a"), row(id = "b")), listOf(row(id = "b", pay = 1.0), row(id = "c")))
        assertEquals(listOf("a", "b", "c"), merged.map { it.id })
        assertEquals("the row already held stays", 780.0, merged[1].estimatedCleanerPay)
    }

    @Test
    fun `best pay is offered only when every row is loaded`() {
        assertEquals(listOf(AvailableSort.SoonestFirst), rules.sortOptions(100, 137))
        assertEquals(listOf(AvailableSort.SoonestFirst), rules.sortOptions(3, null))
        assertEquals(listOf(AvailableSort.SoonestFirst, AvailableSort.EarningsHighToLow), rules.sortOptions(37, 37))
    }

    @Test
    fun `best pay sorts by pay, unknown pay last, ties by the earlier start`() {
        val rows = listOf(
            row(id = "null", pay = null, start = "2026-11-01T09:00:00Z"),
            row(id = "low", pay = 500.0),
            row(id = "highLate", pay = 900.0, start = "2026-11-03T09:00:00Z"),
            row(id = "highEarly", pay = 900.0, start = "2026-11-02T09:00:00Z"),
        )
        assertEquals(
            listOf("highEarly", "highLate", "low", "null"),
            rules.sorted(rows, AvailableSort.EarningsHighToLow).map { it.id },
        )
        assertEquals(rows, rules.sorted(rows, AvailableSort.SoonestFirst))
    }

    // ── cash, history, badge, stale time ──────────────────────────────────

    @Test
    fun `the cash notice is the first hidden row, printed to the haléř`() {
        val held = CashHeld(currencyCode = "CZK", amount = 3000.40, floatCap = 3000.0, cashJobsHidden = true)
        val notHidden = CashHeld(currencyCode = "EUR", amount = 10.0, floatCap = 100.0, cashJobsHidden = false)
        assertEquals(held, rules.cashNotice(listOf(notHidden, held)))
        assertNull(rules.cashNotice(listOf(notHidden)))
        assertNull(rules.cashNotice(emptyList()))
        val cs = Locale.forLanguageTag("cs-CZ")
        assertEquals("3 000,40 Kč" to "3 000 Kč", rules.cashAmounts(held, cs))
    }

    @Test
    fun `history totals sum per currency, count the server total and round the booked hours`() {
        val eur = CurrencyListItem(id = "cur-eur", code = "EUR", symbol = "€")
        val rows = listOf(
            row(id = "a", pay = 1000.0, minutes = 90),
            row(id = "b", pay = 250.0, minutes = 100),
            row(id = "c", pay = 40.0, minutes = 60).copy(currency = eur),
        )
        assertEquals(HistoryTotals(earnings = "1 250 Kč · 40 €", jobs = 16, hours = 4), rules.historyTotals(rows, 16))
        assertEquals(0, rules.historyTotals(emptyList(), null).jobs)
    }

    @Test
    fun `badges hide at zero or unknown and cap at 99+`() {
        assertNull(rules.badge(null))
        assertNull(rules.badge(0))
        assertEquals("6", rules.badge(6))
        assertEquals("99", rules.badge(99))
        assertEquals("99+", rules.badge(100))
    }

    @Test
    fun `the stale time is the clock today and day plus clock before`() {
        val zone = ZoneId.of("Europe/Prague")
        val now = at("2026-10-31T12:00:00Z")
        assertEquals("10:14", rules.staleWhen(at("2026-10-31T09:14:00Z"), now, zone, en, "EEE d MMM"))
        assertEquals("Fri 30 Oct, 10:14", rules.staleWhen(at("2026-10-30T09:14:00Z"), now, zone, en, "EEE d MMM"))
    }

    @Test
    fun `today and tomorrow are named, other days are not`() {
        val now = at("2026-10-31T08:00:00Z")
        assertEquals(R.string.day_today, rules.relativeDayRes(at("2026-10-31T20:00:00Z"), now, utc))
        assertEquals(R.string.day_tomorrow, rules.relativeDayRes(at("2026-11-01T08:00:00Z"), now, utc))
        assertNull(rules.relativeDayRes(at("2026-11-02T08:00:00Z"), now, utc))
    }

    @Test
    fun `the cash flag says the cash is collected for the company and pay hides when unknown`() {
        assertEquals(
            BoardText.Res(R.string.orders_flag_cash_collect, listOf("1 290 Kč")),
            rules.cashFlag(row(cash = true, due = 1290.0), Locale.forLanguageTag("cs-CZ")),
        )
        assertNull(rules.cashFlag(row(cash = false, due = 1290.0), en))
        assertNull(rules.payText(row(pay = null), en))
        assertEquals("780 Kč", rules.payText(row(pay = 780.0), en))
    }

    private val prague: ZoneId = ZoneId.of("Europe/Prague")
    private val nov12: LocalDate = LocalDate.of(2026, 11, 12)

    /** A Prague wall-clock time in November 2026 (UTC+1); 12 November is a Thursday. */
    private fun pragueAt(day: Int, hm: String): Instant =
        LocalDate.of(2026, 11, day).atTime(LocalTime.parse(hm)).atZone(prague).toInstant()

    private fun job(id: String, day: Int, from: String, minutes: Int, status: Int = 0) =
        row(id = id, start = pragueAt(day, from).toString(), minutes = minutes, status = status)

    @Test
    fun `rows group into their days, soonest first, keeping their order inside a day`() {
        val a = job("a", 12, "08:00", 240)
        val b = job("b", 13, "08:00", 120)
        val c = job("c", 13, "14:00", 120)
        val d = job("d", 14, "09:00", 210)
        val groups = rules.dayGroups(listOf(a, b, c, d), prague)
        assertEquals(listOf(nov12, nov12.plusDays(1), nov12.plusDays(2)), groups.map { it.first })
        assertEquals(listOf(listOf(a), listOf(b, c), listOf(d)), groups.map { it.second })
    }

    @Test
    fun `the day line runs 07 to 19 unless the data reaches past it`() {
        assertEquals(DayRange(7 * 60, 19 * 60), rules.dayRange(emptyList()))
        assertEquals(DayRange(7 * 60, 19 * 60), rules.dayRange(listOf(DaySpan(8 * 60, 12 * 60))))
        assertEquals(DayRange(7 * 60, 22 * 60), rules.dayRange(listOf(DaySpan(19 * 60 + 45, 21 * 60 + 45))))
        assertEquals(DayRange(6 * 60, 19 * 60), rules.dayRange(listOf(DaySpan(6 * 60 + 45, 7 * 60 + 45), DaySpan(7 * 60 + 45, 9 * 60 + 45))))
        assertEquals(DayRange(7 * 60, 24 * 60), rules.dayRange(listOf(DaySpan(19 * 60 + 45, 23 * 60 + 45))))
    }

    @Test
    fun `one range per page covers every day's blocks`() {
        val monday = listOf(DaySpan(6 * 60 + 45, 7 * 60 + 45))
        val friday = listOf(DaySpan(19 * 60 + 45, 21 * 60 + 45))
        assertEquals(DayRange(6 * 60, 22 * 60), rules.dayRange(monday + friday))
    }

    @Test
    fun `hour labels sit on the even hours, every third once the line passes 14 hours`() {
        assertEquals(listOf(8, 10, 12, 14, 16, 18), rules.dayLineHours(DayRange(7 * 60, 19 * 60)))
        assertEquals(listOf(9, 12, 15, 18, 21), rules.dayLineHours(DayRange(6 * 60, 22 * 60)))
    }

    @Test
    fun `a job's span is its minutes from the day's midnight in the device zone`() {
        assertEquals(DaySpan(14 * 60, 16 * 60), rules.daySpan(job("a1", 12, "14:00", 120), nov12, prague))
        assertNull(rules.daySpan(job("a2", 13, "14:00", 120), nov12, prague))
    }

    @Test
    fun `an open job that overlaps her job carries the overlap as its clash span`() {
        val a2 = job("A2", 13, "15:00", 180, status = 2)
        val v3 = job("V3", 13, "08:00", 120)
        val v4 = job("V4", 13, "14:00", 120)
        val slots = rules.openSlots(listOf(v3, v4), listOf(a2), nov12.plusDays(1), prague)
        assertEquals(
            listOf(
                OpenSlot(DaySpan(8 * 60, 10 * 60), null, SlotLane.Full),
                OpenSlot(DaySpan(14 * 60, 16 * 60), DaySpan(15 * 60, 16 * 60), SlotLane.Full),
            ),
            slots,
        )
    }

    @Test
    fun `two overlapping open jobs take a lane each`() {
        val v5 = job("V5", 14, "09:00", 210)
        val r1 = job("R1", 14, "10:00", 120)
        val lanes = rules.openSlots(listOf(v5, r1), emptyList(), nov12.plusDays(2), prague).map { it.lane }
        assertEquals(listOf(SlotLane.Top, SlotLane.Bottom), lanes)
    }

    @Test
    fun `the day is named today, tomorrow or by its weekday`() {
        assertEquals(BoardText.Res(R.string.day_today), rules.dayName(nov12, nov12, en))
        assertEquals(BoardText.Res(R.string.day_tomorrow), rules.dayName(nov12.plusDays(1), nov12, en))
        assertEquals(BoardText.Literal("Saturday"), rules.dayName(nov12.plusDays(2), nov12, en))
    }

    @Test
    fun `the day header names her job, or her jobs in start order, by their count`() {
        val late = job("b", 12, "14:00", 120, status = 2)
        val early = job("a", 12, "08:00", 120, status = 2)
        assertEquals(
            BoardText.Plural(R.plurals.orders_day_your_job, 1, listOf("14:00–16:00")),
            rules.dayMineLabel(listOf(late), short = false, zone = prague, locale = en),
        )
        assertEquals(
            BoardText.Plural(R.plurals.orders_day_your_job, 2, listOf("08:00–10:00, 14:00–16:00")),
            rules.dayMineLabel(listOf(late, early), short = false, zone = prague, locale = en),
        )
        assertEquals(
            BoardText.Plural(R.plurals.orders_day_yours_short, 2, listOf("08:00–10:00, 14:00–16:00")),
            rules.dayMineLabel(listOf(late, early), short = true, zone = prague, locale = en),
        )
        assertNull(rules.dayMineLabel(emptyList(), short = false, zone = prague, locale = en))
    }

    @Test
    fun `the overlap finding names the day and her job when one day clashes, else the count`() {
        val a2 = job("A2", 13, "15:00", 180, status = 2)
        val v4 = job("V4", 13, "14:00", 120)
        assertEquals(
            BoardText.Plural(R.plurals.orders_overlap_day, 1, listOf(BoardText.Res(R.string.day_tomorrow), 1, "15:00–18:00")),
            rules.overlapFinding(listOf(v4 to a2), nov12, prague, en),
        )
        val a3 = job("A3", 16, "08:00", 120, status = 2)
        val v9 = job("V9", 16, "09:00", 120)
        assertEquals(
            BoardText.Plural(R.plurals.available_overlap_count, 2),
            rules.overlapFinding(listOf(v4 to a2, v9 to a3), nov12, prague, en),
        )
        assertNull(rules.overlapFinding(emptyList(), nov12, prague, en))
    }

    @Test
    fun `the week strip counts the loaded open jobs of the next 7 days, not the badge`() {
        val open = listOf(
            job("V3", 13, "08:00", 120), job("V4", 13, "14:00", 120),
            job("V5", 14, "09:00", 210), job("R1", 14, "10:00", 120),
            job("V6", 16, "11:00", 180), job("V7", 16, "14:00", 120),
            job("V8", 17, "10:00", 120), job("V9", 17, "15:00", 120),
            job("V10", 19, "10:00", 120),
        )
        val own = listOf(job("A1", 12, "14:00", 120, status = 4), job("A2", 13, "15:00", 180, status = 2))
        val cells = rules.weekCells(nov12, open, own, prague, loaded = true)
        assertEquals((0..6).map { nov12.plusDays(it.toLong()) }, cells.map { it.day })
        assertEquals(listOf(0, 2, 2, 0, 2, 2, 0), cells.map { it.open })
        assertEquals(8, cells.sumOf { it.open ?: 0 })
        assertEquals(listOf(true, true, false, false, false, false, false), cells.map { it.mine })
    }

    @Test
    fun `the week cell says its date, your job on her days, and its count, a real 0 included`() {
        assertEquals(
            BoardText.Joined(listOf(BoardText.Literal("Friday 13 November"), BoardText.Plural(R.plurals.active_week_open_count, 2)), ", "),
            rules.weekCellLabel(WeekCell(nov12.plusDays(1), 2, mine = false), "Friday 13 November"),
        )
        assertEquals(
            BoardText.Joined(
                listOf(
                    BoardText.Literal("Thursday 12 November"),
                    BoardText.Res(R.string.active_week_legend_your_job),
                    BoardText.Plural(R.plurals.active_week_open_count, 0),
                ),
                ", ",
            ),
            rules.weekCellLabel(WeekCell(nov12, 0, mine = true), "Thursday 12 November"),
        )
    }

    @Test
    fun `the run step follows the status, and cleaning closes at the first after photo solo, her own mark on a crew`() {
        val photo = pragueAt(12, "15:46")
        assertEquals(RunStep.SetOff, rules.runStep(2, crew = false, myMarkAt = null, firstAfterPhotoAt = null))
        assertEquals(RunStep.GetIn, rules.runStep(3, crew = false, myMarkAt = null, firstAfterPhotoAt = null))
        assertEquals(RunStep.Clean, rules.runStep(4, crew = false, myMarkAt = null, firstAfterPhotoAt = null))
        assertEquals(RunStep.Finish, rules.runStep(4, crew = false, myMarkAt = null, firstAfterPhotoAt = photo))
        assertEquals(RunStep.Clean, rules.runStep(4, crew = true, myMarkAt = null, firstAfterPhotoAt = photo))
        assertEquals(RunStep.Finish, rules.runStep(4, crew = true, myMarkAt = pragueAt(12, "17:44"), firstAfterPhotoAt = null))
        assertEquals(RunStep.Done, rules.runStep(5, crew = false, myMarkAt = null, firstAfterPhotoAt = null))
        assertEquals(RunStep.Cancelled, rules.runStep(6, crew = false, myMarkAt = null, firstAfterPhotoAt = null))
    }

    private fun a1Status(
        status: Int,
        now: String,
        nowDay: Int = 12,
        day: Int = 12,
        from: String = "14:00",
        minutes: Int = 120,
        crew: Boolean = false,
        mark: Instant? = null,
        photo: Instant? = null,
        lockout: Instant? = null,
    ) = rules.runStatus(
        status = status, start = pragueAt(day, from), minutes = minutes, now = pragueAt(nowDay, now), zone = prague, locale = en,
        crew = crew, myMarkAt = mark, firstAfterPhotoAt = photo, lockoutAt = lockout,
    )

    private val settingOff = BoardText.Res(R.string.run_step_set_off)

    @Test
    fun `setting off on another day says tomorrow or in N days`() {
        assertEquals(listOf(settingOff, BoardText.Res(R.string.day_tomorrow)), a1Status(2, "09:20", day = 13, from = "15:00", minutes = 180))
        assertEquals(listOf(settingOff, BoardText.Plural(R.plurals.detail_in_days, 4)), a1Status(2, "09:20", day = 16, from = "08:00"))
    }

    @Test
    fun `setting off today counts down to the set-off, then says go, then says it was due`() {
        assertEquals(listOf(settingOff, BoardText.Res(R.string.active_hero_in, listOf(rules.durationText(220)))), a1Status(2, "09:20"))
        assertEquals(listOf(settingOff, BoardText.Res(R.string.status_set_off_now)), a1Status(2, "13:10"))
        assertEquals(listOf(settingOff, BoardText.Res(R.string.active_hero_due, listOf("14:00"))), a1Status(2, "14:10"))
    }

    @Test
    fun `getting in is on the way before the start, was due after it, and waits for the company once reported`() {
        assertEquals(
            listOf(BoardText.Res(R.string.status_on_the_way), BoardText.Res(R.string.active_hero_in, listOf(rules.durationText(20)))),
            a1Status(3, "13:40"),
        )
        assertEquals(listOf(BoardText.Res(R.string.run_step_get_in), BoardText.Res(R.string.active_hero_due, listOf("14:00"))), a1Status(3, "14:17"))
        assertEquals(
            listOf(BoardText.Res(R.string.detail_waiting_company), BoardText.Res(R.string.detail_reported_at, listOf("14:19"))),
            a1Status(3, "14:25", lockout = pragueAt(12, "14:19")),
        )
    }

    @Test
    fun `cleaning and finishing count to the booked end, then say booked until`() {
        assertEquals(
            listOf(BoardText.Res(R.string.run_step_clean), BoardText.Res(R.string.detail_left, listOf(rules.durationText(48)))),
            a1Status(4, "15:12"),
        )
        assertEquals(
            listOf(BoardText.Res(R.string.run_step_finish), BoardText.Res(R.string.detail_left, listOf(rules.durationText(11)))),
            a1Status(4, "15:49", photo = pragueAt(12, "15:46")),
        )
        assertEquals(
            listOf(BoardText.Res(R.string.run_step_clean), BoardText.Res(R.string.active_hero_until, listOf("16:00"))),
            a1Status(4, "16:10"),
        )
    }

    @Test
    fun `a crew-mate's mark or photo never moves her to finishing, her own mark does`() {
        val crewDay = { mark: Instant? ->
            a1Status(4, "17:46", nowDay = 13, day = 13, from = "15:00", minutes = 180, crew = true, mark = mark, photo = pragueAt(13, "17:38"))
        }
        assertEquals(BoardText.Res(R.string.run_step_clean), crewDay(null).first())
        assertEquals(BoardText.Res(R.string.run_step_finish), crewDay(pragueAt(13, "17:44")).first())
        assertEquals(BoardText.Res(R.string.detail_left, listOf(rules.durationText(14))), crewDay(null).last())
    }

    @Test
    fun `a completed job says when it finished, with the day once a day has passed`() {
        val done = { now: Instant, finished: Instant ->
            rules.runStatus(
                status = 5, start = pragueAt(10, "09:00"), minutes = 120, now = now, zone = prague, locale = en,
                finishedAt = finished, dayPattern = "EEE, MMM d",
            )
        }
        assertEquals(
            listOf(BoardText.Res(R.string.tracker_headline_done), BoardText.Res(R.string.detail_finished_at, listOf("15:51"))),
            done(pragueAt(12, "16:20"), pragueAt(12, "15:51")),
        )
        assertEquals(
            listOf(BoardText.Res(R.string.tracker_headline_done), BoardText.Res(R.string.detail_finished_at, listOf("Tue, Nov 10, 10:52"))),
            done(pragueAt(12, "09:20"), pragueAt(10, "10:52")),
        )
    }

    @Test
    fun `a cancelled job is neutral without a reason and says why with one`() {
        val cancelled = { reason: String? ->
            rules.runStatus(
                status = 6, start = pragueAt(12, "11:00"), minutes = 120, now = pragueAt(12, "09:20"), zone = prague, locale = en,
                cancelledAt = pragueAt(12, "08:10"), cancelReason = reason,
            )
        }
        assertEquals(listOf(BoardText.Res(R.string.detail_cancelled_at, listOf("08:10"))), cancelled(null))
        assertEquals(
            listOf(BoardText.Res(R.string.detail_cancelled_at, listOf("08:10")), BoardText.Res(R.string.order_cancelled_customer_lockout)),
            cancelled("order.cancelled.customer_lockout"),
        )
        listOf(
            "order.cancelled.recurring_not_confirmed" to R.string.order_cancelled_recurring_not_confirmed,
            "order.cancelled.payment_not_completed" to R.string.order_cancelled_payment_not_completed,
            "order.cancelled.no_cleaner_available" to R.string.order_cancelled_no_cleaner_available,
            "order.cancelled.company_wind_down" to R.string.order_cancelled_company_wind_down,
        ).forEach { (key, res) -> assertEquals(BoardText.Res(res), cancelled(key).last()) }
        assertEquals(1, cancelled("order.cancelled.something_new").size)
    }

    @Test
    fun `three words or more tie their last two, never across a separator`() {
        assertEquals("Cash: collect 1 240 Kč for the company", rules.tie("Cash: collect 1 240 Kč for the company"))
        assertEquals("Under way", rules.tie("Under way"))
        assertEquals("Praha 5 · Jana", rules.tie("Praha 5 · Jana"))
        assertEquals("Take", rules.tie("Take"))
    }

    @Test
    fun `the separator flow fills each line and never starts or ends one with a dot`() {
        assertEquals(listOf(listOf(0, 1), listOf(2)), rules.flowLines(listOf(50, 60, 70), separator = 10, maxWidth = 130))
        assertEquals(listOf(listOf(0), listOf(1)), rules.flowLines(listOf(200, 20), separator = 10, maxWidth = 100))
        assertEquals(listOf(listOf(0, 1, 2)), rules.flowLines(listOf(30, 30, 30), separator = 5, maxWidth = 100))
        assertEquals(emptyList<List<Int>>(), rules.flowLines(emptyList(), separator = 5, maxWidth = 100))
    }

    @Test
    fun `a tied flow never starts a line with its last item alone, it takes the item before it along`() {
        assertEquals(listOf(listOf(0, 1), listOf(2)), rules.flowLines(listOf(50, 40, 30), separator = 10, maxWidth = 110))
        assertEquals(listOf(listOf(0), listOf(1, 2)), rules.flowLines(listOf(50, 40, 30), separator = 10, maxWidth = 110, tieLast = true))
        assertEquals(
            listOf(listOf(0, 1), listOf(2, 3)),
            rules.flowLines(listOf(150, 50, 45, 60), separator = 14, maxWidth = 280, tieLast = true),
        )
        assertEquals(listOf(listOf(0, 1, 2)), rules.flowLines(listOf(30, 30, 30), separator = 5, maxWidth = 100, tieLast = true))
        assertEquals(listOf(listOf(0, 1), listOf(2)), rules.flowLines(listOf(50, 60, 70), separator = 10, maxWidth = 130, tieLast = true))
        assertEquals(listOf(listOf(0), listOf(1)), rules.flowLines(listOf(100, 100), separator = 10, maxWidth = 150, tieLast = true))
        assertEquals(listOf(listOf(0)), rules.flowLines(listOf(200), separator = 10, maxWidth = 100, tieLast = true))
        assertEquals(emptyList<List<Int>>(), rules.flowLines(emptyList(), separator = 5, maxWidth = 100, tieLast = true))
    }

    @Test
    fun `an Available row's scope always runs full width under its text and tile, whatever its length`() {
        assertTrue(rules.scopeFullWidth(besideHeight = 90, wideHeight = 100, measured = false))
        assertTrue(rules.scopeFullWidth(besideHeight = 100, wideHeight = 100, measured = false))
        assertTrue(rules.scopeFullWidth(besideHeight = 120, wideHeight = 100, measured = false))
    }

    @Test
    fun `the reserved card's scope goes full width only when that makes its row shorter`() {
        assertTrue(rules.scopeFullWidth(besideHeight = 120, wideHeight = 100, measured = true))
        assertFalse(rules.scopeFullWidth(besideHeight = 100, wideHeight = 100, measured = true))
        assertFalse(rules.scopeFullWidth(besideHeight = 90, wideHeight = 100, measured = true))
    }

    private fun cashRow(status: Int?, due: Double = 1240.0) = row(cash = true, due = due).copy(paymentStatus = status?.let { Code(value = it) })

    @Test
    fun `once the cash is marked collected the flag says so, before the job is complete`() {
        val cs = Locale.forLanguageTag("cs-CZ")
        assertEquals(BoardText.Res(R.string.history_cash_collected, listOf("1 240 Kč")), rules.cashFlag(cashRow(2), cs))
        assertEquals(BoardText.Res(R.string.orders_flag_cash_collect, listOf("1 240 Kč")), rules.cashFlag(cashRow(1), cs))
        assertEquals(BoardText.Res(R.string.orders_flag_cash_collect, listOf("1 240 Kč")), rules.cashFlag(cashRow(null), cs))
    }

    @Test
    fun `History says what happened to the cash by the payment status`() {
        val cs = Locale.forLanguageTag("cs-CZ")
        assertEquals(listOf(BoardText.Res(R.string.history_cash_collected, listOf("1 240 Kč"))), rules.historyCash(cashRow(2), cs))
        assertEquals(
            listOf(BoardText.Res(R.string.history_cash_not_paid, listOf("1 440 Kč")), BoardText.Res(R.string.history_paid_in_full)),
            rules.historyCash(cashRow(1, due = 1440.0), cs),
        )
        assertEquals(emptyList<BoardText>(), rules.historyCash(cashRow(3), cs))
        assertEquals(emptyList<BoardText>(), rules.historyCash(row(cash = false, due = 1240.0).copy(paymentStatus = Code(value = 2)), cs))
    }

    @Test
    fun `the cash panel shows the first balance, a hidden one first, and none without a balance`() {
        val held = CashHeld(currencyCode = "CZK", amount = 1240.0, floatCap = 3000.0, cashJobsHidden = false)
        val over = CashHeld(currencyCode = "EUR", amount = 130.0, floatCap = 120.0, cashJobsHidden = true)
        assertEquals(held, rules.cashPanelRow(listOf(held)))
        assertEquals(over, rules.cashPanelRow(listOf(held, over)))
        assertNull(rules.cashPanelRow(emptyList()))
    }

    @Test
    fun `the meter fills at the true ratio, and no cap draws no meter`() {
        assertEquals(1240f / 3000f, rules.cashRatio(1240.0, 3000.0)!!, 0.0001f)
        assertEquals(1f, rules.cashRatio(3200.4, 3000.0)!!, 0.0001f)
        assertNull(rules.cashRatio(1240.0, null))
    }

    @Test
    fun `the month grid starts on Monday and stops at the current week`() {
        val grid = rules.historyGrid(LocalDate.of(2026, 11, 1), rules.historyGridEnd(LocalDate.of(2026, 11, 30), nov12))
        assertEquals(LocalDate.of(2026, 10, 26), grid.first())
        assertEquals(LocalDate.of(2026, 11, 15), grid.last())
        assertEquals(21, grid.size)
        assertEquals(LocalDate.of(2026, 11, 1), rules.historyGridEnd(LocalDate.of(2026, 11, 30), LocalDate.of(2026, 11, 1)))
        assertEquals(LocalDate.of(2026, 10, 31), rules.historyGridEnd(LocalDate.of(2026, 10, 31), nov12))
        assertEquals(7, rules.historyGrid(LocalDate.of(2026, 11, 1), LocalDate.of(2026, 11, 1)).size)
    }

    @Test
    fun `November's totals sum the rows to the haléř and round the booked hours`() {
        val pays = listOf(537.0 to 180, 829.0 to 120, 690.0 to 120, 944.0 to 120, 776.1 to 240, 714.0 to 120, 762.0 to 240, 600.0 to 120, 654.0 to 120)
        val rows = pays.mapIndexed { i, (pay, minutes) -> row(id = "h$i", pay = pay, minutes = minutes, status = 5) }
        assertEquals(650610L, Math.round(earningsByCurrency(rows).single().second * 100))
        val totals = rules.historyTotals(rows, 9)
        assertEquals(9, totals.jobs)
        assertEquals(23, totals.hours)
    }

    private val resDir: java.io.File = sequenceOf(
        java.io.File("src/main/res"),
        java.io.File("partner-app/src/main/res"),
        java.io.File("src/cleansia_android/partner-app/src/main/res"),
    ).firstOrNull { it.isDirectory } ?: error("partner-app res/ not found from ${java.io.File(".").absolutePath}")

    private val localeDirs = mapOf("en" to "values", "cs" to "values-cs", "sk" to "values-sk", "uk" to "values-uk", "ru" to "values-ru")

    private fun xml(locale: String) = java.io.File(resDir, "${localeDirs.getValue(locale)}/strings.xml").readText()

    private fun string(locale: String, key: String): String? =
        Regex("<string name=\"$key\">(.*?)</string>", RegexOption.DOT_MATCHES_ALL).find(xml(locale))?.groupValues?.get(1)

    private fun plural(locale: String, key: String): Map<String, String>? =
        Regex("<plurals name=\"$key\">(.*?)</plurals>", RegexOption.DOT_MATCHES_ALL).find(xml(locale))?.groupValues?.get(1)?.let { body ->
            Regex("<item quantity=\"(\\w+)\">(.*?)</item>").findAll(body).associate { it.groupValues[1] to it.groupValues[2] }
        }

    private val quantities = mapOf(
        "en" to setOf("one", "other"),
        "cs" to setOf("one", "few", "other"),
        "sk" to setOf("one", "few", "other"),
        "uk" to setOf("one", "few", "many"),
        "ru" to setOf("one", "few", "many"),
    )

    @Test
    fun `every working-day string is written in five locales and keeps its slots`() {
        val strings = mapOf(
            "cash_hidden_back" to listOf("%1\$s"), "cash_limit_at" to listOf("%1\$s"), "orders_flag_under_way_short" to emptyList(),
            "orders_flag_cash_collect" to listOf("%1\$s"), "available_checked_at" to listOf("%1\$s"),
            "orders_your_next_jobs" to emptyList(), "cash_held_title" to emptyList(), "cash_held_of_limit" to listOf("%1\$s", "%2\$s"),
            "cash_held_hint" to emptyList(), "active_empty_body" to emptyList(), "active_nothing_else" to emptyList(),
            "active_open_next_7_days" to emptyList(), "active_week_legend_your_job" to emptyList(), "active_soonest_open" to emptyList(),
            "active_see_offer" to emptyList(), "history_cash_collected" to listOf("%1\$s"), "history_cash_not_paid" to listOf("%1\$s"),
            "history_paid_in_full" to emptyList(), "history_empty_month" to emptyList(), "history_empty_week" to emptyList(),
            "history_empty_body" to emptyList(), "history_last_month" to emptyList(), "history_show_period" to listOf("%1\$s"),
            "detail_left" to listOf("%1\$s"), "detail_finished_at" to listOf("%1\$s"), "detail_cancelled_at" to listOf("%1\$s"),
            "status_set_off_now" to emptyList(), "detail_waiting_company" to emptyList(), "detail_reported_at" to listOf("%1\$s"),
            "run_step_set_off" to emptyList(), "run_step_get_in" to emptyList(), "run_step_clean" to emptyList(), "run_step_finish" to emptyList(),
            "order_cancelled_customer_lockout" to emptyList(), "order_cancelled_recurring_not_confirmed" to emptyList(),
            "order_cancelled_payment_not_completed" to emptyList(), "order_cancelled_no_cleaner_available" to emptyList(),
            "order_cancelled_company_wind_down" to emptyList(),
        )
        val plurals = mapOf(
            "orders_day_your_job" to listOf("%1\$s"), "orders_day_yours_short" to listOf("%1\$s"), "active_week_open_count" to listOf("%1\$d"),
            "orders_overlap_day" to listOf("%1\$s", "%2\$d", "%3\$s"), "active_see_open" to listOf("%1\$d"), "detail_in_days" to listOf("%1\$d"),
        )
        localeDirs.keys.forEach { locale ->
            strings.forEach { (key, slots) ->
                val value = string(locale, key)
                assertTrue("$locale is missing $key", !value.isNullOrBlank())
                slots.forEach { assertTrue("$locale/$key lost $it: $value", value!!.contains(it)) }
            }
            plurals.forEach { (key, slots) ->
                val items = plural(locale, key) ?: error("$locale is missing the plural $key")
                assertTrue("$locale/$key lacks ${quantities.getValue(locale) - items.keys}", items.keys.containsAll(quantities.getValue(locale)))
                items.forEach { (quantity, value) -> slots.forEach { assertTrue("$locale/$key/$quantity lost $it: $value", value.contains(it)) } }
            }
        }
    }

    @Test
    fun `the changed keys read as decided, and the dropped keys are gone`() {
        listOf("en" to "Your share", "cs" to "Váš podíl", "sk" to "Váš podiel", "uk" to "Ваша частка", "ru" to "Ваша доля").forEach { (locale, value) ->
            assertEquals(value, string(locale, "orders_card_your_share"))
        }
        assertEquals("No active jobs", string("en", "no_active_orders"))
        assertEquals("Žádné aktivní zakázky", string("cs", "no_active_orders"))
        assertEquals("%1\$d místo obsazeno", plural("cs", "orders_flag_crew_taken")?.get("one"))
        assertEquals("%1\$d місце вже зайняте", plural("uk", "orders_flag_crew_taken")?.get("one"))
        assertEquals("%1\$d мест уже занято", plural("ru", "orders_flag_crew_taken")?.get("many"))
        assertEquals("%1\$d already on it", plural("en", "orders_flag_crew_taken")?.get("one"))
        assertEquals("ваше замовлення %1\$s", plural("uk", "orders_day_your_job")?.get("one"))
        assertEquals("ваші замовлення %1\$s", plural("uk", "orders_day_your_job")?.get("few"))
        localeDirs.keys.forEach { locale ->
            listOf("orders_flag_cash", "orders_flag_under_way", "orders_card_job_duration", "available_cash_hidden_notice", "swipe_to_start").forEach { key ->
                assertNull("$locale still declares $key", string(locale, key))
            }
        }
    }

    // ── set-off when, day groups, soonest open, See N, cash collected, freed slot, after photo ──

    @Test
    fun `the locked set-off counts down on the set-off's own day and names the day on any other`() {
        assertEquals(BoardText.Res(R.string.active_hero_in, listOf(rules.durationText(220))), rules.setOffWhen(pragueAt(12, "13:00"), pragueAt(12, "09:20"), prague, en))
        assertEquals(BoardText.Literal("Friday" + OrdersBoardRules.SEP + "13 Nov"), rules.setOffWhen(pragueAt(13, "14:00"), pragueAt(12, "09:20"), prague, en))
        assertEquals(BoardText.Literal("Monday" + OrdersBoardRules.SEP + "16 Nov"), rules.setOffWhen(pragueAt(16, "07:00"), pragueAt(12, "09:20"), prague, en))
    }

    @Test
    fun `best pay keeps one panel per day, each day where it first appears, its rows in the list's order`() {
        val tomorrowBest = job("t1", 13, "15:00", 120)
        val today = job("d1", 12, "08:00", 120)
        val tomorrowNext = job("t2", 13, "08:00", 120)
        val groups = rules.dayGroups(listOf(tomorrowBest, today, tomorrowNext), prague)
        assertEquals(listOf(nov12.plusDays(1), nov12), groups.map { it.first })
        assertEquals(listOf(listOf(tomorrowBest, tomorrowNext), listOf(today)), groups.map { it.second })
    }

    @Test
    fun `the soonest open job is the earliest one not ended that does not overlap her own`() {
        val now = pragueAt(12, "14:30")
        val own = listOf(job("A1", 12, "14:00", 120, status = 4))
        val ended = job("v0", 12, "08:00", 240, status = 4)
        val overlapping = job("v1", 12, "15:00", 120)
        val next = job("v3", 13, "08:00", 120)
        val later = job("v4", 13, "14:00", 120)
        assertEquals(next, rules.soonestOpen(listOf(ended, overlapping, later, next), own, now))
        assertNull(rules.soonestOpen(listOf(ended, overlapping), own, now))
        assertEquals(overlapping, rules.soonestOpen(listOf(later, overlapping), emptyList(), now))
    }

    @Test
    fun `See N open jobs is the Available badge, the server's total, and is not offered at 0 or before Available answers`() {
        val at = pragueAt(12, "09:20")
        assertEquals(12, rules.availableBadge(loaded = true, total = 12, rows = emptyList(), now = at))
        assertNull(rules.availableBadge(loaded = true, total = 0, rows = emptyList(), now = at))
        assertNull(rules.availableBadge(loaded = false, total = null, rows = emptyList(), now = at))
    }

    @Test
    fun `the Available badge never counts a loaded row whose booked slot has ended`() {
        val at = pragueAt(12, "12:30")
        val ended = job("V1", 12, "08:00", 240)
        val endsNow = job("V2", 12, "11:30", 60)
        val ahead = job("V3", 13, "08:00", 120)
        assertNull(rules.availableBadge(loaded = true, total = 1, rows = listOf(ended), now = at))
        assertNull(rules.availableBadge(loaded = true, total = 2, rows = listOf(ended, endsNow), now = at))
        assertEquals(1, rules.availableBadge(loaded = true, total = 2, rows = listOf(ended, ahead), now = at))
        assertEquals(135, rules.availableBadge(loaded = true, total = 137, rows = listOf(ended, endsNow, ahead), now = at))
    }

    @Test
    fun `the week strip prints no count while Available has not answered, and a real 0 once it has`() {
        val own = listOf(job("A1", 12, "14:00", 120, status = 4))
        val waiting = rules.weekCells(nov12, emptyList(), own, prague, loaded = false)
        assertEquals(List(7) { null }, waiting.map { it.open })
        assertEquals(listOf(true, false, false, false, false, false, false), waiting.map { it.mine })
        assertEquals(List(7) { 0 }, rules.weekCells(nov12, emptyList(), own, prague, loaded = true).map { it.open })
        assertEquals(
            BoardText.Joined(listOf(BoardText.Literal("Thursday 12 November"), BoardText.Res(R.string.active_week_legend_your_job)), ", "),
            rules.weekCellLabel(waiting.first(), "Thursday 12 November"),
        )
    }

    @Test
    fun `a day-line mark is never narrower than the least width, a shorter one centred on its span and kept on the track`() {
        assertEquals(100f to 20f, rules.markBounds(left = 100f, right = 120f, track = 300f, min = 12f))
        assertEquals(100f to 12f, rules.markBounds(left = 100f, right = 112f, track = 300f, min = 12f))
        assertEquals(96f to 12f, rules.markBounds(left = 100f, right = 104f, track = 300f, min = 12f))
        assertEquals(0f to 12f, rules.markBounds(left = 1f, right = 3f, track = 300f, min = 12f))
        assertEquals(288f to 12f, rules.markBounds(left = 297f, right = 299f, track = 300f, min = 12f))
    }

    @Test
    fun `the cash is collected once a cash job's payment is Paid, and a card job has none to collect`() {
        assertTrue(rules.cashCollected(row(cash = true, due = 1240.0).copy(paymentStatus = Code(value = 2))))
        assertFalse(rules.cashCollected(row(cash = true, due = 1240.0).copy(paymentStatus = Code(value = 1))))
        assertFalse(rules.cashCollected(row(cash = true, due = 1240.0)))
        assertFalse(rules.cashCollected(row().copy(paymentStatus = Code(value = 2))))
    }

    @Test
    fun `a cancellation frees its slot from its start or now to its end, and nothing once the end has passed`() {
        val x1 = job("X1", 12, "11:00", 120, status = 6)
        assertEquals(DaySpan(11 * 60, 13 * 60), rules.freedSlot(x1, pragueAt(12, "09:20"), prague))
        assertEquals(DaySpan(12 * 60, 13 * 60), rules.freedSlot(x1, pragueAt(12, "12:00"), prague))
        assertNull(rules.freedSlot(x1, pragueAt(12, "14:00"), prague))
        assertNull(rules.freedSlot(x1, pragueAt(13, "09:20"), prague))
        assertEquals(DaySpan(11 * 60, 13 * 60), rules.freedSlot(x1, pragueAt(11, "18:00"), prague))
    }

    @Test
    fun `the server's after photo closes Cleaning on a solo job before the photo list answers`() {
        assertEquals(RunStep.Finish, rules.runStep(4, crew = false, myMarkAt = null, firstAfterPhotoAt = null, afterPhoto = true))
        assertEquals(RunStep.Clean, rules.runStep(4, crew = false, myMarkAt = null, firstAfterPhotoAt = null, afterPhoto = false))
        assertEquals(RunStep.Clean, rules.runStep(4, crew = true, myMarkAt = null, firstAfterPhotoAt = null, afterPhoto = true))
        assertEquals(
            listOf(BoardText.Res(R.string.run_step_finish), BoardText.Res(R.string.detail_left, listOf(rules.durationText(11)))),
            rules.runStatus(status = 4, start = pragueAt(12, "14:00"), minutes = 120, now = pragueAt(12, "15:49"), zone = prague, locale = en, afterPhoto = true),
        )
    }

    @Test
    fun `the now mark gives way to a mark it would touch, since its tick already says now`() {
        val ukAt360Large = listOf(191.5f to 124f, 410f to 422f)
        assertEquals(listOf(null, 199f), rules.dayLineLabelLefts(ukAt360Large, nowIndex = 0, track = 984f, gap = 12f))
        val enAt412 = listOf(221.5f to 94f, 475f to 280f)
        assertEquals(listOf(174.5f, 335f), rules.dayLineLabelLefts(enAt412, nowIndex = 0, track = 1140f, gap = 12f))
        assertEquals(listOf(null, 205f), rules.dayLineLabelLefts(listOf(150f to 100f, 255f to 100f), nowIndex = 0, track = 1000f, gap = 12f))
        assertEquals(listOf(100f, 212f), rules.dayLineLabelLefts(listOf(150f to 100f, 262f to 100f), nowIndex = 0, track = 1000f, gap = 12f))
        assertEquals(listOf(205f, null), rules.dayLineLabelLefts(listOf(255f to 100f, 150f to 100f), nowIndex = 1, track = 1000f, gap = 12f))
        assertEquals(listOf(100f, 205f), rules.dayLineLabelLefts(listOf(150f to 100f, 255f to 100f), nowIndex = null, track = 1000f, gap = 12f))
    }

    @Test
    fun `a day-line mark near an end starts or ends at its moment, and every mark stays on the track`() {
        assertEquals(listOf(50f), rules.dayLineLabelLefts(listOf(50f to 100f), nowIndex = 0, track = 1000f, gap = 12f))
        assertEquals(listOf(860f), rules.dayLineLabelLefts(listOf(960f to 100f), nowIndex = 0, track = 1000f, gap = 12f))
        assertEquals(listOf(450f), rules.dayLineLabelLefts(listOf(500f to 100f), nowIndex = null, track = 1000f, gap = 12f))
        assertEquals(listOf(0f), rules.dayLineLabelLefts(listOf(85f to 200f), nowIndex = null, track = 1000f, gap = 12f))
        assertEquals(listOf(700f), rules.dayLineLabelLefts(listOf(910f to 300f), nowIndex = null, track = 1000f, gap = 12f))
        assertEquals(listOf(0f), rules.dayLineLabelLefts(listOf(500f to 1200f), nowIndex = null, track = 1000f, gap = 12f))
    }

    @Test
    fun `a count is held to the word after it, so a line never ends on the number alone`() {
        assertEquals("1\u00A0ванна кімната", rules.tieCount("1 ванна кімната"))
        assertEquals("12\u00A0rooms", rules.tieCount("12 rooms"))
        assertEquals("Standard clean", rules.tieCount("Standard clean"))
        assertEquals("3", rules.tieCount("3"))
    }

    @Test
    fun `the offer teaser keeps See offer beside the job only while every run fits whole and the text is not large`() {
        assertFalse(rules.offerStacks(runWidths = listOf(120, 96, 140), column = 140, fontScale = 1f))
        assertTrue(rules.offerStacks(runWidths = listOf(120, 96, 141), column = 140, fontScale = 1f))
        assertTrue(rules.offerStacks(runWidths = listOf(10), column = 140, fontScale = 1.3f))
        assertFalse(rules.offerStacks(runWidths = emptyList(), column = 0, fontScale = 1.29f))
    }
}
