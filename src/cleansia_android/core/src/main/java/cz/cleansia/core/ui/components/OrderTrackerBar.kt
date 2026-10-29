package cz.cleansia.core.ui.components

import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.fillMaxHeight
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.setValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.animation.core.RepeatMode
import androidx.compose.animation.core.animateFloat
import androidx.compose.animation.core.infiniteRepeatable
import androidx.compose.animation.core.rememberInfiniteTransition
import androidx.compose.animation.core.tween
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
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
    pulseCurrent: Boolean = false,
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
    val pulse = if (pulseCurrent && pulseAllowed && inForeground) {
        rememberInfiniteTransition(label = "active-cleaning").animateFloat(
            initialValue = 0.6f,
            targetValue = 1f,
            animationSpec = infiniteRepeatable(tween(1_200), RepeatMode.Reverse),
            label = "active-cleaning-opacity",
        )
    } else null
    val shape = RoundedCornerShape(5.dp)
    val active = when {
        cancelled -> MaterialTheme.colorScheme.error
        overdue -> MaterialTheme.colorScheme.warningText
        else -> MaterialTheme.colorScheme.primary
    }
    Column(modifier.fillMaxWidth()) {
        val label = if (cancelled) cancelledLabel else stepCounterLabel
        if (label != null) {
            Text(label, style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
            Spacer(Modifier.height(4.dp))
        }
        if (cancelled) {
            Box(Modifier.fillMaxWidth().height(10.dp).clip(shape).background(active))
        } else {
            Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(6.dp)) {
                repeat(totalSteps) { index ->
                    val past = index < currentStep
                    val current = index == currentStep
                    val tint = if (current) active else MaterialTheme.colorScheme.primary
                    val fill = when {
                        past -> 1f
                        current -> currentFraction?.coerceIn(0f, 1f) ?: 0f
                        else -> 0f
                    }
                    Box(
                        Modifier.weight(1f).height(10.dp).clip(shape)
                            .graphicsLayer { alpha = if (current) pulse?.value ?: 1f else 1f }
                            .background(if (past || current) tint.copy(alpha = 0.12f) else MaterialTheme.colorScheme.surfaceVariant)
                            .border(2.dp, if (past || current) tint else MaterialTheme.colorScheme.outlineVariant, shape)
                            .padding(2.dp),
                    ) {
                        if (fill > 0f) {
                            Box(Modifier.fillMaxWidth(fill).fillMaxHeight().clip(shape).background(tint))
                        }
                    }
                }
            }
        }
    }
}
