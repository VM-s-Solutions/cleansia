package cz.cleansia.partner.features.orders.components

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.statusBarsPadding
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
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
internal fun OrdersListSkeleton(modifier: Modifier = Modifier) {
    val brush = rememberShimmerBrush()
    LazyColumn(
        modifier = modifier.fillMaxSize(),
        contentPadding = PaddingValues(start = 16.dp, end = 16.dp, top = 16.dp, bottom = 100.dp),
        verticalArrangement = Arrangement.spacedBy(12.dp)
    ) {
        items(5) { SkeletonOrderCard(brush) }
    }
}

@Composable
private fun SkeletonOrderCard(brush: Brush) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
        elevation = CardDefaults.cardElevation(defaultElevation = 2.dp)
    ) {
        Column(modifier = Modifier.padding(16.dp)) {
            // Header: order number + price
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween
            ) {
                ShimmerBox(width = 130.dp, height = 18.dp, brush = brush)
                ShimmerBox(width = 80.dp, height = 18.dp, brush = brush)
            }
            Spacer(Modifier.height(8.dp))
            // Status badges
            Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                ShimmerBox(width = 70.dp, height = 22.dp, shape = RoundedCornerShape(11.dp), brush = brush)
                ShimmerBox(width = 60.dp, height = 22.dp, shape = RoundedCornerShape(11.dp), brush = brush)
            }
            Spacer(Modifier.height(12.dp))
            // Address row
            Row(verticalAlignment = Alignment.CenterVertically) {
                ShimmerCircle(size = 16.dp, brush = brush)
                Spacer(Modifier.width(4.dp))
                ShimmerBox(width = 200.dp, height = 14.dp, brush = brush)
            }
            Spacer(Modifier.height(4.dp))
            // Date row
            Row(verticalAlignment = Alignment.CenterVertically) {
                ShimmerCircle(size = 16.dp, brush = brush)
                Spacer(Modifier.width(4.dp))
                ShimmerBox(width = 140.dp, height = 14.dp, brush = brush)
            }
            Spacer(Modifier.height(8.dp))
            // Services
            ShimmerBox(width = 180.dp, height = 12.dp, brush = brush)
        }
    }
}

@Composable
internal fun OrderDetailsSkeleton(modifier: Modifier = Modifier) {
    val brush = rememberShimmerBrush()
    Column(
        modifier = modifier
            .fillMaxSize()
            .verticalScroll(rememberScrollState())
            .statusBarsPadding()
            .padding(start = 16.dp, end = 16.dp, top = 72.dp, bottom = 32.dp),
        verticalArrangement = Arrangement.spacedBy(16.dp)
    ) {
        // Quick info card
        Card(
            modifier = Modifier.fillMaxWidth(),
            shape = RoundedCornerShape(16.dp),
            colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface)
        ) {
            Column(modifier = Modifier.padding(16.dp)) {
                // Address row
                Row(verticalAlignment = Alignment.CenterVertically) {
                    ShimmerCircle(size = 18.dp, brush = brush)
                    Spacer(Modifier.width(10.dp))
                    ShimmerBox(width = 220.dp, height = 14.dp, brush = brush)
                }
                Spacer(Modifier.height(10.dp))
                // Schedule row
                Row(verticalAlignment = Alignment.CenterVertically) {
                    ShimmerCircle(size = 18.dp, brush = brush)
                    Spacer(Modifier.width(10.dp))
                    ShimmerBox(width = 160.dp, height = 14.dp, brush = brush)
                }
                Spacer(Modifier.height(10.dp))
                // Property row
                Row(verticalAlignment = Alignment.CenterVertically) {
                    ShimmerCircle(size = 18.dp, brush = brush)
                    Spacer(Modifier.width(10.dp))
                    ShimmerBox(width = 140.dp, height = 14.dp, brush = brush)
                }
            }
        }

        // Workflow stepper
        Card(
            modifier = Modifier.fillMaxWidth(),
            shape = RoundedCornerShape(16.dp),
            colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface)
        ) {
            Column(modifier = Modifier.padding(16.dp)) {
                repeat(4) { i ->
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        ShimmerCircle(size = 24.dp, brush = brush)
                        Spacer(Modifier.width(12.dp))
                        ShimmerBox(width = 100.dp, height = 14.dp, brush = brush)
                    }
                    if (i < 3) Spacer(Modifier.height(12.dp))
                }
            }
        }

        // Customer contact card
        Card(
            modifier = Modifier.fillMaxWidth(),
            shape = RoundedCornerShape(16.dp),
            colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface)
        ) {
            Row(
                modifier = Modifier.padding(16.dp),
                verticalAlignment = Alignment.CenterVertically
            ) {
                ShimmerCircle(size = 40.dp, brush = brush)
                Spacer(Modifier.width(12.dp))
                Column(modifier = Modifier.weight(1f)) {
                    ShimmerBox(width = 120.dp, height = 16.dp, brush = brush)
                    Spacer(Modifier.height(4.dp))
                    ShimmerBox(width = 100.dp, height = 12.dp, brush = brush)
                }
                ShimmerBox(width = 36.dp, height = 36.dp, shape = RoundedCornerShape(18.dp), brush = brush)
            }
        }

        // Expandable details card
        Card(
            modifier = Modifier.fillMaxWidth(),
            shape = RoundedCornerShape(16.dp),
            colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface)
        ) {
            Column(modifier = Modifier.padding(16.dp)) {
                ShimmerBox(width = 150.dp, height = 16.dp, brush = brush)
                Spacer(Modifier.height(12.dp))
                repeat(3) {
                    ShimmerBox(height = 14.dp, brush = brush)
                    Spacer(Modifier.height(6.dp))
                }
            }
        }

        // Action button placeholder
        ShimmerBox(
            height = 52.dp,
            shape = RoundedCornerShape(16.dp),
            brush = brush
        )
    }
}
