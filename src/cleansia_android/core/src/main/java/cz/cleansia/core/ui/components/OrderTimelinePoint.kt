package cz.cleansia.core.ui.components

import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.Check
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.semantics.clearAndSetSemantics
import androidx.compose.ui.unit.dp

enum class OrderTimelineSegment { Past, Current, Future }

/**
 * One point on an order's timeline. Past, current and to come are told apart by structure, not fill colour: a past
 * step is a washed disc carrying a check, the current step a bullseye, a step to come an empty outline.
 *
 * Decorative: the row beside it names the step.
 */
@Composable
fun OrderTimelinePoint(
    segment: OrderTimelineSegment,
    modifier: Modifier = Modifier,
    tint: Color = MaterialTheme.colorScheme.primary,
) {
    val disc = Modifier.size(PointSize).clearAndSetSemantics {}
    when (segment) {
        OrderTimelineSegment.Past -> Box(
            modifier = modifier
                .then(disc)
                .background(tint.copy(alpha = FillAlpha), CircleShape)
                .border(1.dp, tint.copy(alpha = EdgeAlpha), CircleShape),
            contentAlignment = Alignment.Center,
        ) {
            Icon(
                imageVector = Icons.Outlined.Check,
                contentDescription = null,
                tint = MaterialTheme.colorScheme.onSurfaceVariant,
                modifier = Modifier.size(10.dp),
            )
        }
        OrderTimelineSegment.Current -> Box(
            modifier = modifier
                .then(disc)
                .background(tint.copy(alpha = FillAlpha), CircleShape)
                .border(2.dp, tint.copy(alpha = EdgeAlpha), CircleShape),
            contentAlignment = Alignment.Center,
        ) {
            Box(Modifier.size(10.dp).background(tint, CircleShape))
        }
        OrderTimelineSegment.Future -> Box(
            modifier = modifier
                .then(disc)
                .border(1.5.dp, MaterialTheme.colorScheme.outlineVariant, CircleShape),
        )
    }
}

private val PointSize = 20.dp
private const val FillAlpha = 0.14f
private const val EdgeAlpha = 0.30f
