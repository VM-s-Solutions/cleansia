package cz.cleansia.partner.features.dashboard.components

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import cz.cleansia.partner.R
import cz.cleansia.partner.ui.theme.LocalDarkTheme

@Composable
internal fun GreetingHero(
    greetingText: String,
    activeOrders: Int,
    availableOrders: Int
) {
    val isDarkTheme = LocalDarkTheme.current
    val bgGradientStart = if (isDarkTheme) Color(0xFF1E293B) else Color(0xFFE0F2FE)
    val bgGradientEnd = if (isDarkTheme) Color(0xFF0F172A) else Color(0xFFF0F9FF)
    val textColor = if (isDarkTheme) Color(0xFFE0F2FE) else Color(0xFF0C4A6E)
    val subtextColor = if (isDarkTheme) Color(0xFF94A3B8) else Color(0xFF475569)

    Box(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(16.dp))
            .background(
                Brush.linearGradient(
                    colors = listOf(bgGradientStart, bgGradientEnd)
                )
            )
            .padding(20.dp)
    ) {
        Column {
            Text(
                text = greetingText,
                style = MaterialTheme.typography.headlineSmall,
                fontWeight = FontWeight.Bold,
                color = textColor
            )
            Spacer(modifier = Modifier.height(4.dp))
            Text(
                text = stringResource(R.string.dashboard_subtitle),
                style = MaterialTheme.typography.bodyMedium,
                color = subtextColor
            )
        }
    }
}
