package cz.cleansia.partner.ui.components

import android.view.HapticFeedbackConstants
import androidx.compose.animation.AnimatedVisibility
import androidx.compose.animation.animateColorAsState
import androidx.compose.animation.core.animateFloatAsState
import androidx.compose.animation.core.tween
import androidx.compose.animation.fadeIn
import androidx.compose.animation.fadeOut
import androidx.compose.animation.scaleIn
import androidx.compose.animation.scaleOut
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.interaction.MutableInteractionSource
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.navigationBarsPadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.material3.ripple
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.remember
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.draw.scale
import androidx.compose.ui.draw.shadow
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.platform.LocalView
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp

data class FloatingNavItem(
    val titleResId: Int,
    val selectedIcon: ImageVector,
    val unselectedIcon: ImageVector,
    val isSelected: Boolean,
    val onClick: () -> Unit
)

@Composable
fun FloatingBottomNavigation(
    items: List<FloatingNavItem>,
    modifier: Modifier = Modifier
) {
    val containerShape = RoundedCornerShape(28.dp)

    Box(
        modifier = modifier
            .navigationBarsPadding()
            .padding(horizontal = 24.dp, vertical = 16.dp)
            .shadow(
                elevation = 2.5.dp,
                shape = containerShape,
                ambientColor = Color.Black.copy(alpha = 1f),
                spotColor = Color.Black.copy(alpha = 0.85f)
            )
            .clip(containerShape)
            .background(MaterialTheme.colorScheme.surface)
            .border(
                width = 0.15.dp,
                color = MaterialTheme.colorScheme.outline.copy(alpha = 1f),
                shape = containerShape
            )
    ) {
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .padding(horizontal = 6.dp, vertical = 6.dp),
            horizontalArrangement = Arrangement.SpaceEvenly,
            verticalAlignment = Alignment.CenterVertically
        ) {
            items.forEach { item ->
                FloatingNavItemView(
                    item = item,
                    modifier = Modifier.weight(1f)
                )
            }
        }
    }
}

@Composable
private fun FloatingNavItemView(
    item: FloatingNavItem,
    modifier: Modifier = Modifier
) {
    val view = LocalView.current

    val selectedContentColor = MaterialTheme.colorScheme.primary
    val unselectedContentColor = MaterialTheme.colorScheme.onSurfaceVariant
    val indicatorColor = MaterialTheme.colorScheme.primary

    val itemShape = RoundedCornerShape(20.dp)

    val contentColor by animateColorAsState(
        targetValue = if (item.isSelected) selectedContentColor else unselectedContentColor,
        animationSpec = tween(300),
        label = "navColor"
    )

    val iconScale by animateFloatAsState(
        targetValue = if (item.isSelected) 1.1f else 1.0f,
        animationSpec = tween(200),
        label = "iconScale"
    )

    Box(
        modifier = modifier
            .padding(horizontal = 2.dp)
            .clip(itemShape)
            .clickable(
                role = Role.Tab,
                indication = ripple(bounded = true),
                interactionSource = remember { MutableInteractionSource() }
            ) {
                view.performHapticFeedback(HapticFeedbackConstants.CLOCK_TICK)
                item.onClick()
            }
            .padding(vertical = 8.dp),
        contentAlignment = Alignment.Center
    ) {
        Column(
            horizontalAlignment = Alignment.CenterHorizontally,
            verticalArrangement = Arrangement.Center
        ) {
            Icon(
                imageVector = if (item.isSelected) item.selectedIcon else item.unselectedIcon,
                contentDescription = stringResource(item.titleResId),
                tint = contentColor,
                modifier = Modifier
                    .size(20.dp)
                    .scale(iconScale)
            )
            Text(
                text = stringResource(item.titleResId),
                color = contentColor,
                style = MaterialTheme.typography.labelSmall,
                fontWeight = if (item.isSelected) FontWeight.SemiBold else FontWeight.Normal,
                fontSize = 12.sp,
                modifier = Modifier.padding(top = 2.dp)
            )
            AnimatedVisibility(
                visible = item.isSelected,
                enter = scaleIn(tween(200)) + fadeIn(tween(200)),
                exit = scaleOut(tween(150)) + fadeOut(tween(150))
            ) {
                Box(
                    modifier = Modifier
                        .padding(top = 3.dp)
                        .size(4.dp)
                        .clip(CircleShape)
                        .background(indicatorColor)
                )
            }
        }
    }
}
