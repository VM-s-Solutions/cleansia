package cz.cleansia.core.ui.components

import androidx.compose.animation.core.LinearEasing
import androidx.compose.animation.core.RepeatMode
import androidx.compose.animation.core.animateFloat
import androidx.compose.animation.core.infiniteRepeatable
import androidx.compose.animation.core.rememberInfiniteTransition
import androidx.compose.animation.core.tween
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxHeight
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.draw.drawWithCache
import androidx.compose.ui.geometry.Size
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.drawscope.translate
import androidx.compose.ui.graphics.graphicsLayer
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.unit.dp
import androidx.lifecycle.compose.LifecycleResumeEffect
import cz.cleansia.core.ui.theme.warningText

@Composable
fun OrderTrackerBar(
    currentStep: Int,
    stepCounterLabel: String?,
    modifier: Modifier = Modifier,
    totalSteps: Int = 5,
    cancelled: Boolean = false,
    cancelledLabel: String? = null,
    currentFraction: Float? = null,
    overdue: Boolean = false,
    highlightCurrent: Boolean = true,
) {
    val context = LocalContext.current
    var pulseAllowed by remember { mutableStateOf(false) }
    var inForeground by remember { mutableStateOf(false) }
    LifecycleResumeEffect(Unit) {
        inForeground = true
        pulseAllowed = android.provider.Settings.Global.getFloat(
            context.contentResolver,
            android.provider.Settings.Global.ANIMATOR_DURATION_SCALE,
            1f,
        ) > 0f
        onPauseOrDispose { inForeground = false }
    }
    val transition = if (highlightCurrent && !cancelled && currentStep in 0 until totalSteps && pulseAllowed && inForeground) {
        rememberInfiniteTransition(label = "current-phase")
    } else null
    val pulse = transition?.animateFloat(
        initialValue = 0.88f,
        targetValue = 1f,
        animationSpec = infiniteRepeatable(tween(1_200), RepeatMode.Reverse),
        label = "active-cleaning-opacity",
    )
    val sweep = transition?.animateFloat(
        initialValue = -0.5f,
        targetValue = 2.5f,
        animationSpec = infiniteRepeatable(tween(1_300, easing = LinearEasing), RepeatMode.Restart),
        label = "current-phase-sweep",
    )
    val shape = RoundedCornerShape(2.dp)
    val active = when {
        cancelled -> MaterialTheme.colorScheme.error
        overdue -> MaterialTheme.colorScheme.warningText
        else -> MaterialTheme.colorScheme.primary
    }
    Column(modifier.fillMaxWidth()) {
        val label = if (cancelled) cancelledLabel else stepCounterLabel
        if (label != null) {
            Row(Modifier.fillMaxWidth(), horizontalArrangement = if (cancelled) Arrangement.Start else Arrangement.End) {
                Text(
                    label,
                    style = MaterialTheme.typography.labelSmall,
                    color = if (cancelled) active else MaterialTheme.colorScheme.onSurfaceVariant,
                )
            }
            Spacer(Modifier.height(4.dp))
        }
        if (cancelled) {
            Box(Modifier.fillMaxWidth().height(4.dp).clip(shape).background(active))
        } else {
            Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(6.dp)) {
                repeat(totalSteps) { index ->
                    val past = index < currentStep
                    val current = index == currentStep
                    val tint = if (current) active else MaterialTheme.colorScheme.primary
                    val fill = currentFraction?.coerceIn(0f, 1f) ?: 0f
                    Box(
                        Modifier.weight(1f).height(4.dp).clip(shape)
                            .background(
                                when {
                                    past -> tint
                                    current -> tint.copy(alpha = 0.22f)
                                    else -> MaterialTheme.colorScheme.outlineVariant
                                },
                            )
                            .drawWithCache {
                                val bandWidth = size.width * 0.5f
                                val brush = Brush.horizontalGradient(
                                    colors = listOf(tint.copy(alpha = 0f), tint.copy(alpha = 0.18f), tint.copy(alpha = 0.30f)),
                                    startX = 0f,
                                    endX = bandWidth,
                                )
                                onDrawWithContent {
                                    drawContent()
                                    if (current && sweep != null) {
                                        translate(left = size.width * sweep.value) {
                                            drawRect(brush, size = Size(bandWidth, size.height))
                                        }
                                    }
                                }
                            },
                    ) {
                        if (current && fill > 0f) {
                            Box(
                                Modifier.fillMaxWidth(fill).fillMaxHeight().clip(shape)
                                    .graphicsLayer { alpha = pulse?.value ?: 1f }.background(tint),
                            )
                        }
                    }
                }
            }
        }
    }
}
