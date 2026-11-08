package cz.cleansia.partner.features.orders

import androidx.compose.runtime.Composable
import androidx.compose.runtime.ReadOnlyComposable
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalConfiguration
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.semantics.LiveRegionMode
import androidx.compose.ui.semantics.liveRegion
import androidx.compose.ui.semantics.semantics

/**
 * What TalkBack hears on the Orders board and the take sheet (spec §6.6, §6.7, §7.4, §9.6): one sentence per
 * card in the visual order, action buttons that name their job, and live regions for state changes.
 */

/** Resolves a [BoardText] against the app's resources (and re-resolves on a locale change). */
@Composable
@ReadOnlyComposable
fun BoardText.text(): String {
    LocalConfiguration.current
    return resolve(LocalContext.current.resources)
}

/**
 * Clauses joined ", " inside and ". " between, each ended with a full stop once: "Tomorrow, 1 November,
 * 08:00–10:00, you earn 780 Kč. Praha 3, 2 h. Standard clean, 2 rooms."
 */
fun spokenSentence(vararg clauses: List<String?>): String =
    clauses.map { clause ->
        clause.filterNotNull().map { it.replace(OrdersBoardRules.SEP, ", ").replace(" · ", ", ").trim() }.filter(String::isNotEmpty).joinToString(", ")
    }
        .filter(String::isNotEmpty)
        .joinToString(" ") { if (it.endsWith(".")) it else "$it." }

/**
 * The Active hero as one sentence: "{status}. {day, date}, {window}. {street}, {area}, {customer}. {scope, team}.
 * you earn {pay}. {cash}. {why the slide is locked}, {when}." Absent clauses drop out.
 */
fun heroSentence(
    status: List<String>,
    day: String?,
    window: String?,
    place: List<String>,
    scope: List<String>,
    pay: String?,
    cash: String?,
    setOff: List<String>,
): String = spokenSentence(status, listOf(day, window), place, scope, listOf(pay), listOf(cash), setOff)

/** "Take, tomorrow, 08:00–10:00": a rotor of N cards never lists N identical "Take" buttons. */
fun actionLabel(action: String, day: String?, window: String?): String =
    listOfNotNull(action, day, window).filter(String::isNotBlank).joinToString(", ")

/** A polite live region: TalkBack reads the text when it appears or changes, without moving focus. */
fun Modifier.announcePolitely(): Modifier = semantics { liveRegion = LiveRegionMode.Polite }

/** An assertive live region, for a refusal the cleaner must hear at once. */
fun Modifier.announceAssertively(): Modifier = semantics { liveRegion = LiveRegionMode.Assertive }
