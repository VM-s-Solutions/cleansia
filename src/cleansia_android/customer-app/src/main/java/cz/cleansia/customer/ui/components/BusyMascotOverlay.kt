package cz.cleansia.customer.ui.components

import androidx.compose.animation.AnimatedVisibility
import androidx.compose.animation.EnterExitState
import androidx.compose.animation.EnterTransition
import androidx.compose.animation.ExitTransition
import androidx.compose.animation.core.EaseInOut
import androidx.compose.animation.core.MutableTransitionState
import androidx.compose.animation.core.animateDpAsState
import androidx.compose.animation.core.Spring
import androidx.compose.animation.core.spring
import androidx.compose.animation.core.tween
import androidx.compose.animation.fadeIn
import androidx.compose.animation.fadeOut
import androidx.compose.animation.scaleIn
import androidx.compose.animation.scaleOut
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.interaction.MutableInteractionSource
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.widthIn
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.remember
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.geometry.Rect
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.graphicsLayer
import androidx.compose.ui.layout.onGloballyPositioned
import androidx.compose.ui.layout.positionInRoot
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.toSize
import cz.cleansia.core.ui.components.MascotAnimation
import cz.cleansia.customer.R

internal const val BusyMascotResizeMillis = 250

/**
 * Full-screen busy overlay for long async operations, so the user sees motion rather than a bare spinner.
 *
 * **It swallows touches**, which is what prevents a double-submit mid-flight.
 *
 * [exit] defaults to leaving in one frame; given one, it is the scrim's, and the card leaves on its own fade
 * as the overlay starts leaving. [mascotFigureHeight] draws the cleaner that tall, following a change over
 * [BusyMascotResizeMillis]; without it the mascot keeps its 140 dp box. While [onMascotBounds] is set the mascot is
 * the caller's to carry out: its bounds in the root are reported, and it is not drawn while the overlay leaves.
 */
@Composable
fun BusyMascotOverlay(
    visible: Boolean,
    message: String,
    modifier: Modifier = Modifier,
    exit: ExitTransition = ExitTransition.None,
    mascotFigureHeight: Dp? = null,
    onMascotBounds: ((Rect) -> Unit)? = null,
) {
    if (!visible && exit == ExitTransition.None) return

    AnimatedVisibility(visible = visible, enter = EnterTransition.None, exit = exit) {
        val leaving = transition.targetState == EnterExitState.PostExit
        val state = remember { MutableTransitionState(false) }
            .apply { targetState = !leaving }

        Box(
            modifier = modifier
                .fillMaxSize()
                // Block all touches under the overlay. Empty interactionSource +
                // null indication = no ripple, just the click-swallow effect.
                .clickable(
                    interactionSource = remember { MutableInteractionSource() },
                    indication = null,
                    onClick = {},
                )
                .background(Color.Black.copy(alpha = 0.45f)),
            contentAlignment = Alignment.Center,
        ) {
            AnimatedVisibility(
                visibleState = state,
                enter = scaleIn(
                    animationSpec = spring(
                        dampingRatio = Spring.DampingRatioMediumBouncy,
                        stiffness = Spring.StiffnessMediumLow,
                    ),
                    initialScale = 0.85f,
                ) + fadeIn(animationSpec = tween(durationMillis = 220)),
                exit = fadeOut(animationSpec = tween(durationMillis = 120)) +
                    scaleOut(
                        animationSpec = tween(durationMillis = 120),
                        targetScale = 0.95f,
                    ),
            ) {
                Surface(
                    modifier = Modifier
                        .padding(horizontal = 32.dp)
                        .widthIn(max = 360.dp)
                        .fillMaxWidth(),
                    shape = RoundedCornerShape(24.dp),
                    color = MaterialTheme.colorScheme.surface,
                    tonalElevation = 8.dp,
                    shadowElevation = 16.dp,
                ) {
                    Column(
                        modifier = Modifier.padding(horizontal = 24.dp, vertical = 28.dp),
                        horizontalAlignment = Alignment.CenterHorizontally,
                    ) {
                        val mascotSide by animateDpAsState(
                            mascotFigureHeight?.let { MascotFigureBox.Cleaning.canvasSide(it.value).dp } ?: 140.dp,
                            tween(BusyMascotResizeMillis, easing = EaseInOut),
                            label = "busy-mascot",
                        )
                        MascotAnimation(
                            resId = R.raw.mascot_cleaning_in_progress,
                            size = mascotSide,
                            modifier = if (onMascotBounds == null) Modifier else Modifier
                                .onGloballyPositioned { onMascotBounds(Rect(it.positionInRoot(), it.size.toSize())) }
                                .graphicsLayer { alpha = if (leaving) 0f else 1f },
                        )
                        Spacer(Modifier.height(8.dp))
                        Text(
                            text = message,
                            style = MaterialTheme.typography.titleMedium,
                            fontWeight = FontWeight.SemiBold,
                            color = MaterialTheme.colorScheme.onSurface,
                            textAlign = TextAlign.Center,
                        )
                    }
                }
            }
        }
    }
}
