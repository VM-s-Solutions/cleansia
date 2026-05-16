package cz.cleansia.partner.features.profile.components

import androidx.compose.foundation.background
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
internal fun ProfileSkeleton(modifier: Modifier = Modifier) {
    val brush = rememberShimmerBrush()
    LazyColumn(
        modifier = modifier
            .fillMaxSize()
            .statusBarsPadding(),
        contentPadding = PaddingValues(start = 16.dp, end = 16.dp, top = 72.dp, bottom = 32.dp),
        verticalArrangement = Arrangement.spacedBy(16.dp)
    ) {
        // Profile header
        item { SkeletonProfileHeader(brush) }
        // 5 section cards
        items(5) { SkeletonProfileSection(brush) }
    }
}

@Composable
private fun SkeletonProfileHeader(brush: Brush) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(16.dp),
        elevation = CardDefaults.cardElevation(defaultElevation = 2.dp)
    ) {
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .background(
                    Brush.linearGradient(
                        colors = listOf(
                            MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.5f),
                            MaterialTheme.colorScheme.surface
                        )
                    )
                )
                .padding(24.dp),
            horizontalAlignment = Alignment.CenterHorizontally
        ) {
            // Avatar (80dp like real ProfileHeaderCard)
            ShimmerCircle(size = 80.dp, brush = brush)
            Spacer(Modifier.height(16.dp))
            // Name + verified icon row
            ShimmerBox(width = 150.dp, height = 20.dp, brush = brush)
            Spacer(Modifier.height(4.dp))
            // Email
            ShimmerBox(width = 180.dp, height = 14.dp, brush = brush)
        }
    }
}

@Composable
private fun SkeletonProfileSection(brush: Brush) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(16.dp),
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface)
    ) {
        Column(modifier = Modifier.padding(16.dp)) {
            // Section header (icon + title)
            Row(verticalAlignment = Alignment.CenterVertically) {
                ShimmerCircle(size = 20.dp, brush = brush)
                Spacer(Modifier.width(8.dp))
                ShimmerBox(width = 120.dp, height = 16.dp, brush = brush)
            }
            Spacer(Modifier.height(16.dp))
            // InfoRow-style fields (label left, value right)
            repeat(3) {
                Row(
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(vertical = 5.dp),
                    horizontalArrangement = Arrangement.SpaceBetween,
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    ShimmerBox(width = 80.dp, height = 14.dp, brush = brush)
                    ShimmerBox(width = 110.dp, height = 14.dp, brush = brush)
                }
            }
        }
    }
}
