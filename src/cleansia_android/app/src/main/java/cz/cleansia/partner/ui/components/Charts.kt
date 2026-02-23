package cz.cleansia.partner.ui.components

import androidx.compose.animation.core.animateFloatAsState
import androidx.compose.animation.core.tween
import androidx.compose.foundation.Canvas
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
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableFloatStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.geometry.Size
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.StrokeCap
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.dp
import cz.cleansia.partner.ui.theme.CleansiaColors

/**
 * Data for a single bar in the bar chart
 */
data class BarChartData(
    val label: String,
    val value: Float,
    val color: Color
)

/**
 * A simple horizontal bar chart component
 */
@Composable
fun HorizontalBarChart(
    data: List<BarChartData>,
    modifier: Modifier = Modifier,
    barHeight: Dp = 24.dp,
    barSpacing: Dp = 12.dp,
    valueFormatter: ((Float) -> String)? = null
) {
    val maxValue = data.maxOfOrNull { it.value } ?: 1f

    Column(
        modifier = modifier.fillMaxWidth(),
        verticalArrangement = Arrangement.spacedBy(barSpacing)
    ) {
        data.forEach { item ->
            var animationPlayed by remember { mutableFloatStateOf(0f) }
            val animatedWidth by animateFloatAsState(
                targetValue = animationPlayed,
                animationSpec = tween(durationMillis = 500),
                label = "barWidth"
            )

            LaunchedEffect(item.value) {
                animationPlayed = if (maxValue > 0) item.value / maxValue else 0f
            }

            Column {
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.SpaceBetween,
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    Text(
                        text = item.label,
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                    Text(
                        text = valueFormatter?.invoke(item.value) ?: String.format("%.0f", item.value),
                        style = MaterialTheme.typography.labelMedium,
                        fontWeight = FontWeight.Medium,
                        color = MaterialTheme.colorScheme.onSurface
                    )
                }
                Spacer(modifier = Modifier.height(4.dp))
                Box(
                    modifier = Modifier
                        .fillMaxWidth()
                        .height(barHeight)
                        .clip(RoundedCornerShape(barHeight / 2))
                        .background(item.color.copy(alpha = 0.15f))
                ) {
                    Box(
                        modifier = Modifier
                            .fillMaxWidth(animatedWidth)
                            .height(barHeight)
                            .clip(RoundedCornerShape(barHeight / 2))
                            .background(item.color)
                    )
                }
            }
        }
    }
}

/**
 * Data for the donut/pie chart
 */
data class DonutChartData(
    val label: String,
    val value: Float,
    val color: Color
)

/**
 * A donut chart component for showing distribution
 */
@Composable
fun DonutChart(
    data: List<DonutChartData>,
    modifier: Modifier = Modifier,
    strokeWidth: Dp = 20.dp,
    centerContent: @Composable () -> Unit = {}
) {
    val total = data.sumOf { it.value.toDouble() }.toFloat()
    var animationPlayed by remember { mutableFloatStateOf(0f) }
    val sweepAnimation by animateFloatAsState(
        targetValue = animationPlayed,
        animationSpec = tween(durationMillis = 800),
        label = "sweepAnimation"
    )

    LaunchedEffect(Unit) {
        animationPlayed = 1f
    }

    Box(
        modifier = modifier,
        contentAlignment = Alignment.Center
    ) {
        Canvas(modifier = Modifier.matchParentSize()) {
            val strokePx = strokeWidth.toPx()
            val diameter = minOf(size.width, size.height) - strokePx
            val topLeft = Offset(
                (size.width - diameter) / 2,
                (size.height - diameter) / 2
            )

            var startAngle = -90f
            data.forEach { item ->
                val sweepAngle = if (total > 0) (item.value / total) * 360f * sweepAnimation else 0f
                drawArc(
                    color = item.color,
                    startAngle = startAngle,
                    sweepAngle = sweepAngle,
                    useCenter = false,
                    topLeft = topLeft,
                    size = Size(diameter, diameter),
                    style = Stroke(width = strokePx, cap = StrokeCap.Round)
                )
                startAngle += sweepAngle
            }
        }

        centerContent()
    }
}

/**
 * A legend item for charts
 */
@Composable
fun ChartLegendItem(
    color: Color,
    label: String,
    value: String,
    modifier: Modifier = Modifier
) {
    Row(
        modifier = modifier,
        verticalAlignment = Alignment.CenterVertically
    ) {
        Box(
            modifier = Modifier
                .size(10.dp)
                .clip(CircleShape)
                .background(color)
        )
        Spacer(modifier = Modifier.width(8.dp))
        Text(
            text = label,
            style = MaterialTheme.typography.bodySmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
            modifier = Modifier.weight(1f)
        )
        Text(
            text = value,
            style = MaterialTheme.typography.labelMedium,
            fontWeight = FontWeight.Medium,
            color = MaterialTheme.colorScheme.onSurface
        )
    }
}

/**
 * A progress arc/gauge component
 */
@Composable
fun ProgressGauge(
    progress: Float,
    modifier: Modifier = Modifier,
    strokeWidth: Dp = 12.dp,
    progressColor: Color = CleansiaColors.success,
    trackColor: Color = MaterialTheme.colorScheme.surfaceVariant,
    centerContent: @Composable () -> Unit = {}
) {
    var animationPlayed by remember { mutableFloatStateOf(0f) }
    val animatedProgress by animateFloatAsState(
        targetValue = animationPlayed,
        animationSpec = tween(durationMillis = 800),
        label = "gaugeProgress"
    )

    LaunchedEffect(progress) {
        animationPlayed = progress.coerceIn(0f, 1f)
    }

    Box(
        modifier = modifier,
        contentAlignment = Alignment.Center
    ) {
        Canvas(modifier = Modifier.matchParentSize()) {
            val strokePx = strokeWidth.toPx()
            val diameter = minOf(size.width, size.height) - strokePx
            val topLeft = Offset(
                (size.width - diameter) / 2,
                (size.height - diameter) / 2
            )

            // Track
            drawArc(
                color = trackColor,
                startAngle = 135f,
                sweepAngle = 270f,
                useCenter = false,
                topLeft = topLeft,
                size = Size(diameter, diameter),
                style = Stroke(width = strokePx, cap = StrokeCap.Round)
            )

            // Progress
            drawArc(
                color = progressColor,
                startAngle = 135f,
                sweepAngle = 270f * animatedProgress,
                useCenter = false,
                topLeft = topLeft,
                size = Size(diameter, diameter),
                style = Stroke(width = strokePx, cap = StrokeCap.Round)
            )
        }

        centerContent()
    }
}

/**
 * Mini sparkline chart for showing trends
 */
@Composable
fun SparklineChart(
    data: List<Float>,
    modifier: Modifier = Modifier,
    lineColor: Color = MaterialTheme.colorScheme.primary,
    fillColor: Color = MaterialTheme.colorScheme.primary.copy(alpha = 0.1f)
) {
    if (data.isEmpty()) return

    val maxValue = data.maxOrNull() ?: 1f
    val minValue = data.minOrNull() ?: 0f
    val range = (maxValue - minValue).coerceAtLeast(1f)

    Canvas(modifier = modifier) {
        if (data.size < 2) return@Canvas

        val stepX = size.width / (data.size - 1)
        val points = data.mapIndexed { index, value ->
            Offset(
                x = index * stepX,
                y = size.height - ((value - minValue) / range) * size.height * 0.8f - size.height * 0.1f
            )
        }

        // Draw fill
        val fillPath = androidx.compose.ui.graphics.Path().apply {
            moveTo(0f, size.height)
            points.forEach { point ->
                lineTo(point.x, point.y)
            }
            lineTo(size.width, size.height)
            close()
        }
        drawPath(fillPath, fillColor)

        // Draw line
        for (i in 0 until points.size - 1) {
            drawLine(
                color = lineColor,
                start = points[i],
                end = points[i + 1],
                strokeWidth = 2.dp.toPx(),
                cap = StrokeCap.Round
            )
        }
    }
}

/**
 * A simple stats card with a mini chart
 */
@Composable
fun StatsCardWithChart(
    title: String,
    value: String,
    chartData: List<Float>,
    chartColor: Color = MaterialTheme.colorScheme.primary,
    modifier: Modifier = Modifier
) {
    Card(
        modifier = modifier,
        colors = CardDefaults.cardColors(
            containerColor = MaterialTheme.colorScheme.surface
        ),
        elevation = CardDefaults.cardElevation(defaultElevation = 1.dp)
    ) {
        Column(
            modifier = Modifier.padding(16.dp)
        ) {
            Text(
                text = title,
                style = MaterialTheme.typography.labelMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant
            )
            Spacer(modifier = Modifier.height(8.dp))
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.Bottom
            ) {
                Text(
                    text = value,
                    style = MaterialTheme.typography.headlineSmall,
                    fontWeight = FontWeight.Bold,
                    color = MaterialTheme.colorScheme.onSurface
                )
                SparklineChart(
                    data = chartData,
                    lineColor = chartColor,
                    fillColor = chartColor.copy(alpha = 0.1f),
                    modifier = Modifier
                        .width(80.dp)
                        .height(32.dp)
                )
            }
        }
    }
}
