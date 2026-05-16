package cz.cleansia.partner.features.dashboard.components

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.MaterialTheme
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import cz.cleansia.partner.ui.components.ShimmerBox
import cz.cleansia.partner.ui.components.ShimmerCircle
import cz.cleansia.partner.ui.components.rememberShimmerBrush

@Composable
internal fun AnalyticsSkeleton(modifier: Modifier = Modifier) {
    val brush = rememberShimmerBrush()
    Column(
        modifier = modifier,
        verticalArrangement = Arrangement.spacedBy(16.dp)
    ) {
        // Earnings chart card (matches ChartCard: title + amount + 180dp sparkline)
        Card(
            modifier = Modifier.fillMaxWidth(),
            colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.primaryContainer)
        ) {
            Column(modifier = Modifier.padding(16.dp)) {
                ShimmerBox(width = 130.dp, height = 16.dp, brush = brush)
                Spacer(Modifier.height(4.dp))
                ShimmerBox(width = 160.dp, height = 24.dp, brush = brush)
                Spacer(Modifier.height(16.dp))
                ShimmerBox(height = 180.dp, shape = RoundedCornerShape(8.dp), brush = brush)
            }
        }

        // Comparison card (title + row with 2 columns + change badge)
        Card(
            modifier = Modifier.fillMaxWidth(),
            colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
            elevation = CardDefaults.cardElevation(defaultElevation = 1.dp)
        ) {
            Column(modifier = Modifier.padding(16.dp)) {
                ShimmerBox(width = 140.dp, height = 16.dp, brush = brush)
                Spacer(Modifier.height(12.dp))
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.SpaceBetween,
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    Column(modifier = Modifier.weight(1f)) {
                        ShimmerBox(width = 80.dp, height = 10.dp, brush = brush)
                        Spacer(Modifier.height(4.dp))
                        ShimmerBox(width = 90.dp, height = 16.dp, brush = brush)
                    }
                    Column(modifier = Modifier.weight(1f)) {
                        ShimmerBox(width = 80.dp, height = 10.dp, brush = brush)
                        Spacer(Modifier.height(4.dp))
                        ShimmerBox(width = 90.dp, height = 16.dp, brush = brush)
                    }
                    ShimmerBox(width = 56.dp, height = 24.dp, shape = RoundedCornerShape(12.dp), brush = brush)
                }
            }
        }

        // Stats grid — 2 rows of 2 mini cards (matches StatsRow)
        repeat(2) {
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.spacedBy(12.dp)
            ) {
                repeat(2) {
                    Card(
                        modifier = Modifier.weight(1f),
                        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
                        elevation = CardDefaults.cardElevation(defaultElevation = 1.dp)
                    ) {
                        Column(
                            modifier = Modifier
                                .fillMaxWidth()
                                .padding(10.dp),
                            horizontalAlignment = Alignment.CenterHorizontally
                        ) {
                            ShimmerBox(width = 80.dp, height = 14.dp, brush = brush)
                            Spacer(Modifier.height(4.dp))
                            ShimmerBox(width = 50.dp, height = 10.dp, brush = brush)
                        }
                    }
                }
            }
        }

        // Day-of-week earnings card (title + 7 bar rows)
        Card(
            modifier = Modifier.fillMaxWidth(),
            colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
            elevation = CardDefaults.cardElevation(defaultElevation = 1.dp)
        ) {
            Column(modifier = Modifier.padding(16.dp)) {
                ShimmerBox(width = 140.dp, height = 16.dp, brush = brush)
                Spacer(Modifier.height(16.dp))
                repeat(7) {
                    Row(
                        modifier = Modifier
                            .fillMaxWidth()
                            .padding(vertical = 3.dp),
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        ShimmerBox(width = 28.dp, height = 12.dp, brush = brush)
                        Spacer(Modifier.width(8.dp))
                        ShimmerBox(
                            height = 20.dp,
                            shape = RoundedCornerShape(4.dp),
                            brush = brush,
                            modifier = Modifier.weight(1f)
                        )
                        Spacer(Modifier.width(8.dp))
                        ShimmerBox(width = 60.dp, height = 12.dp, brush = brush)
                    }
                }
            }
        }

        // Order status donut card (title + circle + legend)
        Card(
            modifier = Modifier.fillMaxWidth(),
            colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
            elevation = CardDefaults.cardElevation(defaultElevation = 1.dp)
        ) {
            Column(
                modifier = Modifier.padding(16.dp),
                horizontalAlignment = Alignment.CenterHorizontally
            ) {
                ShimmerBox(
                    width = 140.dp, height = 16.dp, brush = brush,
                    modifier = Modifier.align(Alignment.Start)
                )
                Spacer(Modifier.height(16.dp))
                ShimmerCircle(size = 160.dp, brush = brush)
                Spacer(Modifier.height(16.dp))
                repeat(4) {
                    Row(
                        modifier = Modifier.fillMaxWidth().padding(vertical = 2.dp),
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        ShimmerCircle(size = 10.dp, brush = brush)
                        Spacer(Modifier.width(8.dp))
                        ShimmerBox(width = 80.dp, height = 12.dp, brush = brush)
                        Spacer(Modifier.weight(1f))
                        ShimmerBox(width = 24.dp, height = 12.dp, brush = brush)
                    }
                }
            }
        }

        // Performance score card (title + gauge + 3 metrics)
        Card(
            modifier = Modifier.fillMaxWidth(),
            colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
            elevation = CardDefaults.cardElevation(defaultElevation = 1.dp)
        ) {
            Column(
                modifier = Modifier.padding(16.dp),
                horizontalAlignment = Alignment.CenterHorizontally
            ) {
                ShimmerBox(
                    width = 140.dp, height = 16.dp, brush = brush,
                    modifier = Modifier.align(Alignment.Start)
                )
                Spacer(Modifier.height(16.dp))
                ShimmerCircle(size = 160.dp, brush = brush)
                Spacer(Modifier.height(20.dp))
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.SpaceEvenly
                ) {
                    repeat(3) {
                        Column(horizontalAlignment = Alignment.CenterHorizontally) {
                            ShimmerCircle(size = 20.dp, brush = brush)
                            Spacer(Modifier.height(4.dp))
                            ShimmerBox(width = 36.dp, height = 14.dp, brush = brush)
                            Spacer(Modifier.height(2.dp))
                            ShimmerBox(width = 50.dp, height = 10.dp, brush = brush)
                        }
                    }
                }
            }
        }
    }
}
