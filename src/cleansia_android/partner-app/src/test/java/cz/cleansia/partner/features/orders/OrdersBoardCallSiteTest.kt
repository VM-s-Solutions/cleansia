package cz.cleansia.partner.features.orders

import java.io.File
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * The board's composables must read the tested rules, not their own copy of them: each pin names the call that
 * carries the rule into the screen. Kotlin source read off disk, so meaningful only under `--rerun`.
 */
class OrdersBoardCallSiteTest {

    private val featureDir: File = sequenceOf(File("."), File("partner-app"), File("src/cleansia_android/partner-app"))
        .map { File(it, "src/main/java/cz/cleansia/partner/features/orders") }
        .firstOrNull { it.isDirectory }
        ?: error("orders feature dir not found from working dir ${File(".").absolutePath}")

    private val hero = source("ActiveHero.kt")
    private val list = source("OrdersListScreen.kt")
    private val card = source("AvailableJobCard.kt")
    private val primaryAction = source("OrderPrimaryAction.kt")
    private val sheet = source("WorkContractSheet.kt")

    private fun body(text: String, function: String): String {
        val from = text.indexOf("fun $function(").also { assertTrue("$function not found", it >= 0) }
        val next = Regex("""\n(@Composable|private fun|internal fun|fun |/\*\*)""").find(text, text.indexOf('\n', from))
        return text.substring(from, next?.range?.first ?: text.length)
    }

    @Test
    fun `the hero's locked slide says when as the detail's does`() {
        assertTrue("the hero's second line comes from rules.setOffWhen", hero.contains("rules.setOffWhen("))
        assertFalse("the hero no longer reuses the status line as its countdown", hero.contains("countdown = statusItems.getOrNull(1)"))
        assertFalse("the detail's locked slide says its reason alone: the head's status line is the countdown", primaryAction.contains("OrdersBoardRules.setOffWhen("))
    }

    @Test
    fun `the hero labels the set-off band whenever the slide is locked`() {
        assertFalse(hero.contains("reason != null && day == today"))
    }

    @Test
    fun `the hero's sentence is built by heroSentence and says you earn`() {
        assertTrue(hero.contains("heroSentence("))
        assertTrue(hero.contains("R.string.you_earn"))
    }

    @Test
    fun `an open job's row opens the job and carries no Take`() {
        val row = body(card, "AvailableJobRow")
        assertFalse(row.contains("onTake"))
        assertFalse(row.contains("PayTile("))
        assertTrue(row.contains("onClick = onOpen"))
        assertTrue(row.contains("role = Role.Button"))
        val day = body(list, "DayPanel")
        assertTrue(day.contains("onOpen = { row.id?.let(onOrderClick) }"))
        assertFalse(day.contains("onTake"))
        val alsoOpen = body(source("OrderDetailScreen.kt"), "OpenRowsSection")
        assertTrue(alsoOpen.contains("onOpen = { row.id?.let(lists.onOpenOrder) }"))
        assertFalse(alsoOpen.contains("onTake"))
    }

    @Test
    fun `an open job's row speaks your share on a team job`() {
        assertTrue(body(card, "AvailableJobRow").contains("if (team) R.string.orders_card_your_share else R.string.you_earn"))
    }

    @Test
    fun `an action names the day word, not the date`() {
        assertFalse(card.contains("actionLabel(action, dayText, window)"))
        assertFalse(card.contains("actionLabel(stringResource(R.string.offer_decline), dayText, window)"))
        assertEquals(2, Regex("""dayWord\?\.lowercase\(locale\)""").findAll(card).count())
    }

    @Test
    fun `Available focuses the taken row's place by its index among the cards`() {
        assertTrue(list.contains("cards.indexOf(row) == "))
        assertFalse(list.contains("firstIndex + index"))
    }

    @Test
    fun `Available chooses its short panel from the rows on show`() {
        assertTrue(body(list, "AvailablePane").contains("availableShort(pane, cards, reservedShown = reserved.isNotEmpty())"))
    }

    @Test
    fun `Active's soonest open job skips one that overlaps hers`() {
        val active = body(list, "ActivePane")
        assertTrue(active.contains("rules.soonestOpen("))
        assertTrue(active.contains("clash = null,"))
    }

    @Test
    fun `See N open jobs is the Available badge`() {
        val active = body(list, "ActivePane")
        assertTrue(active.contains("rules.availableBadge(available.hasLoaded, available.total, available.rows, now)"))
        assertFalse(active.contains("available.total ?: open.size"))
    }

    @Test
    fun `the Available tab's badge is the same number, not the server's total`() {
        val content = body(list, "OrdersListContent")
        assertTrue(content.contains("OrdersTab.Available to OrdersBoardRules.availableBadge(available.hasLoaded, available.total, available.rows, now),"))
        assertFalse(content.contains("OrdersTab.Available to uiState.pane(OrdersTab.Available).total"))
    }

    @Test
    fun `the week strip is told whether Available has answered`() {
        assertEquals(3, Regex("""rules\.weekCells\([^\n]*, loaded = available\.hasLoaded\)""").findAll(body(list, "ActivePane")).count())
    }

    @Test
    fun `the week strip prints no count under a day until Available has answered`() {
        assertTrue(body(list, "WeekStrip").contains("text = cell.open?.toString().orEmpty(),"))
    }

    @Test
    fun `the reserved row's scope sits beside its tile unless full width is shorter`() {
        assertTrue(body(card, "ReservedJobRow").contains("JobRowLayout("))
        val layout = body(card, "JobRowLayout")
        assertTrue(layout.contains("OrdersBoardRules.scopeFullWidth(besideHeight, wideHeight, measured = true)"))
        assertTrue(layout.contains("if (wide) scopeWide?.place(0, maxOf(headP.height, tileP.height) + line)"))
    }

    @Test
    fun `her block, an open slot and a clash are all drawn through the least-width rule`() {
        val line = source("OrdersDayLine.kt")
        assertTrue(line.contains("OrdersBoardRules.markBounds(x(from) + inset, x(to) - inset, size.width, MIN_MARK.toPx())"))
        assertEquals(1, Regex("""val bounds = mark\(slot\.span\.start, slot\.span\.end\)""").findAll(line).count())
        assertEquals(1, Regex("""dashedOutline\(bounds\.first, top, bounds\.second, height, openEdge\)""").findAll(line).count())
        assertEquals(2, Regex("""block\(mark\(it\.start, it\.end\)""").findAll(line).count())
    }

    @Test
    fun `every scope line ties its last item to the one before it`() {
        val scopeFlows = featureDir.listFiles { f -> f.extension == "kt" }!!.sortedBy { it.name }.flatMap { file ->
            dotFlowCalls(file.readText()).filter { "scope" in it }.map { "${file.name}: $it" }
        }
        assertEquals("the board's open and reserved rows', the hero's and the take sheet's scope lines; the detail's scope is Property & scope's rows", 4, scopeFlows.size)
        scopeFlows.forEach { assertTrue(it, "tieLast = true" in it) }
        assertTrue("the hero is the detail's head, whose sub is tied", hero.contains("JobHead(") && dotFlowCalls(body(source("JobHead.kt"), "JobHead")).any { "sub.map" in it && "tieLast = true" in it })
        assertTrue("the checklist's lead row is the scope", dotFlowCalls(body(source("CleaningChecklist.kt"), "CleaningChecklist")).single().contains("tieLast = true"))
        assertTrue(body(card, "DotFlow").contains("OrdersBoardRules.flowLines(placeables.map { it.width }, slotPx, width, tieLast)"))
    }

    @Test
    fun `her own job's street and service never leave a word alone on their last line`() {
        assertTrue(body(hero, "OwnJobRow").contains("DotFlow(items = lines.map { AnnotatedString(rules.tie(it)) },"))
    }

    @Test
    fun `the sort's words and chevron are centred together in their touch target, so its clip never cuts a letter`() {
        assertTrue(
            Regex("""\{\s*Row \{\s*Text\(text = stringResource\(currentSort\.labelRes\),[^\n]*Modifier\.alignBy\(FirstBaseline\)\)\s*Icon\(""")
                .containsMatchIn(body(list, "SortControl")),
        )
    }

    @Test
    fun `See N open jobs and the payouts link wrap to a second line rather than cut their words`() {
        assertTrue(
            Regex("""BrandOutlinedButton\(\s*text = OrdersBoardRules\.tie\(pluralStringResource\(R\.plurals\.active_see_open, openTotal, openTotal\)\),\s*onClick = onShowAvailable,\s*maxLines = 2,""")
                .containsMatchIn(list),
        )
        assertTrue(body(list, "BrandOutlinedButton").contains("maxLines = maxLines,"))
        assertTrue(
            Regex(
                """CleansiaTextLink\(\s*text = stringResource\(R\.string\.history_payouts_link, stringResource\(R\.string\.earnings_title\)\),\s*""" +
                    """onClick = onOpenEarnings,\s*trailingIcon = chevron,\s*maxLines = 2,""",
            ).containsMatchIn(list),
        )
    }

    @Test
    fun `the day line's marks are laid out together, the now mark giving way to another it would touch`() {
        assertTrue(source("OrdersDayLine.kt").contains("OrdersBoardRules.dayLineLabelLefts("))
    }

    private fun dotFlowCalls(text: String): List<String> = Regex("""(?<!fun )DotFlow\(""").findAll(text).map { match ->
        var depth = 0
        val end = (match.range.last until text.length).first { i ->
            when (text[i]) {
                '(' -> depth++
                ')' -> depth--
            }
            depth == 0
        }
        text.substring(match.range.first, end + 1)
    }.toList()

    @Test
    fun `every figure on the board is one string, as the app's money formatter writes it`() {
        featureDir.listFiles { f -> f.extension == "kt" }!!.sortedBy { it.name }.forEach { file ->
            val text = file.readText()
            listOf("moneyText(", "figureParts(", "totalParts(", "PayFigure(").forEach { split ->
                assertFalse("${file.name} still splits a figure through `$split`", text.contains(split))
            }
        }
        assertTrue(body(list, "HistoryTotal").contains("OrdersBoardRules.historyTotalText("))
    }

    @Test
    fun `amber is for money only, so the dirt flag is neutral and every warning-toned flag is a cash flag`() {
        val warn = featureDir.listFiles { f -> f.extension == "kt" }!!.sortedBy { it.name }.flatMap { file ->
            file.readLines().filter { "FlagTone.Warn" in it && "FlagTone.Warn ->" !in it }.map { "${file.name}: ${it.trim()}" }
        }
        assertTrue(warn.isNotEmpty())
        warn.forEach { assertTrue(it, "Icons.Outlined.Payments" in it) }
        assertTrue(body(card, "rowFlags").contains("OrderFlag(text, FlagTone.Neutral, Icons.Outlined.AutoAwesome)"))
        assertTrue(source("ScopeCard.kt").contains("OrderFlag(stringResource(it), FlagTone.Neutral, Icons.Outlined.AutoAwesome)"))
    }

    @Test
    fun `Active's error draws the compact notice over the cap, as Available's empty state`() {
        val active = body(list, "ActivePane")
        assertTrue(active.contains("uiState.cashHidden?.let { item(key = \"cash-notice\") { CashNotice(it) } }"))
        assertTrue(active.contains("if (uiState.cashHidden == null) uiState.cashHeld?.let { item(key = \"cash\") { CashPanel(it) } }"))
    }

    @Test
    fun `History says a failed refresh over held rows`() {
        assertTrue(body(list, "HistoryPane").contains("StaleStrip(loadedAt = pane.loadedAt, now = now, onRetry = onRetry)"))
    }

    @Test
    fun `the take sheet's Close and swipe both wait while the take is in flight`() {
        assertEquals(2, Regex("""closeWhenIdle\(submitting, onDismiss\)""").findAll(sheet).count())
        assertTrue(sheet.contains("enabled = !submitting"))
    }

    private fun source(name: String): String =
        File(featureDir, name).also { assertTrue("$name not found at ${it.absolutePath}", it.isFile) }.readText()
}
