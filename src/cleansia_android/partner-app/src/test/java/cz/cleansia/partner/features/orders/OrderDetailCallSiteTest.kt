package cz.cleansia.partner.features.orders

import java.io.File
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * A resolver test cannot see the call site, and the call site is where this
 * defect lived: the wire carried the coarse address and the seat block, and the
 * three composables that own the detail's location and crew simply never read
 * them. So these pin that the screen renders *through* the resolvers and never
 * reaches around them into the raw `address` record.
 *
 * Evidence here is Kotlin source read off disk, which Gradle does not track as
 * an input — this file only means something under
 * `testDebugUnitTest --rerun-tasks --no-build-cache`.
 */
class OrderDetailCallSiteTest {

    private val featureDir: File = sequenceOf(
        File("."),
        File("partner-app"),
        File("src/cleansia_android/partner-app"),
    ).map { File(it, "src/main/java/cz/cleansia/partner/features/orders") }
        .firstOrNull { it.isDirectory }
        ?: error("orders feature dir not found from working dir ${File(".").absolutePath}")

    private val customerCard = source("CustomerCard.kt")
    private val detailScreen = source("OrderDetailScreen.kt")
    private val jobHead = source("JobHead.kt")
    private val scopeCard = source("ScopeCard.kt")
    private val primaryAction = source("OrderPrimaryAction.kt")
    private val heroPills = source("ActiveHero.kt")

    @Test
    fun `the head renders whichever location arrived`() {
        assertTrue(
            "the job head's street must come from OrderLocation.line — the coarse zone is a location too",
            detailScreen.contains("rules.streetLine(location.line)"),
        )
        listOf("OrderDetailScreen.kt" to detailScreen, "JobHead.kt" to jobHead, "CustomerCard.kt" to customerCard).forEach { (name, text) ->
            assertFalse(
                "$name must not format the address itself; orderLocation() decides what arrived",
                text.contains("formatSingleLine("),
            )
        }
    }

    @Test
    fun `Navigate is offered only for a street address`() {
        assertTrue(
            "the run sheet's contact pills must gate Navigate on OrderLocation.navigationTarget() — a maps " +
                "app opened on a city name is worse than no button",
            customerCard.contains("location.navigationTarget()"),
        )
        assertTrue(
            "the Navigate pill must still exist for the entitled reader, and only for a target",
            heroPills.contains("navigateTo?.let { target ->") && heroPills.contains("R.string.action_navigate"),
        )
    }

    /** The band's map is chosen from the location that arrived, by the tested mode; the map itself is drawn in one file. */
    @Test
    fun `the detail's map follows the location that arrived and never reads the raw address`() {
        assertFalse("OrderDetailScreen must not draw a map of its own", detailScreen.contains("MapboxMap(") || detailScreen.contains(".mapPoint("))
        assertTrue("the band's mode comes from jobMapMode", detailScreen.contains("jobMapMode("))
        val jobMap = source("JobMap.kt")
        assertFalse(
            "a tap on the band map would fight her pan; Navigate stays in the run sheet",
            jobMap.contains("launchNavigation(") || jobMap.contains("navigateTo") || jobMap.contains("action_navigate"),
        )
        assertTrue("the map she pans never rotates or tilts", jobMap.contains("rotateEnabled = false") && jobMap.contains("pitchEnabled = false"))
        val rawReads = detailScreen.lines().filter { it.contains("order.address") }
        assertTrue(
            "OrderDetailScreen must not read order.address directly: " +
                rawReads.joinToString(" | ") { it.trim() },
            rawReads.isEmpty(),
        )
    }

    @Test
    fun `the non-payment report is offered through its gate and sent only from its confirm`() {
        assertTrue(
            "the footer must offer the report through cashNotPaidReportable()",
            detailScreen.contains("canReportCashNotPaid = order.cashNotPaidReportable()"),
        )
        assertTrue(
            "the action must render only under its gate",
            primaryAction.contains("PinnedMove.CollectCash(canReportCashNotPaid)") && primaryAction.contains("if (move.canReportNotPaid)"),
        )
        assertTrue(
            "the report must be sent from the confirm dialog",
            Regex("""confirmingCashNotPaid = false\s+viewModel\.reportCashNotPaid\(\)""").containsMatchIn(detailScreen),
        )
    }

    @Test
    fun `withdrawing from the job is offered through its gate and sent only from its confirm`() {
        assertTrue(
            "the footer must offer Withdraw through droppable(now)",
            detailScreen.contains("canDrop = order.droppable(now)"),
        )
        assertTrue(
            "the footer on the ground must say Withdraw from this job (M21)",
            Regex("""private fun DetailFooter\([\s\S]*?R\.string\.order_drop_action""").containsMatchIn(detailScreen),
        )
        assertTrue(
            "the clock behind the gate must be read again on every return to the screen",
            detailScreen.contains("LifecycleEventEffect(Lifecycle.Event.ON_RESUME) { now = Instant.now() }"),
        )
        assertTrue(
            "the clock must be read again each minute, so the gate closes at the start",
            Regex("""LaunchedEffect\(now\)\s*\{\s*delay\(""").containsMatchIn(detailScreen),
        )
        assertTrue(
            "the drop must be sent from the confirm dialog",
            Regex("""confirmingDrop = false\s+viewModel\.dropOrder\(\)""").containsMatchIn(detailScreen),
        )
        assertTrue(
            "the screen must leave the job once the drop lands",
            Regex("""viewModel\.left\.collect\s*\{\s*onNavigateBack\(\)\s*\}""").containsMatchIn(detailScreen),
        )
        assertFalse(
            "the gate stays out of OrderPrimaryAction.kt, whose first `when (status)` the parity checker reads",
            primaryAction.contains("droppable"),
        )
    }

    @Test
    fun `the about panel renders the seat facts`() {
        assertTrue(
            "the about panel's crew block must resolve the crew through orderCrew() — TakeOrder's free-seat " +
                "conjunct refuses a take this screen otherwise never warned about",
            scopeCard.contains("orderCrew("),
        )
        assertTrue("the Cleaning step reads the crew's own seats", detailScreen.contains("order.crewSeats(myEmployeeId)"))
    }

    /**
     * Positive claims only. "The view does not consult a flag" is an absence assertion that goes
     * green when the view is renamed away and says nothing about the claim's content; that the view
     * renders *through* the resolver is the checkable half, and it is what
     * [OrderDisclosurePresentationTest] then drives at the divergent shape.
     */
    @Test
    fun `the redacted fields are rendered through the disclosure resolver`() {
        assertTrue(
            "CustomerCard must take the phone from OrderDisclosure — the server already withheld " +
                "it from anyone it meant to",
            customerCard.contains("disclosure.customerPhone") &&
                customerCard.contains("disclosure.showsCustomerContact"),
        )
        assertTrue(
            "the access card must gate on the door code's own arrival",
            detailScreen.contains("disclosure.showsAccessCard(status)"),
        )
        assertTrue(
            "floor, flat and mode show from Confirmed through their own gate (M12)",
            detailScreen.contains("disclosure.showsAccessFacts(status)"),
        )
        assertTrue(
            "the notes & issues section must gate on the record's own arrival",
            detailScreen.contains("disclosure.showsWorkRecordSection(canAddNotesOrIssues)"),
        )
        val rawReads = detailScreen.lines()
            .filter { it.contains("order.accessInstructions") || it.contains("order.customerPhone") }
        assertTrue(
            "OrderDetailScreen must not reach around orderDisclosure() into the raw fields: " +
                rawReads.joinToString(" | ") { it.trim() },
            rawReads.isEmpty(),
        )
    }

    /**
     * The scope line of the rule. Withdrawing these would offer "Slide to start" and the checklist
     * on a stranger's order, which is the failure the entitlement flag is the right input to.
     */
    @Test
    fun `the action gates still read the assignment flag`() {
        assertTrue(
            "the checklist and photo rails are the assignee's work tools",
            detailScreen.contains("val showWorkSections = isMine &&"),
        )
        assertTrue(
            "adding a note is a write, and a write is the assignee's",
            detailScreen.contains("isMine && (status == OrderStatus._3 || status == OrderStatus._4)"),
        )
        assertTrue(
            "the primary action decides what is OFFERED, which is not this rule's subject",
            source("OrderPrimaryAction.kt").contains("isAssignedToCurrentUser"),
        )
    }

    @Test
    fun `a past job says the customer's details were removed instead of promising them`() {
        assertTrue(
            "the detail must resolve the note through customerDetailsClosedNote()",
            detailScreen.contains("order.customerDetailsClosedNote()"),
        )
        assertTrue(
            "the customer card must render the note where the customer was",
            customerCard.contains("closedNote"),
        )
        assertTrue(
            "the about panel says the details were removed in the customer's place",
            detailScreen.contains("R.string.order_customer_details_closed_completed"),
        )
    }

    @Test
    fun `the cash button follows the tested gate`() {
        assertTrue(
            "the footer must take its cash gate from needsCashCollection()",
            detailScreen.contains("needsCashCollection = order.needsCashCollection()"),
        )
    }

    /** The detail shows no customer price; the cash to collect is the total less credit, `amountDueOnCard`. */
    @Test
    fun `the detail shows no customer price, and the cash is what the customer owes`() {
        listOf("OrderDetailScreen.kt" to detailScreen, "ScopeCard.kt" to scopeCard).forEach { (name, text) ->
            assertFalse("$name reads the customer's price", text.contains("order.totalPrice") || text.contains(".price"))
        }
        assertEquals(
            "the cash is said by the head's flag and the pinned move, so no block of the detail names amountDueOnCard itself",
            0,
            Regex("""order(\?)?\.amountDueOnCard""").findAll(detailScreen).count(),
        )
        assertTrue("the move and the confirm name it through cashDueText", detailScreen.contains("amountDueOnCard?.takeIf { it > 0 }"))
    }

    @Test
    fun `the cash confirm writes the amount as the pinned button does`() {
        assertTrue("the pinned button names the cash through cashDueText", detailScreen.contains("cashDue = order.cashDueText(appLocale())"))
        assertTrue(
            "the mark-collected confirm names the cash through cashDueText",
            Regex("""order\?\.cashDueText\(appLocale\(\)\)\s*\?\.let \{ stringResource\(R\.string\.partner_order_mark_cash_collected_confirm_message, it\) \}""")
                .containsMatchIn(detailScreen),
        )
    }

    @Test
    fun `job photos come from the camera and never reach a disk cache`() {
        val photosSection = source("PhotosSection.kt")
        assertTrue(
            "the job photos must open the picker camera-only — a gallery photo is already a " +
                "copy on the device",
            detailScreen.contains("rememberPhotoSourcePicker(cameraOnly = true)"),
        )
        assertFalse("no job photo opens the camera-or-library sheet", detailScreen.contains("cameraOnly = false"))
        assertTrue(
            "an order photo must not be written to Coil's disk cache",
            photosSection.contains(".diskCachePolicy(CachePolicy.DISABLED)"),
        )
        assertFalse(
            "Coil's network policy must stay enabled — with the disk cache also off it asks for " +
                "only-if-cached and every tile fails with a 504",
            photosSection.contains("networkCachePolicy(CachePolicy.DISABLED)"),
        )
        assertTrue(
            "the rails open and close on the server's photo windows",
            detailScreen.contains("canUploadBefore = photoWindowOpen(PhotoType._1, status)") &&
                detailScreen.contains("canUploadAfter = photoWindowOpen(PhotoType._2, status)"),
        )
    }

    @Test
    fun `a take from the detail ends on the taken panel, Open job closing onto the job and Back to jobs going to the board`() {
        val sheet = source("WorkContractSheet.kt")
        assertTrue("the sheet shows the taken panel for any host that says it was taken", sheet.contains("if (taken && facts != null)"))
        assertTrue("the detail keeps the sheet up on the taken panel", detailScreen.contains("taken = takenRow != null"))
        assertTrue("Open job closes the panel onto the taken job", detailScreen.contains("onOpenJob = viewModel::dismissContract"))
        assertTrue(
            "Back to jobs closes the panel and goes to the board's Available tab",
            Regex("""onBackToJobs = \{\s*viewModel\.dismissContract\(\)\s*viewModel\.showAvailable\(\)\s*onShowAvailable\(\)""")
                .containsMatchIn(detailScreen),
        )
    }

    @Test
    fun `the area map's veil is drawn on the map view, under the Mapbox wordmark and attribution`() {
        val jobMap = source("JobMap.kt")
        assertFalse("blurring the map composable blurs its ornaments with it", jobMap.contains("Modifier.blur("))
        assertTrue("the blur and the dim go on the map view itself", jobMap.contains("MapEffect(Unit) { view -> view.veil() }"))
    }

    @Test
    fun `the Active hero and the detail head say the status line the one builder makes`() {
        val ownWords = Regex("""R\.string\.(run_step_\w+|status_on_the_way|status_set_off_now|detail_left|detail_finished_at|detail_waiting_company|detail_reported_at)\b""")
        listOf("ActiveHero.kt" to heroPills, "OrderDetailScreen.kt" to detailScreen).forEach { (name, text) ->
            assertTrue("$name must build its status line with rules.runStatus(", text.contains("val statusItems = ") && text.contains("rules.runStatus("))
            assertTrue("$name must hand JobHead the line as built", text.contains("statusItems = statusItems,"))
            val words = ownWords.findAll(text).map { it.value }.toList()
            assertTrue("$name must not compose the status line's words itself: $words", words.isEmpty())
        }
    }

    @Test
    fun `the payouts link and See N open jobs wrap to a second line rather than cut their words`() {
        assertTrue(
            Regex(
                """CleansiaTextLink\(\s*text = stringResource\(R\.string\.history_payouts_link, stringResource\(R\.string\.earnings_title\)\),\s*""" +
                    """onClick = onOpenEarnings,\s*trailingIcon = Icons\.AutoMirrored\.Outlined\.KeyboardArrowRight,\s*maxLines = 2,""",
            ).containsMatchIn(body("PayoutsLink")),
        )
        assertTrue(
            Regex("""CleansiaOutlinedButton\(\s*text = OrdersBoardRules\.tie\(pluralStringResource\(R\.plurals\.active_see_open, total, total\)\),\s*onClick = onShowAvailable,\s*maxLines = 2,""")
                .containsMatchIn(body("SeeOpenButton")),
        )
        assertTrue("a wrapped week title never leaves its last word alone", body("WeekSection").contains("title = OrdersBoardRules.tie(stringResource(R.string.active_open_next_7_days))"))
    }

    @Test
    fun `Property holds each count to its noun`() {
        assertTrue(scopeCard.contains(".joinToString(rules.SEP) { rules.tieCount(it) }"))
    }

    @Test
    fun `the lock over a refused Take stays beside the first line of its wrapped reason`() {
        val overlaps = primaryAction.substringAfter("is PinnedMove.TakeOverlaps ->").substringBefore("is PinnedMove.SetOffLocked ->")
        assertTrue(overlaps.contains("OverlapReason(OrdersBoardRules.tie(reason)"))
        assertTrue(bodyOf(primaryAction, "OverlapReason").contains("it.getLineLeft(0)"))
    }

    private fun body(function: String): String {
        val from = detailScreen.indexOf("fun $function(").also { assertTrue("$function not found", it >= 0) }
        val next = Regex("""\n(@Composable|private fun|internal fun|fun |/\*\*)""").find(detailScreen, detailScreen.indexOf('\n', from))
        return detailScreen.substring(from, next?.range?.first ?: detailScreen.length)
    }

    /** The cash step says no amount and no rule: the head's flag says the cash (collected, it says so) and Payment the rule. */
    @Test
    fun `once the cash is collected the cash step's gate is done`() {
        val cash = body("CashBlock")
        assertTrue(cash.contains("val collected = OrdersBoardRules.cashCollected(row)"))
        assertTrue("collected, the gate row is drawn done", cash.contains("OrderTimelinePoint(if (collected) OrderTimelineSegment.Past else OrderTimelineSegment.Future)"))
        listOf("R.string.cash_rule", "OrdersBoardRules.money(").forEach { assertFalse("the cash step restates `$it`", cash.contains(it)) }
        assertTrue("the head's flag says the cash, collected or not", detailScreen.contains("rules.cashFlag(row, locale)"))
    }

    @Test
    fun `the not-paid confirm writes the amount as the board does`() {
        assertTrue(detailScreen.contains("cashDueLabel(order?.cashNotPaidOwed(), order?.asBoardRow()?.let(OrdersBoardRules::symbolOf))"))
    }

    @Test
    fun `every cash figure on the detail takes the board's symbol, the code's when the symbol is blank`() {
        assertFalse(detailScreen.contains("currency?.symbol"))
        assertTrue(detailScreen.contains("OrdersBoardRules.money(it, OrdersBoardRules.symbolOf(asBoardRow()), locale)"))
    }

    @Test
    fun `the cash collected gate row is drawn done with no time`() {
        val cash = body("CashBlock")
        assertTrue(Regex("""title = stringResource\(R\.string\.finish_cash_collected\),\s*sub = null,""").containsMatchIn(cash))
        assertFalse(cash.contains("cashCollectedAt"))
    }

    @Test
    fun `the head keeps the amber cash flag on the way too`() {
        assertTrue(detailScreen.contains("if (cash && (status == OrderStatus._2 || status == OrderStatus._3 || status == OrderStatus._4))"))
    }

    @Test
    fun `one after-photo fact feeds the step, the status line, the run sheet, the gate and the pinned move`() {
        val own = body("ownJobDetail")
        assertTrue(own.contains("val afterPhoto = order.hasAfterPhotos == true || afterPhotos.isNotEmpty()"))
        assertTrue(Regex("""afterPhoto = afterPhoto""").findAll(own).count() >= 4)
        assertTrue(own.contains("afterNeeded = slot && !afterPhoto"))
        assertFalse(detailScreen.contains("hasAfterPhoto = order.hasAfterPhotos == true"))
    }

    @Test
    fun `a late join is offered on time alone, as the server admits a cover seat`() {
        assertFalse(detailScreen.contains("hasAvailableSpots"))
        assertFalse(primaryAction.contains("seatOpen"))
    }

    @Test
    fun `the payment reason is the locked slide's alone, so the gate does not say it again`() {
        assertFalse("the gate repeats the locked slide's reason", detailScreen.contains("R.string.error_order_payment_not_confirmed"))
        assertFalse(detailScreen.contains("cardNotCharged"))
        val locked = primaryAction.substringAfter("PinnedMove.CompleteLocked ->").substringBefore("PinnedMove.Complete ->")
        assertTrue(locked.contains("stringResource(R.string.error_order_payment_not_confirmed)") && locked.contains("LockedSwipeToConfirm("))
    }

    @Test
    fun `a text link that waits for the move in flight is drawn and announced disabled`() {
        val setOff = primaryAction.substringAfter("is PinnedMove.SetOff ->").substringBefore("PinnedMove.Start ->")
        assertTrue(
            Regex("""CleansiaTextLink\(\s*text = stringResource\(R\.string\.late_already_there\),\s*onClick = onStartRequested,\s*enabled = inFlight == null,?\s*\)""")
                .containsMatchIn(setOff),
        )
        val notPaid = primaryAction.substringAfter("private fun CashNotPaidButton(")
        assertTrue(
            Regex("""CleansiaTextLink\(\s*text = stringResource\(R\.string\.order_cash_not_paid_action\),\s*onClick = onClick,\s*enabled = enabled,?\s*\)""")
                .containsMatchIn(notPaid),
        )
        assertFalse("a link that only swallows the tap still reads as enabled", primaryAction.contains("if (inFlight == null) onStartRequested()"))
        assertFalse("a link that only swallows the tap still reads as enabled", primaryAction.contains("if (enabled) onClick()"))
    }

    @Test
    fun `the Take refused for an overlap stays legible in the surface variant`() {
        val overlaps = primaryAction.substringAfter("is PinnedMove.TakeOverlaps ->").substringBefore("is PinnedMove.SetOffLocked ->")
        assertTrue(
            Regex(
                """CleansiaPrimaryButton\([\s\S]*?enabled = false,[\s\S]*?disabledContainerColor = MaterialTheme\.colorScheme\.surfaceVariant,\s*""" +
                    """disabledContentColor = MaterialTheme\.colorScheme\.onSurfaceVariant,""",
            ).containsMatchIn(overlaps),
        )
    }

    @Test
    fun `a cancelled job always draws its day line, the freed slot only while some is left`() {
        val cancelled = body("cancelledDetail")
        assertTrue(cancelled.contains("rules.freedSlot(row, now, zone)"))
        assertFalse(cancelled.contains("bookedEnd(row)?.let { it > now }"))
    }

    @Test
    fun `from the board's row a job not hers draws the not-taken page, and the head its own figure and pin`() {
        val rowDetail = body("rowDetail")
        assertTrue(rowDetail.contains("notTakenPage("))
        assertTrue(rowDetail.contains("rowDetailPin(move, failed)"))
        assertTrue(rowDetail.contains("detailMascot("))
        assertFalse(rowDetail.contains("R.drawable.mascot_waving"))
    }

    @Test
    fun `a job the server refused is never drawn from the board's row`() {
        assertTrue(
            Regex("""is OrderDetailUiState\.Error -> boardRow\?\.takeUnless \{ uiState\.refused \}\?\.let \{ rowDetail\(""")
                .containsMatchIn(body("OrderDetailContent")),
        )
    }

    /** A job opened from the board is drawn from its row, then loaded: one frame keeps its map, sheet and figure across both. */
    @Test
    fun `every state fills the one frame, which draws the one map`() {
        assertEquals(1, Regex("""JobDetailFrame\(""").findAll(detailScreen).count())
        assertEquals(1, Regex("""JobBandMap\(""").findAll(detailScreen).count())
        assertTrue(body("OrderDetailContent").contains("JobDetailFrame("))
    }

    @Test
    fun `from the board's row the pinned move knows the action in flight`() {
        val rowDetail = body("rowDetail")
        assertTrue(Regex("""OrderPrimaryAction\(\s*move = move, inFlight = inFlight,""").containsMatchIn(rowDetail))
        assertFalse(rowDetail.contains("inFlight = null"))
        assertEquals(2, Regex("""rowDetail\(it, lists, preferredOffer, inFlight, now,""").findAll(body("OrderDetailContent")).count())
    }

    @Test
    fun `while the start asked for from the set-off is sent, the start is shown under way in the set-off's place`() {
        val setOff = primaryAction.substringAfter("is PinnedMove.SetOff ->").substringBefore("PinnedMove.Start ->")
        val sending = setOff.substringBefore("} else {")
        val otherwise = setOff.substringAfter("} else {", missingDelimiterValue = "")
        assertTrue(
            Regex("""if \(inFlight == OrderAction\.Start\) \{\s*DetailSlide\(\s*idle = stringResource\(R\.string\.slide_to_start\),\s*busy = stringResource\(R\.string\.starting_order\),\s*isBusy = true,""")
                .containsMatchIn(sending),
        )
        listOf("R.string.swipe_to_notify_on_the_way", "R.string.late_already_there").forEach {
            assertFalse(sending.contains(it))
            assertTrue(otherwise.contains(it))
        }
    }

    @Test
    fun `the detail offers See N open jobs as the board does, only for an Available total above 0`() {
        val rule = "availableBadge(lists.open.hasLoaded, lists.open.total, lists.open.rows, lists.now)"
        assertTrue(body("OpenWeek").contains("openTotal = OrdersBoardRules.$rule,"))
        assertTrue(body("cancelledDetail").contains("rules.$rule?.let { total ->"))
        assertFalse(detailScreen.contains("lists.open.total ?:"))
    }

    /** Money is one string through the app's formatter: the head's pay pill draws the board's pay text whole. */
    @Test
    fun `the head says the pay as one string`() {
        val pill = bodyOf(jobHead, "PayPill")
        assertTrue("the pay pill draws the pay as given", pill.contains("text = pay,"))
        listOf("PayFigure(", "moneyText(", "figureParts(").forEach { assertFalse("the pay pill splits the figure through `$it`", pill.contains(it)) }
        assertTrue("the detail's pay is the board's formatter", detailScreen.contains("rules.payText(row, locale)"))
    }

    /** The 4-step progress and its counter come from the one rule, the counter on the status line as "Step n / 4". */
    @Test
    fun `the head counts the run as Step n of 4 through headProgress`() {
        val head = bodyOf(jobHead, "JobHead")
        assertTrue(head.contains("stringResource(R.string.tracker_step_counter, it.step, RUN_STEPS)"))
        assertTrue(Regex("""OrderTrackerBar\([\s\S]*?totalSteps = RUN_STEPS""").containsMatchIn(head))
        assertTrue(detailScreen.contains("headProgress("))
    }

    /** The panel collapses over a map that never moves: the map is the sheet's backdrop, outside every scroll. */
    @Test
    fun `the job detail is the core snap sheet over a still map, with the back button fixed over it`() {
        val file = source("JobDetailFrame.kt")
        val frame = bodyOf(file, "JobDetailFrame")
        assertTrue("the detail is no longer the core SnapSheet", frame.contains("SnapSheet("))
        assertTrue(
            "the panel does not rest expanded between its two stops",
            Regex("""rememberSnapSheetState\(initialAnchor = SnapAnchor\.Expanded, stops = PanelStops\)""").containsMatchIn(frame),
        )
        assertTrue("the stops are not collapsed and expanded", file.contains("private val PanelStops = setOf(SnapAnchor.MapFocus, SnapAnchor.Expanded)"))
        val backdrop = frame.substringAfter("backdrop = {").substringBefore("overlay = {")
        assertTrue("the map is not drawn in the backdrop", backdrop.contains("map?.let"))
        assertTrue("something scrolls the backdrop", !backdrop.contains("verticalScroll"))
        assertTrue("the back button left the fixed overlay", frame.substringAfter("overlay = {").substringBefore("PanelHandle").contains("FloatingCircleButton("))
        assertTrue("the map is still inside the page's scroll", frame.indexOf("verticalScroll") > frame.indexOf("overlay = {"))
        assertTrue("the scrolled bar is back", !frame.contains("barAlpha"))
    }

    private fun bodyOf(text: String, function: String): String {
        val from = text.indexOf("fun $function(").also { assertTrue("$function not found", it >= 0) }
        val next = Regex("""\n(@Composable|private fun|internal fun|fun |/\*\*)""").find(text, text.indexOf('\n', from))
        return text.substring(from, next?.range?.first ?: text.length)
    }

    private fun source(name: String): String =
        File(featureDir, name)
            .also { assertTrue("$name not found at ${it.absolutePath}", it.isFile) }
            .readText()
}
