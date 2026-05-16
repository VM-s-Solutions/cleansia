package cz.cleansia.partner.features.dashboard.components

import androidx.compose.foundation.horizontalScroll
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.MaterialTheme
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.unit.dp
import cz.cleansia.partner.ui.components.ShimmerBox
import cz.cleansia.partner.ui.components.ShimmerCircle
import cz.cleansia.partner.ui.components.rememberShimmerBrush

@Composable
internal fun DashboardSkeleton(modifier: Modifier = Modifier) {
    val brush = rememberShimmerBrush()
    LazyColumn(
        modifier = modifier.fillMaxSize(),
        contentPadding = PaddingValues(start = 16.dp, end = 16.dp, top = 64.dp, bottom = 100.dp),
        verticalArrangement = Arrangement.spacedBy(16.dp)
    ) {
        // Greeting hero
        item { SkeletonGreetingHero(brush) }
        // Working hours card
        item { SkeletonWorkingHoursCard(brush) }
        // Quick stats row
        item { SkeletonStatsRow(brush) }
        // Next up card
        item { SkeletonNextUpCard(brush) }
        // Earnings card
        item { SkeletonEarningsCard(brush) }
        // Upcoming orders section header + 2 cards
        item { ShimmerBox(width = 140.dp, height = 20.dp, brush = brush) }
        item { SkeletonUpcomingOrderCard(brush) }
        item { SkeletonUpcomingOrderCard(brush) }
    }
}

@Composable
private fun SkeletonGreetingHero(brush: Brush) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(16.dp),
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface)
    ) {
        Column(modifier = Modifier.padding(20.dp)) {
            ShimmerBox(width = 200.dp, height = 24.dp, brush = brush)
            Spacer(Modifier.height(8.dp))
            ShimmerBox(width = 260.dp, height = 16.dp, brush = brush)
            Spacer(Modifier.height(12.dp))
            Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                ShimmerBox(width = 100.dp, height = 28.dp, shape = RoundedCornerShape(14.dp), brush = brush)
                ShimmerBox(width = 100.dp, height = 28.dp, shape = RoundedCornerShape(14.dp), brush = brush)
            }
        }
    }
}

@Composable
private fun SkeletonWorkingHoursCard(brush: Brush) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(16.dp),
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface)
    ) {
        Column(modifier = Modifier.padding(16.dp)) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                ShimmerCircle(size = 36.dp, brush = brush)
                Spacer(Modifier.width(12.dp))
                Column {
                    ShimmerBox(width = 120.dp, height = 14.dp, brush = brush)
                    Spacer(Modifier.height(6.dp))
                    ShimmerBox(width = 80.dp, height = 12.dp, brush = brush)
                }
            }
            Spacer(Modifier.height(12.dp))
            ShimmerBox(height = 8.dp, shape = RoundedCornerShape(4.dp), brush = brush)
        }
    }
}

@Composable
private fun SkeletonStatsRow(brush: Brush) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .horizontalScroll(rememberScrollState()),
        horizontalArrangement = Arrangement.spacedBy(12.dp)
    ) {
        repeat(4) {
            Card(
                modifier = Modifier
                    .width(140.dp)
                    .height(120.dp),
                shape = RoundedCornerShape(16.dp),
                colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface)
            ) {
                Column(
                    modifier = Modifier.padding(12.dp),
                    verticalArrangement = Arrangement.SpaceBetween
                ) {
                    ShimmerCircle(size = 36.dp, brush = brush)
                    Spacer(Modifier.weight(1f))
                    ShimmerBox(width = 60.dp, height = 22.dp, brush = brush)
                    Spacer(Modifier.height(4.dp))
                    ShimmerBox(width = 90.dp, height = 12.dp, brush = brush)
                }
            }
        }
    }
}

@Composable
private fun SkeletonNextUpCard(brush: Brush) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(16.dp),
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
        elevation = CardDefaults.cardElevation(defaultElevation = 2.dp)
    ) {
        Column(modifier = Modifier.padding(16.dp)) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                ShimmerCircle(size = 32.dp, brush = brush)
                Spacer(Modifier.width(12.dp))
                Column(modifier = Modifier.weight(1f)) {
                    ShimmerBox(width = 140.dp, height = 16.dp, brush = brush)
                    Spacer(Modifier.height(4.dp))
                    ShimmerBox(width = 100.dp, height = 12.dp, brush = brush)
                }
                ShimmerBox(width = 60.dp, height = 24.dp, shape = RoundedCornerShape(12.dp), brush = brush)
            }
            Spacer(Modifier.height(12.dp))
            ShimmerBox(height = 14.dp, brush = brush)
            Spacer(Modifier.height(6.dp))
            ShimmerBox(width = 180.dp, height = 14.dp, brush = brush)
        }
    }
}

@Composable
private fun SkeletonEarningsCard(brush: Brush) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(16.dp),
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface)
    ) {
        Column(modifier = Modifier.padding(16.dp)) {
            ShimmerBox(width = 130.dp, height = 18.dp, brush = brush)
            Spacer(Modifier.height(16.dp))
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween
            ) {
                repeat(3) {
                    Column(horizontalAlignment = Alignment.CenterHorizontally) {
                        ShimmerBox(width = 70.dp, height = 10.dp, brush = brush)
                        Spacer(Modifier.height(6.dp))
                        ShimmerBox(width = 80.dp, height = 20.dp, brush = brush)
                    }
                }
            }
        }
    }
}

@Composable
private fun SkeletonUpcomingOrderCard(brush: Brush) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(12.dp),
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
        elevation = CardDefaults.cardElevation(defaultElevation = 1.dp)
    ) {
        Column(modifier = Modifier.padding(16.dp)) {
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween
            ) {
                ShimmerBox(width = 120.dp, height = 16.dp, brush = brush)
                ShimmerBox(width = 70.dp, height = 16.dp, brush = brush)
            }
            Spacer(Modifier.height(8.dp))
            Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                ShimmerBox(width = 70.dp, height = 20.dp, shape = RoundedCornerShape(10.dp), brush = brush)
                ShimmerBox(width = 60.dp, height = 20.dp, shape = RoundedCornerShape(10.dp), brush = brush)
            }
            Spacer(Modifier.height(8.dp))
            ShimmerBox(width = 200.dp, height = 14.dp, brush = brush)
        }
    }
}
