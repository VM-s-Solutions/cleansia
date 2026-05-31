package cz.cleansia.partner.features.orders.components

import androidx.compose.animation.core.LinearEasing
import androidx.compose.animation.core.RepeatMode
import androidx.compose.animation.core.animateFloat
import androidx.compose.animation.core.infiniteRepeatable
import androidx.compose.animation.core.rememberInfiniteTransition
import androidx.compose.animation.core.tween
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.Check
import androidx.compose.material.icons.outlined.Close
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.draw.shadow
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.RectangleShape
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import cz.cleansia.partner.R
import cz.cleansia.partner.api.model.OrderStatus

/**
 * Horizontal 5-step lifecycle tracker. Wolt/Foodora-style with extra
 * polish: gradient connector flowing into the current step, pulsing
 * halo around the current dot, tinted label chip on the current label.
 *
 * Steps (in order): New → Confirmed → On the way → In progress →
 * Completed. Pending (._1) collapses into "New"; Cancelled (._6)
 * shows as a standalone red bar.
 */
@Composable
fun OrderStatusProgressBar(
    status: OrderStatus?,
    modifier: Modifier = Modifier,
) {
    if (status == OrderStatus._6) {
        CancelledBar(modifier = modifier)
        return
    }

    val currentIndex = when (status) {
        OrderStatus._0, OrderStatus._1 -> 0
        OrderStatus._2 -> 1
        OrderStatus._3 -> 2
        OrderStatus._4 -> 3
        OrderStatus._5 -> 4
        else -> 0
    }

    // Short labels (tracker_step_*) — the longer status_* strings
    // overflow narrow 5-column rows on small phones (Czech
    // "In Progress" clips to "In"). The longer names still appear in
    // the status timeline card lower down the sheet.
    val labels = listOf(
        stringResource(R.string.tracker_step_new),
        stringResource(R.string.tracker_step_confirmed),
        stringResource(R.string.tracker_step_on_way),
        stringResource(R.string.tracker_step_cleaning),
        stringResource(R.string.tracker_step_done),
    )

    Column(modifier = modifier.fillMaxWidth()) {
        Row(
            modifier = Modifier.fillMaxWidth(),
            verticalAlignment = Alignment.CenterVertically,
        ) {
            labels.forEachIndexed { index, _ ->
                val state = stepStateFor(index, currentIndex)
                StepDot(state = state)
                if (index < labels.lastIndex) {
                    StepConnector(
                        // Connector visual mode:
                        //   - leading into a past step → solid green
                        //   - leading into the current step → green → brand gradient
                        //     so the eye flows into "you are here"
                        //   - everywhere else → muted
                        mode = when {
                            index + 1 < currentIndex -> ConnectorMode.Past
                            index + 1 == currentIndex -> ConnectorMode.Transition
                            else -> ConnectorMode.Future
                        },
                        modifier = Modifier.weight(1f),
                    )
                }
            }
        }
        Spacer(Modifier.height(8.dp))
        Row(
            modifier = Modifier.fillMaxWidth(),
            verticalAlignment = Alignment.Top,
        ) {
            labels.forEachIndexed { index, label ->
                val state = stepStateFor(index, currentIndex)
                StepLabel(
                    label = label,
                    state = state,
                    modifier = Modifier.weight(1f),
                )
            }
        }
    }
}

private enum class StepState { Past, Current, Future }
private enum class ConnectorMode { Past, Transition, Future }

private fun stepStateFor(index: Int, current: Int): StepState = when {
    index < current -> StepState.Past
    index == current -> StepState.Current
    else -> StepState.Future
}

private val GreenPast = Color(0xFF16A34A)
private val DangerRed = Color(0xFFDC2626)

@Composable
private fun StepDot(state: StepState) {
    val brand = MaterialTheme.colorScheme.primary
    val muted = MaterialTheme.colorScheme.outlineVariant
    val surface = MaterialTheme.colorScheme.surface

    when (state) {
        StepState.Past -> Box(
            modifier = Modifier
                .size(22.dp)
                .shadow(elevation = 2.dp, shape = CircleShape, ambientColor = GreenPast, spotColor = GreenPast)
                .clip(CircleShape)
                .background(GreenPast),
            contentAlignment = Alignment.Center,
        ) {
            Icon(
                imageVector = Icons.Outlined.Check,
                contentDescription = null,
                tint = Color.White,
                modifier = Modifier.size(13.dp),
            )
        }
        StepState.Current -> CurrentStepDot(brand = brand)
        StepState.Future -> Box(
            modifier = Modifier
                .size(18.dp)
                .clip(CircleShape)
                .background(surface)
                .border(width = 2.dp, color = muted, shape = CircleShape),
        )
    }
}

/**
 * Current step gets the spotlight treatment: pulsing translucent halo
 * behind a brand-blue dot with a soft brand-tinted shadow. The pulse
 * is slow (~1.6s cycle) so it reads as "alive" without being
 * distracting on a screen the cleaner stares at for minutes at a time.
 */
@Composable
private fun CurrentStepDot(brand: Color) {
    val transition = rememberInfiniteTransition(label = "currentStepHalo")
    val pulseScale by transition.animateFloat(
        initialValue = 1.0f,
        targetValue = 2.0f,
        animationSpec = infiniteRepeatable(
            animation = tween(durationMillis = 1600, easing = LinearEasing),
            repeatMode = RepeatMode.Restart,
        ),
        label = "haloScale",
    )
    val pulseAlpha by transition.animateFloat(
        initialValue = 0.45f,
        targetValue = 0f,
        animationSpec = infiniteRepeatable(
            animation = tween(durationMillis = 1600, easing = LinearEasing),
            repeatMode = RepeatMode.Restart,
        ),
        label = "haloAlpha",
    )
    val haloSize = 26.dp * pulseScale

    Box(
        modifier = Modifier.size(40.dp),
        contentAlignment = Alignment.Center,
    ) {
        Box(
            modifier = Modifier
                .size(haloSize)
                .clip(CircleShape)
                .background(brand.copy(alpha = pulseAlpha)),
        )
        Box(
            modifier = Modifier
                .size(26.dp)
                .shadow(elevation = 4.dp, shape = CircleShape, ambientColor = brand, spotColor = brand)
                .clip(CircleShape)
                .background(brand)
                .border(
                    width = 3.dp,
                    color = MaterialTheme.colorScheme.surface,
                    shape = CircleShape,
                ),
        )
    }
}

@Composable
private fun StepConnector(mode: ConnectorMode, modifier: Modifier = Modifier) {
    val brand = MaterialTheme.colorScheme.primary
    val muted = MaterialTheme.colorScheme.outlineVariant

    val brush = when (mode) {
        ConnectorMode.Past -> Brush.horizontalGradient(listOf(GreenPast, GreenPast))
        ConnectorMode.Transition -> Brush.horizontalGradient(listOf(GreenPast, brand))
        ConnectorMode.Future -> Brush.horizontalGradient(listOf(muted, muted))
    }
    Box(
        modifier = modifier
            .height(3.dp)
            .padding(horizontal = 2.dp)
            .clip(RoundedCornerShape(2.dp))
            .background(brush = brush, shape = RectangleShape),
    )
}

@Composable
private fun StepLabel(
    label: String,
    state: StepState,
    modifier: Modifier = Modifier,
) {
    val color = when (state) {
        StepState.Past -> GreenPast
        StepState.Current -> MaterialTheme.colorScheme.primary
        StepState.Future -> MaterialTheme.colorScheme.onSurfaceVariant
    }
    // Per Foodora pattern: same Text treatment for every step, no
    // background chip. The "you are here" emphasis is owned by the
    // dot above (bigger + halo) and reinforced here by weight + color
    // only — keeping the chip would have pushed the active label
    // wider than its column slot and overflowed into neighbours.
    Text(
        text = label,
        modifier = modifier,
        style = MaterialTheme.typography.labelSmall.copy(
            fontSize = 11.sp,
            fontWeight = if (state == StepState.Current) FontWeight.Bold else FontWeight.Medium,
            lineHeight = 13.sp,
        ),
        color = color,
        textAlign = TextAlign.Center,
        maxLines = 1,
    )
}

@Composable
private fun CancelledBar(modifier: Modifier = Modifier) {
    Row(
        modifier = modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(999.dp))
            .background(DangerRed.copy(alpha = 0.10f))
            .padding(horizontal = 12.dp, vertical = 8.dp),
        verticalAlignment = Alignment.CenterVertically,
        horizontalArrangement = Arrangement.Center,
    ) {
        Box(
            modifier = Modifier
                .size(22.dp)
                .shadow(elevation = 2.dp, shape = CircleShape, ambientColor = DangerRed, spotColor = DangerRed)
                .clip(CircleShape)
                .background(DangerRed),
            contentAlignment = Alignment.Center,
        ) {
            Icon(
                imageVector = Icons.Outlined.Close,
                contentDescription = null,
                tint = Color.White,
                modifier = Modifier.size(12.dp),
            )
        }
        Spacer(Modifier.width(8.dp))
        Text(
            text = stringResource(R.string.status_cancelled),
            style = MaterialTheme.typography.labelLarge.copy(fontWeight = FontWeight.Bold),
            color = DangerRed,
        )
    }
}
