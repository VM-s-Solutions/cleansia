package cz.cleansia.partner.features.dashboard.components

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowForward
import androidx.compose.material.icons.filled.KeyboardArrowDown
import androidx.compose.material.icons.filled.KeyboardArrowUp
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.remember
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import cz.cleansia.partner.R
import cz.cleansia.partner.core.utils.CurrencyUtils
import cz.cleansia.partner.ui.components.SparklineChart
import cz.cleansia.partner.ui.components.clickableWithHaptic
import cz.cleansia.partner.ui.theme.LocalDarkTheme

@Composable
internal fun EarningsOverviewCard(
    thisWeek: Double,
    thisMonth: Double,
    lastMonth: Double,
    currency: String,
    onClick: () -> Unit = {}
) {
    val isDarkTheme = LocalDarkTheme.current

    // Generate sparkline data based on the earnings values
    val sparklineData = remember(thisWeek, thisMonth, lastMonth) {
        listOf(
            lastMonth.toFloat() * 0.7f,
            lastMonth.toFloat() * 0.85f,
            lastMonth.toFloat(),
            thisMonth.toFloat() * 0.6f,
            thisMonth.toFloat() * 0.8f,
            thisMonth.toFloat()
        )
    }

    // Month-over-month trend
    val trendPercentage = remember(thisMonth, lastMonth) {
        if (lastMonth > 0) ((thisMonth - lastMonth) / lastMonth * 100) else 0.0
    }
    val trendIsUp = trendPercentage >= 0

    // Theme-aware colors
    val gradientStart = if (isDarkTheme) Color(0xFF0C4A6E) else Color(0xFFE0F2FE)
    val gradientEnd = if (isDarkTheme) Color(0xFF164E63) else Color(0xFFF0F9FF)
    val textColor = if (isDarkTheme) Color.White else Color(0xFF0C4A6E)
    val mutedTextColor = if (isDarkTheme) Color.White.copy(alpha = 0.7f) else Color(0xFF0369A1)
    val sparklineColor = if (isDarkTheme) Color.White.copy(alpha = 0.85f) else Color(0xFF0284C7)
    val sparklineFill = if (isDarkTheme) Color.White.copy(alpha = 0.12f) else Color(0xFF0284C7).copy(alpha = 0.15f)
    val badgeBgUp = if (isDarkTheme) Color.White.copy(alpha = 0.15f) else Color(0xFF16A34A).copy(alpha = 0.1f)
    val badgeBgDown = if (isDarkTheme) Color(0xFFEF4444).copy(alpha = 0.2f) else Color(0xFFEF4444).copy(alpha = 0.1f)
    val badgeTextUp = if (isDarkTheme) Color(0xFF86EFAC) else Color(0xFF16A34A)
    val badgeTextDown = if (isDarkTheme) Color(0xFFFCA5A5) else Color(0xFFEF4444)
    val arrowBg = if (isDarkTheme) Color.White.copy(alpha = 0.15f) else Color(0xFF0284C7).copy(alpha = 0.12f)
    val arrowTint = if (isDarkTheme) Color.White else Color(0xFF0369A1)

    Card(
        modifier = Modifier
            .fillMaxWidth()
            .clickableWithHaptic { onClick() },
        shape = RoundedCornerShape(16.dp),
        colors = CardDefaults.cardColors(containerColor = Color.Transparent)
    ) {
        Box(
            modifier = Modifier
                .fillMaxWidth()
                .background(
                    Brush.linearGradient(
                        colors = listOf(gradientStart, gradientEnd)
                    )
                )
        ) {
            Column(
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(20.dp)
            ) {
                // Top row: label + trend badge
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.SpaceBetween,
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    Text(
                        text = stringResource(R.string.earnings_overview),
                        style = MaterialTheme.typography.labelMedium,
                        color = mutedTextColor,
                        letterSpacing = 0.5.sp
                    )

                    // Trend badge
                    if (lastMonth > 0) {
                        Row(
                            verticalAlignment = Alignment.CenterVertically,
                            modifier = Modifier
                                .background(
                                    color = if (trendIsUp) badgeBgUp else badgeBgDown,
                                    shape = RoundedCornerShape(8.dp)
                                )
                                .padding(horizontal = 8.dp, vertical = 4.dp)
                        ) {
                            Icon(
                                imageVector = if (trendIsUp) Icons.Default.KeyboardArrowUp
                                else Icons.Default.KeyboardArrowDown,
                                contentDescription = null,
                                tint = if (trendIsUp) badgeTextUp else badgeTextDown,
                                modifier = Modifier.size(14.dp)
                            )
                            Text(
                                text = "${String.format("%.1f", kotlin.math.abs(trendPercentage))}%",
                                style = MaterialTheme.typography.labelSmall,
                                fontWeight = FontWeight.SemiBold,
                                color = if (trendIsUp) badgeTextUp else badgeTextDown
                            )
                        }
                    }
                }

                Spacer(modifier = Modifier.height(8.dp))

                // Amount
                Text(
                    text = CurrencyUtils.formatCurrency(thisMonth, currency),
                    style = MaterialTheme.typography.headlineMedium,
                    fontWeight = FontWeight.Bold,
                    color = textColor
                )

                Spacer(modifier = Modifier.height(2.dp))

                Text(
                    text = stringResource(R.string.this_month),
                    style = MaterialTheme.typography.bodySmall,
                    color = mutedTextColor
                )

                Spacer(modifier = Modifier.height(16.dp))

                // Bottom row: sparkline + arrow
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.SpaceBetween,
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    SparklineChart(
                        data = sparklineData,
                        lineColor = sparklineColor,
                        fillColor = sparklineFill,
                        modifier = Modifier
                            .weight(1f)
                            .height(44.dp)
                    )

                    Spacer(modifier = Modifier.width(16.dp))

                    Box(
                        modifier = Modifier
                            .size(36.dp)
                            .background(
                                color = arrowBg,
                                shape = CircleShape
                            ),
                        contentAlignment = Alignment.Center
                    ) {
                        Icon(
                            imageVector = Icons.AutoMirrored.Filled.ArrowForward,
                            contentDescription = null,
                            tint = arrowTint,
                            modifier = Modifier.size(18.dp)
                        )
                    }
                }
            }
        }
    }
}
