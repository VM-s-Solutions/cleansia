package cz.cleansia.partner.features.orders.components

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxHeight
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.offset
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Notes
import androidx.compose.material.icons.filled.Warning
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.draw.drawWithContent
import androidx.compose.ui.draw.shadow
import androidx.compose.ui.geometry.Rect
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.Path
import androidx.compose.ui.graphics.PathFillType
import androidx.compose.ui.graphics.drawscope.clipPath
import androidx.compose.ui.graphics.graphicsLayer
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.IntOffset
import androidx.compose.ui.unit.dp
import androidx.compose.ui.zIndex
import cz.cleansia.partner.R
import cz.cleansia.partner.ui.components.CleaningCountdownTimer
import cz.cleansia.partner.ui.components.DynamicCleaningBackground
import cz.cleansia.partner.ui.components.clickableWithHaptic
import cz.cleansia.partner.ui.theme.LocalDarkTheme
import kotlin.math.roundToInt
import kotlin.math.sqrt

@Composable
internal fun TimerSection(
    estimatedMinutes: Int,
    elapsedSeconds: Long,
    onReportIssue: () -> Unit,
    onAddNote: () -> Unit
) {
    val isDarkTheme = LocalDarkTheme.current
    val density = LocalDensity.current
    val timerSize = 160.dp
    val cutoutGap = 7.dp
    val cutoutRadius = timerSize / 2 + cutoutGap
    val topPartHeight = 135.dp
    val buttonHeight = 64.dp
    val buttonSpacing = 10.dp

    val bgGradientStart = if (isDarkTheme) Color(0xFF1E293B) else Color(0xFFE0F2FE)
    val bgGradientEnd = if (isDarkTheme) Color(0xFF0F172A) else Color(0xFFF0F9FF)
    val iconColor = if (isDarkTheme) Color(0xFF7DD3FC) else Color(0xFF0284C7)
    val titleColor = if (isDarkTheme) Color(0xFFE0F2FE) else Color(0xFF0C4A6E)

    val timerCenterY = topPartHeight
    val timerTopY = timerCenterY - timerSize / 2

    val buttonsTopY = topPartHeight + buttonSpacing

    val totalHeight = buttonsTopY + buttonHeight

    val cutoutRadiusPx = with(density) { cutoutRadius.toPx() }
    val timerCenterYPx = with(density) { timerCenterY.toPx() }

    val timerCenterYRelativeToButtons = timerCenterY - buttonsTopY
    val timerCenterYRelativeToButtonsPx = with(density) { timerCenterYRelativeToButtons.toPx() }

    val buttonMidY = buttonHeight / 2
    val dy = buttonMidY - timerCenterYRelativeToButtons
    val rVal = cutoutRadius.value
    val dyVal = dy.value
    val chordSquared = rVal * rVal - dyVal * dyVal
    val circleHalfChordAtMid = if (chordSquared > 0) sqrt(chordSquared).dp else 0.dp

    Box(
        modifier = Modifier
            .fillMaxWidth()
            .height(totalHeight)
    ) {
        Box(
            modifier = Modifier
                .fillMaxWidth()
                .height(topPartHeight)
                .zIndex(1f)
                .drawWithContent {
                    val centerX = size.width / 2f
                    val cutoutPath = Path().apply {
                        fillType = PathFillType.EvenOdd
                        addRect(Rect(0f, 0f, size.width, size.height))
                        addOval(
                            Rect(
                                centerX - cutoutRadiusPx,
                                timerCenterYPx - cutoutRadiusPx,
                                centerX + cutoutRadiusPx,
                                timerCenterYPx + cutoutRadiusPx
                            )
                        )
                    }
                    clipPath(cutoutPath) {
                        this@drawWithContent.drawContent()
                    }
                }
        ) {
            Box(
                modifier = Modifier
                    .fillMaxSize()
                    .clip(RoundedCornerShape(12.dp))
                    .background(
                        Brush.linearGradient(
                            colors = listOf(bgGradientStart, bgGradientEnd)
                        )
                    )
            ) {
                DynamicCleaningBackground(
                    modifier = Modifier.fillMaxSize(),
                    iconColor = iconColor,
                    iconAlpha = if (isDarkTheme) 0.15f else 0.22f
                )

                Text(
                    text = stringResource(R.string.timer_cleaning_in_progress),
                    style = MaterialTheme.typography.titleMedium,
                    fontWeight = FontWeight.SemiBold,
                    color = titleColor,
                    modifier = Modifier
                        .align(Alignment.TopCenter)
                        .padding(top = 14.dp)
                )
            }
        }

        Box(
            modifier = Modifier
                .fillMaxWidth()
                .height(buttonHeight + 8.dp)
                .offset(y = buttonsTopY)
                .zIndex(0f)
                .graphicsLayer { clip = false }
                .drawWithContent {
                    val centerX = size.width / 2f
                    val shadowOverflow = 24.dp.toPx()
                    val buttonsCutoutPath = Path().apply {
                        fillType = PathFillType.EvenOdd
                        addRect(Rect(-shadowOverflow, -shadowOverflow, size.width + shadowOverflow, size.height + shadowOverflow))
                        addOval(
                            Rect(
                                centerX - cutoutRadiusPx,
                                timerCenterYRelativeToButtonsPx - cutoutRadiusPx,
                                centerX + cutoutRadiusPx,
                                timerCenterYRelativeToButtonsPx + cutoutRadiusPx
                            )
                        )
                    }
                    clipPath(buttonsCutoutPath) {
                        this@drawWithContent.drawContent()
                    }
                }
        ) {
            Row(
                modifier = Modifier
                    .fillMaxWidth()
                    .height(buttonHeight),
                horizontalArrangement = Arrangement.spacedBy(8.dp)
            ) {
                TimerActionButton(
                    icon = Icons.Default.Warning,
                    label = stringResource(R.string.report_issue),
                    onClick = onReportIssue,
                    isWarning = true,
                    modifier = Modifier.weight(1f).fillMaxHeight(),
                    side = ButtonSide.LEFT,
                    circleHalfChordAtMid = circleHalfChordAtMid,
                    gapBetweenButtons = 8.dp
                )

                TimerActionButton(
                    icon = Icons.Default.Notes,
                    label = stringResource(R.string.add_note),
                    onClick = onAddNote,
                    modifier = Modifier.weight(1f).fillMaxHeight(),
                    side = ButtonSide.RIGHT,
                    circleHalfChordAtMid = circleHalfChordAtMid,
                    gapBetweenButtons = 8.dp
                )
            }
        }

        Box(
            modifier = Modifier
                .align(Alignment.TopCenter)
                .offset(y = timerTopY)
                .zIndex(2f)
                .size(timerSize)
                .shadow(
                    elevation = 4.dp,
                    shape = CircleShape
                ),
            contentAlignment = Alignment.Center
        ) {
            CleaningCountdownTimer(
                estimatedMinutes = estimatedMinutes,
                elapsedSeconds = elapsedSeconds,
                size = timerSize,
                strokeWidth = 10.dp
            )
        }
    }
}

@Composable
internal fun TimerActionButton(
    icon: ImageVector,
    label: String,
    onClick: () -> Unit,
    modifier: Modifier = Modifier,
    isWarning: Boolean = false,
    side: ButtonSide = ButtonSide.LEFT,
    circleHalfChordAtMid: Dp = 0.dp,
    gapBetweenButtons: Dp = 8.dp
) {
    val backgroundColor = if (isWarning) {
        MaterialTheme.colorScheme.errorContainer
    } else {
        MaterialTheme.colorScheme.primaryContainer
    }

    val contentColor = if (isWarning) {
        MaterialTheme.colorScheme.onErrorContainer
    } else {
        MaterialTheme.colorScheme.onPrimaryContainer
    }

    val density = LocalDensity.current

    val intrusionIntoButton = (circleHalfChordAtMid - gapBetweenButtons / 2).coerceAtLeast(0.dp)

    val offsetXPx = with(density) {
        val halfIntrusionPx = intrusionIntoButton.toPx() / 2f
        when (side) {
            ButtonSide.LEFT -> -halfIntrusionPx.roundToInt()
            ButtonSide.RIGHT -> halfIntrusionPx.roundToInt()
        }
    }

    Box(
        modifier = modifier
            .shadow(
                elevation = 4.dp,
                shape = RoundedCornerShape(12.dp)
            )
            .clip(RoundedCornerShape(12.dp))
            .background(backgroundColor)
            .clickableWithHaptic(onClick = onClick),
        contentAlignment = Alignment.Center
    ) {
        Column(
            horizontalAlignment = Alignment.CenterHorizontally,
            verticalArrangement = Arrangement.Center,
            modifier = Modifier
                .offset { IntOffset(x = offsetXPx, y = 0) }
                .padding(vertical = 8.dp)
        ) {
            Icon(
                imageVector = icon,
                contentDescription = label,
                modifier = Modifier.size(22.dp),
                tint = contentColor
            )
            Text(
                text = label,
                style = MaterialTheme.typography.labelSmall,
                fontWeight = FontWeight.Medium,
                color = contentColor,
                textAlign = TextAlign.Center,
                maxLines = 1,
                overflow = TextOverflow.Ellipsis
            )
        }
    }
}

internal enum class ButtonSide { LEFT, RIGHT }
