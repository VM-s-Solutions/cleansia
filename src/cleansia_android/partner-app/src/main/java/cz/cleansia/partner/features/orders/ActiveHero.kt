package cz.cleansia.partner.features.orders

import android.content.Context
import android.content.Intent
import android.net.Uri
import androidx.annotation.DrawableRes
import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.BoxWithConstraints
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.outlined.KeyboardArrowRight
import androidx.compose.material.icons.outlined.ChatBubbleOutline
import androidx.compose.material.icons.outlined.Group
import androidx.compose.material.icons.outlined.NearMe
import androidx.compose.material.icons.outlined.Payments
import androidx.compose.material.icons.outlined.Phone
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.layout.Layout
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.clearAndSetSemantics
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.AnnotatedString
import androidx.compose.ui.text.rememberTextMeasurer
import androidx.compose.ui.unit.Constraints
import androidx.compose.ui.unit.dp
import cz.cleansia.core.ui.components.CleansiaButtonSize
import cz.cleansia.core.ui.components.CleansiaOutlinedButton
import cz.cleansia.core.ui.components.CleansiaTextLink
import cz.cleansia.core.ui.components.LockedSwipeToConfirm
import cz.cleansia.core.ui.components.MascotPuck
import cz.cleansia.core.ui.components.SwipeToConfirmButton
import cz.cleansia.core.ui.theme.Spacing
import cz.cleansia.core.ui.theme.primaryText
import cz.cleansia.partner.R
import cz.cleansia.partner.api.model.OrderListItem
import java.time.Instant
import java.time.ZoneId

/**
 * The Active hero: her own job picked by status first, drawn as the job detail's head on a card with the state's figure
 * seated beside its number; then the scope, the cash, the contacts and the one control. There is no Complete here.
 */
@Composable
fun ActiveHero(
    row: OrderListItem,
    now: Instant,
    progress: HeroProgress?,
    isBusy: Boolean,
    onOpen: () -> Unit,
    onNotifyOnTheWay: () -> Unit,
    onStart: () -> Unit,
    modifier: Modifier = Modifier,
) {
    val rules = OrdersBoardRules
    val locale = appLocale()
    val zone = ZoneId.systemDefault()
    val status = rules.status(row)
    val start = rules.start(row)
    val minutes = rules.minutes(row)
    val window = rules.window(start, minutes, zone, locale)
    val own = progress?.takeIf { it.orderId == row.id }
    val crew = own?.crew ?: ((row.requiredEmployees ?: 1) > 1)
    val statusItems = start?.let {
        rules.runStatus(
            status = status,
            start = it,
            minutes = minutes,
            now = now,
            zone = zone,
            locale = locale,
            crew = crew,
            myMarkAt = own?.myMarkAt,
            firstAfterPhotoAt = own?.firstAfterPhotoAt,
        ).map { item -> item.text() }
    }.orEmpty()
    val step = rules.runStep(status, crew, own?.myMarkAt, own?.firstAfterPhotoAt)
    val street = rules.streetLine(row.customerAddress)
    val sub = listOfNotNull(rules.areaLine(row), row.customerName?.trim()?.takeIf { it.isNotEmpty() })
    val scope = rules.scopeItems(row, locale.language).map { it.text() } + listOfNotNull(rules.heroTeam(row)?.text())
    val pay = rules.payText(row, locale)
    val caption = stringResource(if ((row.requiredEmployees ?: 1) > 1) R.string.orders_card_your_share else R.string.work_contract_reward)
    val cash = rules.cashFlag(row, locale)?.text()
    val control = rules.heroControl(row, now)
    val opensAt = (control as? HeroControl.SetOff)?.takeIf { !it.enabled }?.opensAt
    val reason = opensAt?.let { rules.tie(stringResource(R.string.active_set_off_from, rules.timeText(it, zone, locale))) }
    val setOffWhen = opensAt?.let {
        rules.setOffWhen(it, now, zone, locale, android.text.format.DateFormat.getBestDateTimePattern(locale, "dMMM")).text()
    }
    val sentence = heroSentence(
        status = statusItems,
        day = start?.let { dayAndDate(it, now) },
        window = window,
        place = listOfNotNull(street) + sub,
        scope = scope,
        pay = pay?.let { "${stringResource(R.string.you_earn)} $it" },
        cash = cash,
        setOff = listOfNotNull(reason, setOffWhen),
    )
    val figure = heroMascot(status).takeIf { LocalDensity.current.fontScale < 1.3f }

    Surface(
        onClick = onOpen,
        modifier = modifier
            .fillMaxWidth()
            .semantics { contentDescription = sentence },
        shape = MaterialTheme.shapes.medium,
        color = MaterialTheme.colorScheme.surface,
        border = BorderStroke(1.dp, MaterialTheme.colorScheme.outlineVariant),
    ) {
        Box {
            Column(Modifier.padding(Spacing.M)) {
                JobHead(
                    number = row.displayOrderNumber?.takeIf { it.isNotBlank() },
                    status = status,
                    day = calendarDay(start, zone),
                    window = window,
                    statusItems = statusItems,
                    progress = start?.let { headProgress(step, it, minutes, now) },
                    street = street,
                    sub = sub,
                    pay = pay,
                    caption = caption.takeIf { pay != null },
                    clearance = if (figure != null) PuckClearance(width = HeroClearWidth, foot = HeroPuckTop + HeroPuckSize - Spacing.M) else PuckClearance.None,
                    modifier = Modifier.clearAndSetSemantics {},
                ) {
                    if (start != null) OwnDayLine(row, row.orderStatus.toOrderStatus(), start, now)
                }
                ScopeAndLink(scope = scope, showLink = status != 4, onOpen = onOpen, modifier = Modifier.padding(top = Spacing.S))
                if (cash != null) FlagFlow(Modifier.padding(top = 10.dp).clearAndSetSemantics {}) { OrderFlag(cash, FlagTone.Warn, Icons.Outlined.Payments) }
                ContactPills(
                    phone = row.customerPhone,
                    navigateTo = row.customerAddress?.takeIf { it.isNotBlank() }?.let { OrderLocation.Precise(it, row.customerAddressLatitude, row.customerAddressLongitude) },
                    modifier = Modifier.padding(top = Spacing.S),
                )
                HeroControlArea(
                    control = control,
                    reason = reason,
                    isBusy = isBusy,
                    onNotifyOnTheWay = onNotifyOnTheWay,
                    onStart = onStart,
                    onOpen = onOpen,
                )
            }
            figure?.let { art ->
                MascotPuck(
                    painter = painterResource(art),
                    scale = puckScale(art),
                    size = HeroPuckSize,
                    modifier = Modifier
                        .align(Alignment.TopEnd)
                        .padding(top = HeroPuckTop, end = Spacing.M),
                )
            }
        }
    }
}

/** The hero's figure by where her job stands: on the way ready, at work with spray and cloth, else waving. */
@DrawableRes
private fun heroMascot(status: Int?): Int = when (status) {
    4 -> R.drawable.mascot_spray_and_cloth
    3 -> R.drawable.mascot_ready
    else -> R.drawable.mascot_waving
}

/** The figure seated in the head beside the number and the calendar line; those two rows keep its width clear. */
private val HeroPuckSize = 64.dp
private val HeroPuckTop = 5.dp
private val HeroClearWidth = HeroPuckSize + Spacing.M + Spacing.XS

/** The scope, with *Job details ›* at its end on its first line; the link drops under it when the scope would get under 170 dp. */
@Composable
private fun ScopeAndLink(scope: List<String>, showLink: Boolean, onOpen: () -> Unit, modifier: Modifier = Modifier) {
    val flow: @Composable () -> Unit = {
        DotFlow(
            items = scope.map { AnnotatedString(it) },
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
            modifier = Modifier.clearAndSetSemantics {},
            tieLast = true,
        )
    }
    if (!showLink) {
        Box(modifier) { flow() }
        return
    }
    LinkBeside(gap = Spacing.S, startMin = 170.dp, modifier = modifier.fillMaxWidth(), start = flow) {
        CleansiaTextLink(text = stringResource(R.string.active_hero_details), onClick = onOpen, trailingIcon = Icons.AutoMirrored.Outlined.KeyboardArrowRight)
    }
}

@Composable
private fun HeroControlArea(
    control: HeroControl?,
    reason: String?,
    isBusy: Boolean,
    onNotifyOnTheWay: () -> Unit,
    onStart: () -> Unit,
    onOpen: () -> Unit,
) {
    when (control) {
        is HeroControl.SetOff -> if (reason != null) {
            LockedSwipeToConfirm(
                reason = reason,
                modifier = Modifier
                    .padding(top = Spacing.S)
                    .slideSemantics(label = reason, busyLabel = reason, enabled = false, isBusy = false, onCommit = {}, disabledReason = reason),
            )
        } else {
            HeroSlide(stringResource(R.string.swipe_to_notify_on_the_way), stringResource(R.string.customer_notified_on_the_way), isBusy, onNotifyOnTheWay)
        }
        HeroControl.Start -> HeroSlide(stringResource(R.string.slide_to_start), stringResource(R.string.starting_order), isBusy, onStart)
        HeroControl.OpenJob -> CleansiaOutlinedButton(
            text = stringResource(R.string.orders_open_job),
            onClick = onOpen,
            size = CleansiaButtonSize.Medium,
            contentColor = primaryText(),
            borderColor = MaterialTheme.colorScheme.primary,
            modifier = Modifier.padding(top = Spacing.S),
        )
        null -> Unit
    }
}

@Composable
private fun HeroSlide(idle: String, busy: String, isBusy: Boolean, onCommit: () -> Unit) {
    SwipeToConfirmButton(
        text = idle,
        onConfirmed = onCommit,
        modifier = Modifier
            .padding(top = Spacing.S)
            .slideSemantics(label = idle, busyLabel = busy, enabled = true, isBusy = isBusy, onCommit = onCommit),
        isBusy = isBusy,
        busyText = busy,
        maxLines = 2,
        thumbLanes = true,
    )
}

/**
 * Navigate · Call · SMS as equal chips; when a label does not fit (measured by its parts): drop the icons,
 * tighten the side padding, put Navigate on its own row over Call · SMS, then one column.
 */
@Composable
internal fun ContactPills(phone: String?, navigateTo: OrderLocation.Precise?, modifier: Modifier = Modifier) {
    val context = LocalContext.current
    val phone = phone?.trim()?.takeIf { it.isNotEmpty() }
    val pills = listOfNotNull(
        navigateTo?.let { target ->
            Triple(stringResource(R.string.action_navigate), Icons.Outlined.NearMe) {
                launchNavigation(context, target.latitude, target.longitude, target.line)
            }
        },
        phone?.let { Triple(stringResource(R.string.action_call), Icons.Outlined.Phone) { launch(context, Intent(Intent.ACTION_DIAL, Uri.parse("tel:${it.replace(" ", "")}"))) } },
        phone?.let { Triple(stringResource(R.string.action_sms), Icons.Outlined.ChatBubbleOutline) { launch(context, Intent(Intent.ACTION_SENDTO, Uri.parse("smsto:${it.replace(" ", "")}"))) } },
    )
    val labelStyle = tonalChipLabel()
    val measurer = rememberTextMeasurer()
    val density = LocalDensity.current
    val large = density.fontScale >= 1.3f
    BoxWithConstraints(modifier.fillMaxWidth()) {
        val gap = with(density) { Spacing.XS.roundToPx() }
        val iconRoom = with(density) { (18.dp + 6.dp).roundToPx() }
        val widths = pills.map { measurer.measure(it.first, labelStyle, softWrap = false, maxLines = 1).size.width }
        val need = { i: Int, icons: Boolean, pad: Int -> widths[i] + with(density) { (pad * 2).dp.roundToPx() } + if (icons) iconRoom else 0 }
        val fits = { indices: List<Int>, slot: Int, icons: Boolean, pad: Int -> indices.all { need(it, icons, pad) <= slot } }
        val width = constraints.maxWidth
        val all = pills.indices.toList()
        val slot = (width - gap * (pills.size - 1)) / pills.size.coerceAtLeast(1)
        val row: Triple<Boolean, Int, Boolean>? = listOf(true to 10, false to 10, false to 4)
            .firstOrNull { (icons, pad) -> !large && fits(all, slot, icons, pad) }
            ?.let { Triple(it.first, it.second, false) }
        when {
            row != null -> PillGrid(pills, columns = pills.size, icons = row.first, pad = row.second)
            !large && pills.size == 3 && fits(listOf(1, 2), (width - gap) / 2, false, 4) -> {
                val navIcons = need(0, true, 10) <= width
                val pairSlot = (width - gap) / 2
                val pairIcons = fits(listOf(1, 2), pairSlot, true, 10)
                Column(verticalArrangement = Arrangement.spacedBy(Spacing.XS)) {
                    PillGrid(pills.take(1), columns = 1, icons = navIcons, pad = 10)
                    PillGrid(pills.drop(1), columns = 2, icons = pairIcons, pad = if (pairIcons || fits(listOf(1, 2), pairSlot, false, 10)) 10 else 4)
                }
            }
            else -> PillGrid(pills, columns = 1, icons = true, pad = 10)
        }
    }
}

@Composable
private fun PillGrid(pills: List<Triple<String, ImageVector, () -> Unit>>, columns: Int, icons: Boolean, pad: Int) {
    Layout(
        content = {
            pills.forEach { (label, icon, onClick) ->
                TonalChip(label = label, icon = icon.takeIf { icons }, onClick = onClick, padding = pad.dp)
            }
        },
    ) { measurables, constraints ->
        val gap = Spacing.XS.roundToPx()
        val width = constraints.maxWidth
        val slot = (width - gap * (columns - 1)) / columns
        val placeables = measurables.map { it.measure(Constraints.fixedWidth(slot.coerceAtLeast(0))) }
        val rows = placeables.chunked(columns)
        val height = rows.sumOf { r -> r.maxOf { it.height } } + gap * (rows.size - 1).coerceAtLeast(0)
        layout(width, height) {
            var y = 0
            rows.forEach { r ->
                r.forEachIndexed { i, p -> p.place(i * (slot + gap), y) }
                y += r.maxOf { it.height } + gap
            }
        }
    }
}

/**
 * Her own job in a list: row leaf │ window / street · service ‖ pay and caption. Active's *Later* adds the crew and
 * cash flags under the street; *Your next jobs* and the detail's lists leave them out.
 */
@Composable
fun OwnJobRow(
    row: OrderListItem,
    now: Instant,
    onClick: () -> Unit,
    modifier: Modifier = Modifier,
    flags: Boolean = true,
) {
    val rules = OrdersBoardRules
    val locale = appLocale()
    val zone = ZoneId.systemDefault()
    val start = rules.start(row)
    val window = rules.window(start, rules.minutes(row), zone, locale)
    val pay = rules.payText(row, locale)
    val team = (row.requiredEmployees ?: 1) > 1
    val caption = stringResource(R.string.orders_card_your_share).takeIf { team && pay != null }
    val lines = listOfNotNull(rules.streetLine(row.customerAddress), rules.scopeLead(row, locale.language))
    val cash = rules.cashFlag(row, locale)?.text()
    val teamText = rules.heroTeam(row)?.text()
    val sentence = spokenSentence(
        listOf(start?.let { dayAndDate(it, now) }, window),
        lines,
        listOf(teamText.takeIf { flags }),
        listOf(pay?.let { "${caption ?: stringResource(R.string.you_earn)} $it" }),
        listOf(cash.takeIf { flags }),
    )
    val large = LocalDensity.current.fontScale >= 1.3f
    val payBlock: @Composable (Alignment.Horizontal) -> Unit = { align ->
        if (pay != null) {
            Column(horizontalAlignment = align) {
                Text(text = pay, style = MaterialTheme.typography.titleMedium.copy(fontFeatureSettings = "tnum"), color = MaterialTheme.colorScheme.onSurface, softWrap = false)
                caption?.let { Text(text = it, style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant) }
            }
        }
    }
    Row(
        modifier = modifier
            .fillMaxWidth()
            .clickable(onClick = onClick)
            .clearAndSetSemantics { contentDescription = sentence }
            .padding(horizontal = Spacing.M, vertical = Spacing.S),
        horizontalArrangement = Arrangement.spacedBy(Spacing.S),
    ) {
        OrdersDateTile(start = start, size = TileSize.Row, now = now)
        Column(Modifier.weight(1f)) {
            Text(text = window ?: "—", style = MaterialTheme.typography.bodyLarge.copy(fontFeatureSettings = "tnum"), color = MaterialTheme.colorScheme.onSurface)
            DotFlow(items = lines.map { AnnotatedString(rules.tie(it)) }, style = MaterialTheme.typography.bodyMedium, color = MaterialTheme.colorScheme.onSurfaceVariant, modifier = Modifier.padding(top = 2.dp))
            if (flags && (teamText != null || cash != null)) {
                FlagFlow(Modifier.padding(top = Spacing.XS)) {
                    teamText?.let { OrderFlag(it, FlagTone.Neutral, Icons.Outlined.Group) }
                    cash?.let { OrderFlag(it, FlagTone.Warn, Icons.Outlined.Payments) }
                }
            }
            if (large) Box(Modifier.padding(top = 6.dp)) { payBlock(Alignment.Start) }
        }
        if (!large) payBlock(Alignment.End)
    }
}

private fun launch(context: Context, intent: Intent) {
    runCatching { context.startActivity(intent.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)) }
}

/** Coordinates when the job has them, else the address text. */
internal fun launchNavigation(context: Context, latitude: Double?, longitude: Double?, address: String) {
    val uri = if (latitude != null && longitude != null) {
        Uri.parse("geo:$latitude,$longitude?q=$latitude,$longitude(${Uri.encode(address)})")
    } else {
        Uri.parse("geo:0,0?q=${Uri.encode(address)}")
    }
    launch(context, Intent(Intent.ACTION_VIEW, uri))
}
