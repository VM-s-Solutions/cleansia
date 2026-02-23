package cz.cleansia.partner.features.invoices.components

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
import androidx.compose.material3.HorizontalDivider
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
internal fun InvoicesListSkeleton(modifier: Modifier = Modifier) {
    val brush = rememberShimmerBrush()
    LazyColumn(
        modifier = modifier.fillMaxSize(),
        contentPadding = PaddingValues(start = 16.dp, end = 16.dp, top = 16.dp, bottom = 100.dp),
        verticalArrangement = Arrangement.spacedBy(12.dp)
    ) {
        items(4) { SkeletonInvoiceCard(brush) }
    }
}

@Composable
private fun SkeletonInvoiceCard(brush: Brush) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
        elevation = CardDefaults.cardElevation(defaultElevation = 2.dp)
    ) {
        Column(modifier = Modifier.padding(16.dp)) {
            // Header: invoice number + badge
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                ShimmerBox(width = 120.dp, height = 18.dp, brush = brush)
                ShimmerBox(width = 65.dp, height = 22.dp, shape = RoundedCornerShape(11.dp), brush = brush)
            }
            Spacer(Modifier.height(12.dp))
            // 3 detail rows (label + value)
            repeat(3) {
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.SpaceBetween
                ) {
                    ShimmerBox(width = 80.dp, height = 14.dp, brush = brush)
                    ShimmerBox(width = 100.dp, height = 14.dp, brush = brush)
                }
                Spacer(Modifier.height(4.dp))
            }
            Spacer(Modifier.height(8.dp))
            HorizontalDivider(color = MaterialTheme.colorScheme.outlineVariant.copy(alpha = 0.3f))
            Spacer(Modifier.height(8.dp))
            // Total row
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween
            ) {
                ShimmerBox(width = 50.dp, height = 18.dp, brush = brush)
                ShimmerBox(width = 90.dp, height = 20.dp, brush = brush)
            }
        }
    }
}

@Composable
internal fun InvoiceDetailsSkeleton(modifier: Modifier = Modifier) {
    val brush = rememberShimmerBrush()
    Column(
        modifier = modifier
            .fillMaxSize()
            .verticalScroll(rememberScrollState())
            .statusBarsPadding()
            .padding(start = 16.dp, end = 16.dp, top = 72.dp, bottom = 32.dp),
        verticalArrangement = Arrangement.spacedBy(16.dp)
    ) {
        // Header card
        Card(
            modifier = Modifier.fillMaxWidth(),
            shape = RoundedCornerShape(16.dp),
            colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface)
        ) {
            Column(modifier = Modifier.padding(20.dp)) {
                ShimmerBox(width = 140.dp, height = 20.dp, brush = brush)
                Spacer(Modifier.height(8.dp))
                ShimmerBox(width = 180.dp, height = 28.dp, brush = brush)
                Spacer(Modifier.height(8.dp))
                ShimmerBox(width = 120.dp, height = 14.dp, brush = brush)
            }
        }
        // Quick info card
        Card(
            modifier = Modifier.fillMaxWidth(),
            shape = RoundedCornerShape(16.dp),
            colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface)
        ) {
            Column(modifier = Modifier.padding(16.dp)) {
                repeat(4) {
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        ShimmerCircle(size = 18.dp, brush = brush)
                        Spacer(Modifier.width(10.dp))
                        ShimmerBox(width = 80.dp, height = 12.dp, brush = brush)
                        Spacer(Modifier.weight(1f))
                        ShimmerBox(width = 100.dp, height = 14.dp, brush = brush)
                    }
                    if (it < 3) Spacer(Modifier.height(10.dp))
                }
            }
        }
        // Amount breakdown card
        Card(
            modifier = Modifier.fillMaxWidth(),
            shape = RoundedCornerShape(16.dp),
            colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface)
        ) {
            Column(modifier = Modifier.padding(16.dp)) {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    ShimmerBox(width = 32.dp, height = 32.dp, shape = RoundedCornerShape(8.dp), brush = brush)
                    Spacer(Modifier.width(12.dp))
                    ShimmerBox(width = 140.dp, height = 18.dp, brush = brush)
                }
                Spacer(Modifier.height(16.dp))
                repeat(5) {
                    Row(
                        modifier = Modifier
                            .fillMaxWidth()
                            .padding(vertical = 5.dp),
                        horizontalArrangement = Arrangement.SpaceBetween
                    ) {
                        ShimmerBox(width = 80.dp, height = 14.dp, brush = brush)
                        ShimmerBox(width = 70.dp, height = 14.dp, brush = brush)
                    }
                }
            }
        }
        // Timeline card
        Card(
            modifier = Modifier.fillMaxWidth(),
            shape = RoundedCornerShape(16.dp),
            colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface)
        ) {
            Column(modifier = Modifier.padding(16.dp)) {
                repeat(3) { i ->
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        ShimmerCircle(size = 12.dp, brush = brush)
                        Spacer(Modifier.width(12.dp))
                        Column {
                            ShimmerBox(width = 80.dp, height = 14.dp, brush = brush)
                            Spacer(Modifier.height(4.dp))
                            ShimmerBox(width = 120.dp, height = 12.dp, brush = brush)
                        }
                    }
                    if (i < 2) Spacer(Modifier.height(12.dp))
                }
            }
        }
    }
}
