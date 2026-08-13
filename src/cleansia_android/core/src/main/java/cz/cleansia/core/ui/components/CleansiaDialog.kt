package cz.cleansia.core.ui.components

import androidx.compose.animation.AnimatedVisibility
import androidx.compose.animation.core.MutableTransitionState
import androidx.compose.animation.core.Spring
import androidx.compose.animation.core.spring
import androidx.compose.animation.core.tween
import androidx.compose.animation.fadeIn
import androidx.compose.animation.fadeOut
import androidx.compose.animation.scaleIn
import androidx.compose.animation.scaleOut
import androidx.compose.animation.slideInVertically
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.defaultMinSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.widthIn
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.remember
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.TransformOrigin
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.unit.dp
import androidx.compose.ui.window.Dialog
import androidx.compose.ui.window.DialogProperties

/**
 * Cleansia-styled dialog. Replaces stock Material3 [androidx.compose.material3.AlertDialog]
 * across the app so confirm flows share one shape, padding, and typography.
 *
 * Two flavors via [destructive]: standard (primary-tinted confirm) and
 * destructive (error-tinted confirm + matching icon halo for delete/cancel).
 *
 * Optional [icon] renders a circular halo above the title — use for high-stakes
 * confirms (delete account, cancel subscription) where a glyph helps the user
 * orient. Omit for routine actions.
 *
 * For dialogs with custom body content (a TextField, a list of cleaners),
 * pass a [content] slot instead of [message]. When both are provided, [content]
 * renders below [message]. The Surface's [widthIn] keeps the dialog from
 * stretching edge-to-edge on tablets.
 */
@Composable
fun CleansiaDialog(
    onDismiss: () -> Unit,
    title: String,
    confirmLabel: String,
    onConfirm: () -> Unit,
    modifier: Modifier = Modifier,
    message: String? = null,
    dismissLabel: String? = null,
    icon: ImageVector? = null,
    destructive: Boolean = false,
    confirmEnabled: Boolean = true,
    content: (@Composable () -> Unit)? = null,
) {
    Dialog(
        onDismissRequest = onDismiss,
        properties = DialogProperties(usePlatformDefaultWidth = false),
    ) {
        // Entrance choreography: spring-driven scale-up from 0.85 around the
        // bottom anchor + a small upward slide + fade. The transition state
        // starts collapsed and flips to visible on the first composition so
        // the springy "pop" plays even though Dialog itself fades the window
        // in/out at the platform layer. Exit is a fast fade — Dialog tears
        // down the window the moment onDismiss flips state, so a slow exit
        // animation would never finish anyway.
        val visibleState = remember { MutableTransitionState(false) }
            .apply { targetState = true }

        AnimatedVisibility(
            visibleState = visibleState,
            enter = scaleIn(
                animationSpec = spring(
                    dampingRatio = Spring.DampingRatioMediumBouncy,
                    stiffness = Spring.StiffnessMediumLow,
                ),
                initialScale = 0.85f,
                transformOrigin = TransformOrigin(0.5f, 0.6f),
            ) + fadeIn(animationSpec = tween(durationMillis = 220)) +
                slideInVertically(
                    animationSpec = spring(
                        dampingRatio = Spring.DampingRatioMediumBouncy,
                        stiffness = Spring.StiffnessMediumLow,
                    ),
                    initialOffsetY = { it / 8 },
                ),
            exit = fadeOut(animationSpec = tween(durationMillis = 120)) +
                scaleOut(
                    animationSpec = tween(durationMillis = 120),
                    targetScale = 0.95f,
                ),
        ) {
        Surface(
            modifier = modifier
                .padding(horizontal = 24.dp)
                .widthIn(max = 420.dp)
                .fillMaxWidth(),
            shape = RoundedCornerShape(24.dp),
            color = MaterialTheme.colorScheme.surface,
            tonalElevation = 6.dp,
            shadowElevation = 12.dp,
        ) {
            Column(
                modifier = Modifier.padding(horizontal = 24.dp, vertical = 24.dp),
                horizontalAlignment = Alignment.CenterHorizontally,
            ) {
                if (icon != null) {
                    val haloColor = if (destructive) {
                        MaterialTheme.colorScheme.errorContainer
                    } else {
                        MaterialTheme.colorScheme.primaryContainer
                    }
                    val tint = if (destructive) {
                        MaterialTheme.colorScheme.onErrorContainer
                    } else {
                        MaterialTheme.colorScheme.onPrimaryContainer
                    }
                    Box(
                        modifier = Modifier
                            .size(56.dp)
                            .background(haloColor, CircleShape),
                        contentAlignment = Alignment.Center,
                    ) {
                        Icon(
                            imageVector = icon,
                            contentDescription = null,
                            tint = tint,
                            modifier = Modifier.size(28.dp),
                        )
                    }
                    Spacer(Modifier.height(16.dp))
                }

                Text(
                    text = title,
                    style = MaterialTheme.typography.titleLarge,
                    color = MaterialTheme.colorScheme.onSurface,
                )

                if (message != null) {
                    Spacer(Modifier.height(8.dp))
                    Text(
                        text = message,
                        style = MaterialTheme.typography.bodyMedium,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                    )
                }

                if (content != null) {
                    Spacer(Modifier.height(16.dp))
                    Box(modifier = Modifier.fillMaxWidth()) { content() }
                }

                Spacer(Modifier.height(24.dp))
                // Equal-weight filled buttons à la Wolt / Bolt confirms — both
                // sides read as tappable targets, the destructive side gets a
                // saturated fill so it never feels like the safe default.
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.spacedBy(12.dp),
                    verticalAlignment = Alignment.CenterVertically,
                ) {
                    if (dismissLabel != null) {
                        DialogActionButton(
                            label = dismissLabel,
                            onClick = onDismiss,
                            containerColor = MaterialTheme.colorScheme.surfaceVariant,
                            contentColor = MaterialTheme.colorScheme.onSurface,
                            modifier = Modifier.weight(1f),
                        )
                    }
                    val confirmContainer = if (destructive) {
                        MaterialTheme.colorScheme.error
                    } else {
                        MaterialTheme.colorScheme.primary
                    }
                    val confirmContent = if (destructive) {
                        MaterialTheme.colorScheme.onError
                    } else {
                        MaterialTheme.colorScheme.onPrimary
                    }
                    DialogActionButton(
                        label = confirmLabel,
                        onClick = onConfirm,
                        containerColor = confirmContainer,
                        contentColor = confirmContent,
                        enabled = confirmEnabled,
                        modifier = Modifier.weight(1f),
                    )
                }
            }
        }
        }
    }
}

@Composable
private fun DialogActionButton(
    label: String,
    onClick: () -> Unit,
    containerColor: androidx.compose.ui.graphics.Color,
    contentColor: androidx.compose.ui.graphics.Color,
    modifier: Modifier = Modifier,
    enabled: Boolean = true,
) {
    Button(
        onClick = onClick,
        enabled = enabled,
        modifier = modifier.defaultMinSize(minHeight = 48.dp),
        shape = RoundedCornerShape(14.dp),
        contentPadding = PaddingValues(horizontal = 16.dp, vertical = 10.dp),
        colors = ButtonDefaults.buttonColors(
            containerColor = containerColor,
            contentColor = contentColor,
        ),
    ) {
        // Material `Button` lays out its slot as a Row(Start) — wrap in a Box
        // so the label centers within the weighted button regardless of width.
        Box(
            modifier = Modifier.fillMaxWidth(),
            contentAlignment = Alignment.Center,
        ) {
            Text(
                text = label,
                style = MaterialTheme.typography.labelLarge,
            )
        }
    }
}
