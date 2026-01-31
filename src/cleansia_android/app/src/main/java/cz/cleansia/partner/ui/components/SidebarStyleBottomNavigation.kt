package cz.cleansia.partner.ui.components

import androidx.compose.animation.animateColorAsState
import androidx.compose.animation.core.RepeatMode
import androidx.compose.animation.core.Spring
import androidx.compose.animation.core.animateDpAsState
import androidx.compose.animation.core.animateFloat
import androidx.compose.animation.core.animateFloatAsState
import androidx.compose.animation.core.infiniteRepeatable
import androidx.compose.animation.core.rememberInfiniteTransition
import androidx.compose.animation.core.spring
import androidx.compose.animation.core.tween
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.interaction.MutableInteractionSource
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.navigationBarsPadding
import androidx.compose.foundation.layout.offset
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.ripple
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.remember
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.draw.shadow
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.graphicsLayer
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp

/**
 * Bottom navigation bar styled to match the frontend sidebar menu design.
 * Features: glassmorphic container, gradient active states, icon wrappers
 * with colored backgrounds, bottom accent bar, and icon pulse animation.
 */
@Composable
fun SidebarStyleBottomNavigation(
    items: List<FloatingNavItem>,
    modifier: Modifier = Modifier
) {
    val containerShape = RoundedCornerShape(20.dp)

    // Glassmorphic container - white bg, subtle border, shadow
    Surface(
        modifier = modifier
            .navigationBarsPadding()
            .padding(horizontal = 16.dp, vertical = 10.dp)
            .shadow(
                elevation = 12.dp,
                shape = containerShape,
                ambientColor = Color.Black.copy(alpha = 0.08f),
                spotColor = Color.Black.copy(alpha = 0.12f)
            ),
        shape = containerShape,
        color = Color.White,
        tonalElevation = 0.dp
    ) {
        Box(
            modifier = Modifier
                .border(
                    width = 1.dp,
                    color = Color(0xFFE5E7EB),
                    shape = containerShape
                )
        ) {
            Row(
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(horizontal = 8.dp, vertical = 6.dp),
                horizontalArrangement = Arrangement.SpaceEvenly,
                verticalAlignment = Alignment.CenterVertically
            ) {
                items.forEach { item ->
                    SidebarStyleNavItem(
                        item = item,
                        modifier = Modifier.weight(1f)
                    )
                }
            }
        }
    }
}

@Composable
private fun SidebarStyleNavItem(
    item: FloatingNavItem,
    modifier: Modifier = Modifier
) {
    val primaryColor = MaterialTheme.colorScheme.primary
    val primaryContainer = MaterialTheme.colorScheme.primaryContainer
    val onSurfaceVariant = MaterialTheme.colorScheme.onSurfaceVariant

    val itemShape = RoundedCornerShape(12.dp)

    // Animate background - gradient for active, transparent for inactive
    val backgroundAlpha by animateFloatAsState(
        targetValue = if (item.isSelected) 1f else 0f,
        animationSpec = spring(stiffness = Spring.StiffnessLow),
        label = "bgAlpha"
    )

    // Animate icon wrapper background
    val iconWrapperColor by animateColorAsState(
        targetValue = if (item.isSelected) primaryColor else primaryContainer.copy(alpha = 0.7f),
        animationSpec = spring(stiffness = Spring.StiffnessLow),
        label = "iconWrapperColor"
    )

    // Animate icon color
    val iconColor by animateColorAsState(
        targetValue = if (item.isSelected) Color.White else primaryColor.copy(alpha = 0.7f),
        animationSpec = spring(stiffness = Spring.StiffnessLow),
        label = "iconColor"
    )

    // Animate text color
    val textColor by animateColorAsState(
        targetValue = if (item.isSelected) primaryColor else onSurfaceVariant,
        animationSpec = spring(stiffness = Spring.StiffnessLow),
        label = "textColor"
    )

    // Icon pulse animation for active items
    val infiniteTransition = rememberInfiniteTransition(label = "iconPulse")
    val iconScale by infiniteTransition.animateFloat(
        initialValue = 1f,
        targetValue = if (item.isSelected) 1.1f else 1f,
        animationSpec = infiniteRepeatable(
            animation = tween(durationMillis = 2000),
            repeatMode = RepeatMode.Reverse
        ),
        label = "iconPulseScale"
    )

    // Bottom accent bar height
    val accentHeight by animateDpAsState(
        targetValue = if (item.isSelected) 3.dp else 0.dp,
        animationSpec = spring(stiffness = Spring.StiffnessMedium),
        label = "accentHeight"
    )

    // Icon wrapper shadow elevation
    val iconElevation by animateDpAsState(
        targetValue = if (item.isSelected) 4.dp else 0.dp,
        animationSpec = spring(stiffness = Spring.StiffnessLow),
        label = "iconElevation"
    )

    Column(
        modifier = modifier
            .padding(horizontal = 4.dp)
            .clip(itemShape)
            .then(
                if (backgroundAlpha > 0f) {
                    Modifier.background(
                        brush = Brush.linearGradient(
                            colors = listOf(
                                primaryContainer.copy(alpha = 0.4f * backgroundAlpha),
                                primaryContainer.copy(alpha = 0.7f * backgroundAlpha)
                            )
                        ),
                        shape = itemShape
                    )
                } else Modifier
            )
            .clickable(
                role = Role.Tab,
                indication = ripple(bounded = true),
                interactionSource = remember { MutableInteractionSource() }
            ) { item.onClick() }
            .padding(vertical = 8.dp, horizontal = 4.dp),
        horizontalAlignment = Alignment.CenterHorizontally,
        verticalArrangement = Arrangement.Center
    ) {
        // Icon wrapper - rounded box with colored background (like frontend's .menu-item-icon-wrapper)
        Box(
            modifier = Modifier
                .shadow(
                    elevation = iconElevation,
                    shape = RoundedCornerShape(10.dp),
                    ambientColor = primaryColor.copy(alpha = 0.2f),
                    spotColor = primaryColor.copy(alpha = 0.3f)
                )
                .size(36.dp)
                .clip(RoundedCornerShape(10.dp))
                .background(iconWrapperColor),
            contentAlignment = Alignment.Center
        ) {
            Icon(
                imageVector = if (item.isSelected) item.selectedIcon else item.unselectedIcon,
                contentDescription = stringResource(item.titleResId),
                tint = iconColor,
                modifier = Modifier
                    .size(20.dp)
                    .graphicsLayer {
                        scaleX = if (item.isSelected) iconScale else 1f
                        scaleY = if (item.isSelected) iconScale else 1f
                    }
            )
        }

        // Label
        Text(
            text = stringResource(item.titleResId),
            color = textColor,
            style = MaterialTheme.typography.labelSmall,
            fontWeight = if (item.isSelected) FontWeight.SemiBold else FontWeight.Medium,
            fontSize = 10.sp,
            modifier = Modifier.padding(top = 4.dp)
        )

        // Bottom accent bar (equivalent to frontend's left accent bar, adapted to horizontal)
        Box(
            modifier = Modifier
                .padding(top = 4.dp)
                .size(width = 20.dp, height = accentHeight)
                .clip(RoundedCornerShape(2.dp))
                .background(primaryColor)
        )
    }
}
