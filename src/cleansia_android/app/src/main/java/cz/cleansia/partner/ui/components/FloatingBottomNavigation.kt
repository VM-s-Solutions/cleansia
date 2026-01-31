package cz.cleansia.partner.ui.components

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
import androidx.compose.runtime.remember
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.draw.shadow
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.vector.ImageVector
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
    val selectedContentColor = MaterialTheme.colorScheme.primary
    val unselectedContentColor = MaterialTheme.colorScheme.onSurfaceVariant
    val indicatorColor = MaterialTheme.colorScheme.primary

    val itemShape = RoundedCornerShape(20.dp)

    val contentColor = if (item.isSelected) selectedContentColor else unselectedContentColor

    Box(
        modifier = modifier
            .padding(horizontal = 2.dp)
            .clip(itemShape)
            .clickable(
                role = Role.Tab,
                indication = ripple(bounded = true),
                interactionSource = remember { MutableInteractionSource() }
            ) { item.onClick() }
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
                modifier = Modifier.size(20.dp)
            )
            Text(
                text = stringResource(item.titleResId),
                color = contentColor,
                style = MaterialTheme.typography.labelSmall,
                fontWeight = if (item.isSelected) FontWeight.SemiBold else FontWeight.Normal,
                fontSize = 12.sp,
                modifier = Modifier.padding(top = 2.dp)
            )
            if (item.isSelected) {
                Box(
                    modifier = Modifier
                        .padding(top = 4.dp)
                        .size(4.dp)
                        .clip(CircleShape)
                        .background(indicatorColor)
                )
            }
        }
    }
}
