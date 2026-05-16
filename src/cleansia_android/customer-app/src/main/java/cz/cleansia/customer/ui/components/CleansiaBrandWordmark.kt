package cz.cleansia.customer.ui.components

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.size
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.CleaningServices
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import cz.cleansia.core.ui.theme.Poppins

/** "Cleansia" wordmark with brand mark — used in top app bars, login header. */
@Composable
fun CleansiaBrandWordmark(
    modifier: Modifier = Modifier,
    fontSize: Int = 22,
) {
    Row(
        modifier = modifier,
        verticalAlignment = Alignment.CenterVertically,
        horizontalArrangement = Arrangement.spacedBy(8.dp),
    ) {
        Icon(
            Icons.Outlined.CleaningServices,
            contentDescription = null,
            tint = MaterialTheme.colorScheme.primary,
            modifier = Modifier.size((fontSize * 0.9).dp),
        )
        Text(
            text = "Cleansia",
            fontFamily = Poppins,
            fontWeight = FontWeight.Bold,
            fontSize = fontSize.sp,
            color = MaterialTheme.colorScheme.primary,
        )
    }
}
