package cz.cleansia.partner.features.orders.components

import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.CalendarViewWeek
import androidx.compose.material.icons.filled.ViewList
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.unit.dp
import cz.cleansia.partner.R
import cz.cleansia.partner.features.orders.viewmodels.OrdersViewMode

@Composable
fun ViewModeToggle(
    currentMode: OrdersViewMode,
    onModeChange: (OrdersViewMode) -> Unit,
    modifier: Modifier = Modifier
) {
    val shape = RoundedCornerShape(8.dp)

    Row(
        modifier = modifier
            .clip(shape)
            .background(MaterialTheme.colorScheme.surfaceVariant)
    ) {
        // List mode
        Icon(
            imageVector = Icons.Default.ViewList,
            contentDescription = stringResource(R.string.view_mode_list),
            tint = if (currentMode == OrdersViewMode.LIST)
                MaterialTheme.colorScheme.onPrimary
            else MaterialTheme.colorScheme.onSurfaceVariant,
            modifier = Modifier
                .clip(shape)
                .background(
                    if (currentMode == OrdersViewMode.LIST)
                        MaterialTheme.colorScheme.primary
                    else MaterialTheme.colorScheme.surfaceVariant
                )
                .clickable { onModeChange(OrdersViewMode.LIST) }
                .padding(8.dp)
                .size(20.dp)
        )

        // Week mode
        Icon(
            imageVector = Icons.Default.CalendarViewWeek,
            contentDescription = stringResource(R.string.view_mode_week),
            tint = if (currentMode == OrdersViewMode.WEEK)
                MaterialTheme.colorScheme.onPrimary
            else MaterialTheme.colorScheme.onSurfaceVariant,
            modifier = Modifier
                .clip(shape)
                .background(
                    if (currentMode == OrdersViewMode.WEEK)
                        MaterialTheme.colorScheme.primary
                    else MaterialTheme.colorScheme.surfaceVariant
                )
                .clickable { onModeChange(OrdersViewMode.WEEK) }
                .padding(8.dp)
                .size(20.dp)
        )
    }
}
