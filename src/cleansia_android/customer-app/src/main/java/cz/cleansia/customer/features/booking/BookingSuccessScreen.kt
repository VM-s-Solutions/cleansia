package cz.cleansia.customer.features.booking

import android.os.Build
import androidx.compose.animation.Crossfade
import androidx.compose.animation.core.Animatable
import androidx.compose.animation.core.CubicBezierEasing
import androidx.compose.animation.core.tween
import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.Image
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.BoxWithConstraints
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.FlowRow
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.WindowInsets
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.navigationBars
import androidx.compose.foundation.layout.navigationBarsPadding
import androidx.compose.foundation.layout.offset
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.layout.wrapContentSize
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.text.selection.SelectionContainer
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.Person
import androidx.compose.material.icons.rounded.Check
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.drawBehind
import androidx.compose.ui.geometry.CornerRadius
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.geometry.Rect
import androidx.compose.ui.geometry.Size
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.PathEffect
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.graphics.graphicsLayer
import androidx.compose.ui.graphics.luminance
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.layout.onGloballyPositioned
import androidx.compose.ui.layout.onSizeChanged
import androidx.compose.ui.layout.positionInRoot
import androidx.compose.ui.platform.LocalConfiguration
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.res.pluralStringResource
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.semantics.clearAndSetSemantics
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.heading
import androidx.compose.ui.semantics.paneTitle
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.AnnotatedString
import androidx.compose.ui.text.SpanStyle
import androidx.compose.ui.text.TextLayoutResult
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.buildAnnotatedString
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.rememberTextMeasurer
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.TextUnit
import androidx.compose.ui.unit.dp
import androidx.compose.ui.zIndex
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.compose.LifecycleEventEffect
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import cz.cleansia.core.ui.components.CleansiaButtonSize
import cz.cleansia.core.ui.components.CleansiaPrimaryButton
import cz.cleansia.core.ui.components.CleansiaTextButton
import cz.cleansia.core.ui.components.MascotAnimation
import cz.cleansia.core.ui.theme.CleansiaShapes
import cz.cleansia.core.ui.theme.Spacing
import cz.cleansia.core.ui.theme.primaryText
import cz.cleansia.core.ui.theme.primaryTextOnContainer
import cz.cleansia.customer.R
import cz.cleansia.customer.core.orders.OrderAddressDto
import cz.cleansia.customer.core.orders.OrderCurrencyDetailDto
import cz.cleansia.customer.core.orders.OrderDetailDto
import cz.cleansia.customer.core.user.CodeDto
import cz.cleansia.customer.features.orders.roomsAndBathrooms
import cz.cleansia.customer.ui.theme.CleansiaTheme
import java.time.Instant
import java.util.Locale
import kotlin.math.abs
import kotlin.math.floor
import kotlinx.coroutines.delay

private val HomeButtonPadding = 22.dp
private val GrabCompletion = 8.dp
private val ArrivalEasing = CubicBezierEasing(0.22f, 1f, 0.36f, 1f)

/** The height a confirmation [height] tall leaves its header and ticket, above its actions and the navigation bar. */
internal fun confirmationRoom(height: Dp, navInset: Dp): Dp =
    height - GrabCompletion - 12.dp - CleansiaButtonSize.Large.minHeight - 12.dp - navInset

/**
 * The hero's figure height in a confirmation [height] tall, before it is laid out: its tier's minimum on a compact
 * one, whose ticket takes all the room, and its cap on every other.
 */
/** The date tile is a square: the app's 64 day card, grown at accessibility sizes so the day still fits. */
internal fun confirmationLeafSide(size: ConfirmationSize): Dp = if (size == ConfirmationSize.Ax) 92.dp else 64.dp

internal fun expectedFigureHeight(height: Dp, navInset: Dp, fontScale: Float): Dp {
    val size = confirmationSize(confirmationRoom(height, navInset).value.toDouble(), fontScale)
    val tiers = size.tiers
    return (if (size == ConfirmationSize.Regular || size == ConfirmationSize.Tall) tiers.hdCap else tiers.hdMin).dp
}

@Composable
fun BookingSuccessScreen(
    success: BookingSuccess,
    onViewOrders: () -> Unit,
    onGoHome: () -> Unit,
    modifier: Modifier = Modifier,
    arrival: Boolean = false,
    revealed: Boolean = true,
    mascotHeld: Boolean = false,
    onMascotPlaced: (heroCanvas: Rect?) -> Unit = {},
    viewModel: BookingSuccessViewModel = hiltViewModel(key = "booking-success:${success.orderId}"),
) {
    LaunchedEffect(success.orderId) { viewModel.start(success.orderId) }
    val uiState by viewModel.state.collectAsStateWithLifecycle()
    BookingSuccessContent(
        success, uiState, onViewOrders, onGoHome, modifier, arrival, revealed, mascotHeld, onMascotPlaced,
        onRetry = viewModel::retry,
    )
}

@Composable
internal fun bookingSuccessCanvas(): Color =
    if (MaterialTheme.colorScheme.surface.luminance() < 0.5f) {
        MaterialTheme.colorScheme.background
    } else MaterialTheme.colorScheme.surfaceVariant

/**
 * The confirmation as one ticket, filled from [success] until the server's order lands. Its spare height
 * is decided once, on the first layout (under the busy overlay when it arrives), and [distribute]d in tiers
 * so it never collects into one band. [mascotHeld] keeps the header mascot hidden while the sheet carries it
 * in from the overlay; [onMascotPlaced] reports its canvas in the root once the layout is decided.
 */
@Composable
internal fun BookingSuccessContent(
    success: BookingSuccess,
    uiState: BookingSuccessUiState,
    onViewOrders: () -> Unit,
    onGoHome: () -> Unit,
    modifier: Modifier = Modifier,
    arrival: Boolean = false,
    revealed: Boolean = true,
    mascotHeld: Boolean = false,
    onMascotPlaced: (heroCanvas: Rect?) -> Unit = {},
    onRetry: () -> Unit = {},
) {
    val order = (uiState as? BookingSuccessUiState.Loaded)?.order
    val configuration = LocalConfiguration.current
    val locale = configuration.locales[0]
    val density = LocalDensity.current
    val title = stringResource(R.string.booking_success_t1_title)

    var settled by remember { mutableStateOf(!arrival) }
    LaunchedEffect(revealed) {
        if (revealed && !settled) {
            delay(700)
            settled = true
        }
    }

    val motion = remember { android.animation.ValueAnimator.areAnimatorsEnabled() }
    var welcomePlayed by rememberSaveable(success.orderId) { mutableStateOf(false) }
    LifecycleEventEffect(Lifecycle.Event.ON_PAUSE) { welcomePlayed = true }
    val mayPlay = arrival && motion && Build.VERSION.SDK_INT >= 28 && !welcomePlayed
    val mascotIn = remember { Animatable(if (arrival) 0f else 1f) }
    var mascotStarted by remember { mutableStateOf(false) }
    var mascotCarried by remember { mutableStateOf(false) }
    LaunchedEffect(revealed, mascotHeld) {
        if (!revealed || mascotStarted) return@LaunchedEffect
        if (mascotHeld) {
            mascotCarried = true
            return@LaunchedEffect
        }
        mascotStarted = true
        if (mascotCarried || !motion) mascotIn.snapTo(1f) else mascotIn.animateTo(1f, tween(240, easing = ArrivalEasing))
    }
    LaunchedEffect(mascotStarted) {
        if (mascotStarted && !welcomePlayed) {
            delay(2_100)
            welcomePlayed = true
        }
    }

    BoxWithConstraints(
        modifier
            .fillMaxSize()
            .background(bookingSuccessCanvas())
            .semantics { if (settled) paneTitle = title },
    ) {
        val navInset = with(density) { WindowInsets.navigationBars.getBottom(this).toDp() }
        val room = confirmationRoom(maxHeight, navInset)
        val size = confirmationSize(room.value.toDouble(), density.fontScale)
        val ax = size == ConfirmationSize.Ax
        val tiers = size.tiers
        val scroll = rememberScrollState()

        val pMin = with(density) { tiers.pMin.dp.roundToPx() }
        val hdMin = with(density) { tiers.hdMin.dp.roundToPx() }
        var viewportPx by remember { mutableIntStateOf(0) }
        var naturalPx by remember { mutableIntStateOf(0) }
        var spare by remember(maxWidth, maxHeight, density.fontScale) { mutableStateOf<Spare?>(null) }
        fun decide() {
            if (spare != null || viewportPx <= 0 || naturalPx <= 0) return
            if (mascotStarted) welcomePlayed = true
            if (ax) {
                spare = Spare(hdMin.toDouble(), pMin.toDouble(), 0.0)
                return
            }
            val k = density.density.toDouble()
            val px = distribute(
                viewportPx.toDouble(),
                naturalPx.toDouble(),
                Tiers(pMin.toDouble(), tiers.pNat * k, tiers.pMax * k, tiers.pOver * k, hdMin.toDouble(), tiers.hdCap * k),
            )
            val pad = floor(px.rowPad)
            val header = floor(px.header)
            spare = Spare(header, pad, (viewportPx - naturalPx - (header - hdMin) - TicketRowSides * (pad - pMin)).coerceAtLeast(0.0))
        }
        val decided = spare
        val headerHeight = with(density) { decided?.header?.toFloat()?.toDp() ?: tiers.hdMin.dp }
        val rowPad = with(density) { decided?.rowPad?.toFloat()?.toDp() ?: tiers.pMin.dp }
        val rest = with(density) { decided?.rest?.toFloat()?.toDp() ?: 0.dp }
        val type = confirmationType(size)
        val textWidth = maxWidth - Spacing.M * 4
        val actionsWidth = maxWidth - Spacing.M * 2

        val figure: @Composable (Dp, Modifier) -> Unit = { height, figureModifier ->
            WelcomingFigure(
                height = height,
                still = when {
                    !mayPlay -> R.drawable.mascot_welcoming_end
                    mascotStarted -> null
                    else -> R.drawable.mascot_welcoming_start
                },
                modifier = figureModifier
                    .onGloballyPositioned { box ->
                        // The pass that decides still lays out the provisional header, so only the decided one is reported.
                        if (spare?.let { abs(box.size.height - it.header) < 1 } == true) {
                            val k = box.size.height / 249f
                            onMascotPlaced(Rect(box.positionInRoot() + Offset(-88f * k, -51f * k), Size(360f * k, 360f * k)))
                        }
                    }
                    .graphicsLayer { alpha = if (mascotCarried && !mascotHeld && revealed) 1f else mascotIn.value },
            )
        }

        Column(Modifier.fillMaxSize()) {
            Spacer(Modifier.height(GrabCompletion))
            Box(
                Modifier
                    .weight(1f)
                    .fillMaxWidth()
                    .onSizeChanged {
                        viewportPx = it.height
                        decide()
                    },
            ) {
                Column(Modifier.fillMaxWidth().verticalScroll(scroll).padding(horizontal = Spacing.M)) {
                    Column(
                        Modifier.onSizeChanged {
                            val applied = spare
                            naturalPx = it.height - (applied?.let { s -> (s.header - hdMin + TicketRowSides * (s.rowPad - pMin)).toInt() } ?: 0)
                            decide()
                        },
                    ) {
                        ConfirmationHeader(
                            title = title,
                            uiState = uiState,
                            reference = order?.displayOrderNumber?.takeIf { it.isNotBlank() },
                            type = type,
                            height = headerHeight,
                            ax = ax,
                            figure = figure,
                        )
                        ConfirmationTicket(success, uiState, order, type, rowPad, ax, textWidth, onRetry)
                    }
                    if (ax) {
                        Spacer(Modifier.height(Spacing.M))
                        HomeButton(stringResource(R.string.booking_success_go_home), Modifier.fillMaxWidth()) { if (settled) onGoHome() }
                    }
                    Spacer(Modifier.height(rest))
                }
            }
            ConfirmationActions(
                ax = ax,
                edge = scroll.canScrollForward,
                contentWidth = actionsWidth,
                onViewOrders = { if (settled) onViewOrders() },
                onGoHome = { if (settled) onGoHome() },
            )
        }
    }
}

private data class ConfirmationType(
    val title: TextStyle,
    val cardTitle: TextStyle,
    val line: TextStyle,
    val text: TextStyle,
    val window: TextStyle,
    val month: TextStyle,
    val day: TextStyle,
    val leafSide: Dp,
    val seat: Dp,
)

@Composable
private fun confirmationType(size: ConfirmationSize): ConfirmationType {
    val density = LocalDensity.current
    val typography = MaterialTheme.typography
    val ax = size == ConfirmationSize.Ax
    fun TextUnit.stopAt(max: Dp): TextUnit = with(density) { if (toDp() > max) max.toSp() else this@stopAt }
    fun TextStyle.stopAt(size: Dp, line: Dp) =
        if (ax) copy(fontSize = fontSize.stopAt(size), lineHeight = lineHeight.stopAt(line)) else this
    val tnum = "tnum"
    val seat = when (size) {
        ConfirmationSize.Compact -> 26.dp
        ConfirmationSize.Regular -> 28.dp
        ConfirmationSize.Tall -> 30.dp
        ConfirmationSize.Ax -> 36.dp
    }
    return ConfirmationType(
        title = typography.headlineMedium.copy(fontWeight = FontWeight.Bold).stopAt(32.dp, 38.dp),
        cardTitle = typography.titleMedium,
        line = typography.bodyLarge,
        text = typography.bodyMedium,
        window = typography.headlineMedium.copy(fontWeight = FontWeight.Bold, fontFeatureSettings = tnum).stopAt(32.dp, 38.dp),
        month = typography.labelMedium.copy(fontWeight = FontWeight.SemiBold).stopAt(18.dp, 22.dp),
        day = typography.titleMedium.copy(fontFeatureSettings = tnum).stopAt(48.dp, 48.dp),
        leafSide = confirmationLeafSide(size),
        seat = seat,
    )
}

@Composable
private fun ConfirmationHeader(
    title: String,
    uiState: BookingSuccessUiState,
    reference: String?,
    type: ConfirmationType,
    height: Dp,
    ax: Boolean,
    figure: @Composable (Dp, Modifier) -> Unit,
) {
    Row(
        Modifier.zIndex(1f).fillMaxWidth().then(if (ax) Modifier.heightIn(min = height) else Modifier.height(height)),
        verticalAlignment = Alignment.Bottom,
        horizontalArrangement = Arrangement.spacedBy(Spacing.S),
    ) {
        Column(Modifier.weight(1f).padding(start = Spacing.M, bottom = 14.dp)) {
            Text(title, style = type.title, color = MaterialTheme.colorScheme.onSurface, modifier = Modifier.semantics { heading() })
            if (uiState != BookingSuccessUiState.Error) {
                val referenceStyle = type.text.copy(fontFeatureSettings = "tnum")
                Crossfade(reference, animationSpec = tween(250), modifier = Modifier.padding(top = 2.dp), label = "order-reference") { shown ->
                    when {
                        shown != null -> SelectionContainer {
                            Text(shown, style = referenceStyle, color = MaterialTheme.colorScheme.onSurfaceVariant)
                        }
                        uiState == BookingSuccessUiState.Loading -> PlaceholderLine(116.dp, 15.dp, referenceStyle)
                    }
                }
            }
        }
        figure(height, Modifier.padding(end = 14.dp).offset(y = 1.dp))
    }
}

/**
 * The welcoming mascot, sized by its figure: the 360-px art canvas holds it at x 88–255, y 51–300. Plays the
 * one-shot when [still] is null.
 */
@Composable
private fun WelcomingFigure(height: Dp, still: Int?, modifier: Modifier = Modifier) {
    val k = height / 249f
    Box(modifier.size(width = k * 167f, height = height).clearAndSetSemantics {}) {
        val canvas = Modifier
            .wrapContentSize(Alignment.TopStart, unbounded = true)
            .offset(x = -(k * 88f), y = -(k * 51f))
        if (still == null) {
            MascotAnimation(
                resId = R.raw.mascot_welcoming,
                size = k * 360f,
                modifier = canvas,
                loop = false,
                placeholder = painterResource(R.drawable.mascot_welcoming_start),
            )
        } else {
            Image(
                painter = painterResource(still),
                contentDescription = null,
                contentScale = ContentScale.Fit,
                modifier = canvas.size(k * 360f),
            )
        }
    }
}

@Composable
private fun placeholderInk(): Color =
    MaterialTheme.colorScheme.onSurface.copy(alpha = if (MaterialTheme.colorScheme.surface.luminance() < 0.5f) 0.12f else 0.08f)

/** A value not known yet: a flat bar at its final size, on the value's own line so its baseline holds. */
@Composable
private fun PlaceholderLine(width: Dp, barHeight: Dp, style: TextStyle, modifier: Modifier = Modifier) {
    val color = placeholderInk()
    Text(
        " ",
        style = style,
        modifier = modifier
            .width(width)
            .clearAndSetSemantics {}
            .drawBehind {
                val bar = barHeight.toPx()
                drawRoundRect(color, Offset(0f, (size.height - bar) / 2f), Size(size.width, bar), CornerRadius(5.dp.toPx()))
            },
    )
}

@Composable
private fun ConfirmationTicket(
    success: BookingSuccess,
    uiState: BookingSuccessUiState,
    order: OrderDetailDto?,
    type: ConfirmationType,
    rowPad: Dp,
    ax: Boolean,
    textWidth: Dp,
    onRetry: () -> Unit,
) {
    val scheme = MaterialTheme.colorScheme
    val shape = CleansiaShapes.large
    Column(
        Modifier
            .fillMaxWidth()
            .background(scheme.surface, shape)
            .border(1.dp, scheme.outline, shape)
            .padding(horizontal = Spacing.M, vertical = 1.dp),
    ) {
        val row = Modifier.fillMaxWidth().padding(vertical = rowPad)
        FindingRow(success, uiState, order, type, row)
        HorizontalDivider(thickness = 1.dp, color = scheme.outline)
        AnswerRow(success, order, type, ax, textWidth, row)
        HorizontalDivider(thickness = 1.dp, color = scheme.outline)
        BookedRow(success, type, row)
        HorizontalDivider(thickness = 1.dp, color = scheme.outline)
        MoneyRow(success, uiState, type, onRetry, row)
    }
}

@Composable
private fun FindingRow(
    success: BookingSuccess,
    uiState: BookingSuccessUiState,
    order: OrderDetailDto?,
    type: ConfirmationType,
    modifier: Modifier,
) {
    val scheme = MaterialTheme.colorScheme
    val lead = stringResource(R.string.booking_success_finding)
    val sub = stringResource(R.string.booking_success_finding_sub)
    val remedy = stringResource(success.remedyRes())
    val crew = success.requiredEmployees
    val needed = if (crew > 0) pluralStringResource(R.plurals.booking_success_crew_needed, crew, crew) else null
    Column(modifier) {
        Column(Modifier.clearAndSetSemantics { contentDescription = listOfNotNull(lead, needed, sub, remedy).joinToString(" ") }) {
            Text(lead, style = type.cardTitle, color = scheme.onSurface)
            Text(sub, style = type.text, color = scheme.onSurfaceVariant)
            Text(remedy, style = type.text, color = scheme.onSurfaceVariant)
            if (crew > 0) {
                Row(Modifier.padding(top = 10.dp), verticalAlignment = Alignment.CenterVertically) {
                    Seats(crew, type.seat)
                    Spacer(Modifier.width(10.dp))
                    Text(pluralStringResource(R.plurals.booking_success_crew, crew, crew), style = type.text, color = scheme.onSurfaceVariant)
                }
            }
        }
        Deadline(success, uiState, order, type, Modifier.padding(top = Spacing.S))
    }
}

@Composable
private fun Seats(count: Int, diameter: Dp) {
    val scheme = MaterialTheme.colorScheme
    val seat = scheme.onSurfaceVariant.copy(alpha = if (scheme.surface.luminance() < 0.5f) 0.8f else 0.62f)
    val fill = scheme.surface
    Box(Modifier.size(width = diameter * count - 6.dp * (count - 1), height = diameter)) {
        repeat(count) { index ->
            Box(
                Modifier
                    .offset(x = (diameter - 6.dp) * index)
                    .size(diameter)
                    .drawBehind {
                        val stroke = 1.5.dp.toPx()
                        drawCircle(fill)
                        drawCircle(
                            seat,
                            radius = (size.minDimension - stroke) / 2f,
                            style = Stroke(stroke, pathEffect = PathEffect.dashPathEffect(floatArrayOf(3.dp.toPx(), 2.5.dp.toPx()))),
                        )
                    },
                contentAlignment = Alignment.Center,
            ) {
                Icon(Icons.Outlined.Person, contentDescription = null, tint = seat, modifier = Modifier.size(diameter * 0.56f))
            }
        }
    }
}

@Composable
private fun Deadline(
    success: BookingSuccess,
    uiState: BookingSuccessUiState,
    order: OrderDetailDto?,
    type: ConfirmationType,
    modifier: Modifier,
) {
    val locale = LocalConfiguration.current.locales[0]
    val now = remember { Instant.now() }
    val cleaningAt = order?.cleaningDateTime?.takeIf { it.isNotBlank() } ?: success.cleaningDateTime
    val until = order?.let { freeCancellationUntil(cleaningAt, it.freeCancellationHours, now) }
    val loading = uiState == BookingSuccessUiState.Loading
    val shown: Any? = when {
        until != null -> until
        loading -> slotADayAhead(cleaningAt, now)
        else -> null
    }
    val ink = MaterialTheme.colorScheme.onSurfaceVariant
    val fallback = stringResource(R.string.booking_success_free_while)
    val slot = remember(success.cleaningDateTime) { success.cleaningDateTime?.let { runCatching { Instant.parse(it) }.getOrNull() } }
    Crossfade(shown, animationSpec = tween(250), modifier = modifier, label = "free-cancellation") { line ->
        when (line) {
            is Instant -> Text(freeUntil(line, locale), style = type.text, color = ink)
            true -> slot?.let {
                val sentence = freeUntil(it, locale)
                PendingSentence(sentence, sentence.spanStyles.firstOrNull()?.start ?: 0, 164.dp, type.text, ink)
            }
            false -> PendingSentence(AnnotatedString(fallback), 0, 250.dp, type.text, ink)
            else -> Text(fallback, style = type.text, color = ink)
        }
    }
}

/**
 * A sentence the order has not confirmed yet, laid out now so the spare height is decided with the lines it lands
 * with: from [from] on it is unseen, with a flat bar where it will be on each of its lines, none wider than [cap].
 */
@Composable
private fun PendingSentence(text: AnnotatedString, from: Int, cap: Dp, style: TextStyle, color: Color) {
    val bar = placeholderInk()
    val known = text.text.substring(0, from).trim()
    var layout by remember { mutableStateOf<TextLayoutResult?>(null) }
    Text(
        buildAnnotatedString {
            append(text)
            addStyle(SpanStyle(color = Color.Transparent), from, text.length)
        },
        style = style,
        color = color,
        onTextLayout = { layout = it },
        modifier = Modifier
            .fillMaxWidth()
            .clearAndSetSemantics { if (known.isNotEmpty()) contentDescription = known }
            .drawBehind {
                val lines = layout ?: return@drawBehind
                val height = 17.dp.toPx()
                val first = lines.getLineForOffset(from)
                for (line in first until lines.lineCount) {
                    val start = if (line == first) lines.getHorizontalPosition(from, true) else lines.getLineLeft(line)
                    val mid = (lines.getLineTop(line) + lines.getLineBottom(line)) / 2f
                    drawRoundRect(bar, Offset(start, mid - height / 2f), Size(minOf(cap.toPx(), size.width - start), height), CornerRadius(5.dp.toPx()))
                }
            },
    )
}

@Composable
private fun freeUntil(deadline: Instant, locale: Locale): AnnotatedString {
    val date = deadlineLabel(deadline, locale).replace(' ', ' ')
    val sentence = stringResource(R.string.booking_success_free_until, date)
    val at = sentence.indexOf(date)
    val ink = MaterialTheme.colorScheme.onSurface
    return buildAnnotatedString {
        append(sentence)
        if (at >= 0) addStyle(SpanStyle(color = ink), at, at + date.length)
    }
}

@Composable
private fun AnswerRow(
    success: BookingSuccess,
    order: OrderDetailDto?,
    type: ConfirmationType,
    ax: Boolean,
    textWidth: Dp,
    modifier: Modifier,
) {
    val locale = LocalConfiguration.current.locales[0]
    val appointment = success.appointment(order, locale)
    if (appointment == null) {
        Box(modifier)
        return
    }
    val street = order?.address?.street?.takeIf { it.isNotBlank() } ?: success.street
    val city = order?.address?.city?.takeIf { it.isNotBlank() } ?: success.city
    val address = appointmentAddress(street, city)
    val scheme = MaterialTheme.colorScheme
    val dayStyle = type.line
    val leaf = @Composable {
        Box(
            Modifier
                .size(type.leafSide)
                .background(scheme.primaryContainer, CleansiaShapes.small)
                .clearAndSetSemantics {},
            contentAlignment = Alignment.Center,
        ) {
            Column(horizontalAlignment = Alignment.CenterHorizontally) {
                Text(appointment.month.uppercase(locale), style = type.month, color = scheme.primaryTextOnContainer)
                Text(appointment.day, style = type.day, color = scheme.onPrimaryContainer, modifier = Modifier.padding(top = 4.dp))
            }
        }
    }
    if (ax) {
        Column(modifier.semantics(mergeDescendants = true) {}) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                leaf()
                Spacer(Modifier.width(Spacing.M))
                Text(appointment.dayLine, style = dayStyle, color = scheme.onSurface)
            }
            Text(appointment.time, style = type.window, color = scheme.onSurface, modifier = Modifier.padding(top = 14.dp, bottom = 2.dp))
            if (address != null) Text(address, style = type.text, color = scheme.onSurface, modifier = Modifier.padding(top = 6.dp))
        }
        return
    }
    val measurer = rememberTextMeasurer()
    val density = LocalDensity.current
    val slot = textWidth - type.leafSide - 14.dp
    val natural = with(density) { measurer.measure(appointment.time, type.window, maxLines = 1, softWrap = false).size.width.toDp() }
    val window = if (natural > slot) type.window.copy(fontSize = type.window.fontSize * (slot / natural).coerceAtLeast(0.85f)) else type.window
    Row(modifier.semantics(mergeDescendants = true) {}, verticalAlignment = Alignment.CenterVertically) {
        leaf()
        Spacer(Modifier.width(14.dp))
        Column {
            Text(appointment.dayLine, style = dayStyle, color = scheme.onSurface)
            Text(
                appointment.time,
                style = window,
                color = scheme.onSurface,
                maxLines = 1,
                softWrap = false,
                modifier = Modifier.padding(vertical = 2.dp),
            )
            if (address != null) Text(address, style = type.text, color = scheme.onSurface)
        }
    }
}

@Composable
private fun BookedRow(success: BookingSuccess, type: ConfirmationType, modifier: Modifier) {
    val scheme = MaterialTheme.colorScheme
    val dirtiness = success.dirtinessShown()
    Column(modifier.semantics(mergeDescendants = true) {}) {
        if (success.basketNames.isNotEmpty()) {
            Text(success.basketNames.joinToString(" · "), style = type.cardTitle, color = scheme.onSurface)
        }
        if (success.rooms > 0) {
            val home = roomsAndBathrooms(success.rooms, success.bathrooms)
            val sub = if (dirtiness == null) home else {
                "$home · ${stringResource(R.string.dirtiness_level_label)}: ${stringResource(dirtiness.titleRes())}"
            }
            Text(sub, style = type.text, color = scheme.onSurfaceVariant)
        }
        if (success.extraNames.isNotEmpty()) {
            FlowRow(
                Modifier.padding(top = 8.dp),
                horizontalArrangement = Arrangement.spacedBy(6.dp),
                verticalArrangement = Arrangement.spacedBy(6.dp),
            ) {
                success.extraNames.forEach { name ->
                    Row(
                        Modifier
                            .heightIn(min = 30.dp)
                            .background(scheme.surfaceVariant, CircleShape)
                            .padding(start = 9.dp, top = 4.dp, end = 12.dp, bottom = 4.dp),
                        verticalAlignment = Alignment.CenterVertically,
                    ) {
                        Icon(Icons.Rounded.Check, contentDescription = null, tint = scheme.onSurfaceVariant, modifier = Modifier.size(14.dp))
                        Spacer(Modifier.width(6.dp))
                        Text(name, style = type.text, color = scheme.onSurfaceVariant, maxLines = 1)
                    }
                }
            }
        }
    }
}

@Composable
private fun MoneyRow(
    success: BookingSuccess,
    uiState: BookingSuccessUiState,
    type: ConfirmationType,
    onRetry: () -> Unit,
    modifier: Modifier,
) {
    val scheme = MaterialTheme.colorScheme
    val money = success.money(uiState)
    val loading = uiState == BookingSuccessUiState.Loading
    val unavailable = uiState == BookingSuccessUiState.Error
    val loadingLabel = stringResource(R.string.booking_success_details_loading)
    val totalLabelStyle = type.line
    val totalStyle = type.cardTitle.copy(fontFeatureSettings = "tnum")
    val figureStyle = type.text.copy(fontFeatureSettings = "tnum")
    Column(
        modifier.then(
            when {
                loading -> Modifier.clearAndSetSemantics { contentDescription = loadingLabel }
                unavailable -> Modifier.semantics(mergeDescendants = true) {}
                else -> Modifier
            },
        ),
        verticalArrangement = Arrangement.spacedBy(4.dp),
    ) {
        money.rows.forEachIndexed { index, (label, figure) ->
            val isTotal = index == 0
            Row(
                Modifier
                    .fillMaxWidth()
                    .padding(bottom = if (isTotal && money.rows.size > 1) 2.dp else 0.dp)
                    .then(if (unavailable) Modifier else Modifier.semantics(mergeDescendants = true) {}),
            ) {
                Text(
                    stringResource(label),
                    style = if (isTotal) totalLabelStyle else type.text,
                    color = if (isTotal) scheme.onSurface else scheme.onSurfaceVariant,
                    modifier = Modifier.weight(1f).alignByBaseline(),
                )
                Spacer(Modifier.width(Spacing.S))
                Crossfade(figure, animationSpec = tween(250), modifier = Modifier.alignByBaseline(), label = "money-$label") { shown ->
                    when {
                        shown != null -> Text(
                            shown,
                            style = if (isTotal) totalStyle else figureStyle,
                            color = scheme.onSurface,
                            maxLines = 1,
                            softWrap = false,
                        )
                        unavailable -> Text(
                            "—",
                            style = figureStyle,
                            color = scheme.onSurfaceVariant,
                            modifier = Modifier.clearAndSetSemantics {},
                        )
                        else -> PlaceholderLine(
                            if (isTotal) 88.dp else if (label == R.string.order_paid_with_credit) 58.dp else 64.dp,
                            if (isTotal) 20.dp else 16.dp,
                            if (isTotal) totalStyle else figureStyle,
                        )
                    }
                }
            }
        }
        val note = money.note
        if (unavailable && note != null) {
            Row(Modifier.fillMaxWidth().padding(top = 2.dp)) {
                Text(stringResource(note), style = type.text, color = scheme.onSurfaceVariant, modifier = Modifier.weight(1f).alignByBaseline())
                Spacer(Modifier.width(Spacing.S))
                Text(
                    stringResource(R.string.order_detail_error_retry),
                    style = MaterialTheme.typography.labelLarge,
                    color = primaryText(),
                    maxLines = 1,
                    modifier = Modifier.alignByBaseline().clickable(role = Role.Button, onClick = onRetry),
                )
            }
        } else if (note != null) {
            Text(stringResource(note), style = type.text, color = scheme.onSurfaceVariant)
        }
    }
}

@Composable
private fun HomeButton(label: String, modifier: Modifier = Modifier, onClick: () -> Unit) {
    OutlinedButton(
        onClick = onClick,
        shape = CircleShape,
        border = BorderStroke(1.dp, MaterialTheme.colorScheme.outline),
        colors = ButtonDefaults.outlinedButtonColors(contentColor = MaterialTheme.colorScheme.onSurface),
        contentPadding = PaddingValues(horizontal = HomeButtonPadding, vertical = 12.dp),
        modifier = modifier.heightIn(min = CleansiaButtonSize.Large.minHeight),
    ) {
        Text(label, style = MaterialTheme.typography.titleMedium, maxLines = 1, overflow = TextOverflow.Ellipsis)
    }
}

/**
 * Pinned under the scroll: Back to home and View order side by side, sharing the spare width 1 : 1.4 as
 * flex does; the stacked fallback when the two labels cannot sit side by side. At large text only View
 * order is pinned. The bar's top edge shows only while the ticket runs under it.
 */
@Composable
private fun ConfirmationActions(ax: Boolean, edge: Boolean, contentWidth: Dp, onViewOrders: () -> Unit, onGoHome: () -> Unit) {
    val homeLabel = stringResource(R.string.booking_success_go_home)
    val viewLabel = stringResource(R.string.booking_success_view_orders)
    val outline = MaterialTheme.colorScheme.outline
    val measurer = rememberTextMeasurer()
    val density = LocalDensity.current
    val labelStyle = MaterialTheme.typography.titleMedium
    fun width(text: String) = with(density) { measurer.measure(text, labelStyle, maxLines = 1, softWrap = false).size.width.toDp() }
    val home = width(homeLabel) + HomeButtonPadding * 2
    val view = width(viewLabel) + CleansiaButtonSize.Large.horizontalPadding * 2
    val free = contentWidth - Spacing.S - home - view
    Column(
        Modifier
            .fillMaxWidth()
            .drawBehind { if (edge) drawLine(outline, Offset(0f, 0f), Offset(size.width, 0f), 1.dp.toPx()) }
            .navigationBarsPadding()
            .padding(horizontal = Spacing.M, vertical = Spacing.S),
    ) {
        when {
            ax -> CleansiaPrimaryButton(text = viewLabel, onClick = onViewOrders)
            free >= 0.dp -> Row(horizontalArrangement = Arrangement.spacedBy(Spacing.S), verticalAlignment = Alignment.CenterVertically) {
                HomeButton(homeLabel, Modifier.width(home + free / 2.4f), onGoHome)
                CleansiaPrimaryButton(text = viewLabel, onClick = onViewOrders, modifier = Modifier.width(view + free * 1.4f / 2.4f))
            }
            else -> Column(verticalArrangement = Arrangement.spacedBy(Spacing.XS)) {
                CleansiaPrimaryButton(text = viewLabel, onClick = onViewOrders)
                CleansiaTextButton(onClick = onGoHome, modifier = Modifier.fillMaxWidth().heightIn(min = 48.dp)) {
                    Text(homeLabel, style = MaterialTheme.typography.labelLarge)
                }
            }
        }
    }
}

@Preview(name = "Booking confirmation", widthDp = 412, heightDp = 842)
@Preview(name = "Booking confirmation dark", widthDp = 412, heightDp = 842, uiMode = android.content.res.Configuration.UI_MODE_NIGHT_YES)
@Composable
private fun BookingSuccessPreview() {
    CleansiaTheme {
        BookingSuccessContent(
            success = BookingSuccess(
                confirmationCode = "130AA4", orderId = "preview-order", cleaningDateTime = "2026-10-31T08:00:00Z",
                estimatedMinutes = 180, street = "Zenklova 6", city = "Praha 8", creditApplied = true,
                requiredEmployees = 2, rooms = 2, bathrooms = 1, basketNames = listOf("General Cleaning"),
                extraNames = listOf("Inside oven cleaning"),
            ),
            uiState = BookingSuccessUiState.Loaded(OrderDetailDto(
                id = "preview-order", displayOrderNumber = "ORD-88BC31F3", totalPrice = 1150.0,
                originalSubtotal = 1150.0, appliedDiscountSource = 0, creditAppliedAmount = 250.0, amountDueOnCard = 900.0,
                cleaningDateTime = "2026-10-31T08:00:00Z", estimatedTime = 180, freeCancellationHours = 24,
                orderStatus = CodeDto(type = "OrderStatus", name = "New", value = 0),
                address = OrderAddressDto(street = "Zenklova 6", city = "Praha 8"),
                currency = OrderCurrencyDetailDto(code = "CZK", symbol = "Kč"),
            )), onViewOrders = {}, onGoHome = {},
        )
    }
}
