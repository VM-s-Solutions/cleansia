package cz.cleansia.partner.ui.components

import androidx.compose.animation.animateContentSize
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.IntrinsicSize
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxHeight
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.statusBarsPadding
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.rounded.CheckCircle
import androidx.compose.material.icons.rounded.ErrorOutline
import androidx.compose.material.icons.rounded.Info
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.SnackbarData
import androidx.compose.material3.SnackbarHost
import androidx.compose.material3.SnackbarHostState
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp

/**
 * Top-aligned styled snackbar host. Place this inside a Box that fills the screen
 * so snackbars appear at the top, above all content.
 */
@Composable
fun CleansiaSnackbarHost(
    hostState: SnackbarHostState,
    modifier: Modifier = Modifier
) {
    Box(modifier = modifier.fillMaxSize()) {
        SnackbarHost(
            hostState = hostState,
            modifier = Modifier
                .align(Alignment.TopCenter)
                .statusBarsPadding()
                .padding(top = 4.dp),
            snackbar = { data -> CleansiaSnackbar(data) }
        )
    }
}

@Composable
private fun CleansiaSnackbar(data: SnackbarData) {
    val message = data.visuals.message
    val isSuccess = message.contains("success", ignoreCase = true) ||
            message.contains("saved", ignoreCase = true) ||
            message.contains("completed", ignoreCase = true) ||
            message.contains("uploaded", ignoreCase = true) ||
            message.contains("sent", ignoreCase = true) ||
            message.contains("enabled", ignoreCase = true) ||
            message.contains("disabled", ignoreCase = true) ||
            message.contains("accepted", ignoreCase = true)
    // Check success first — everything else that isn't explicitly informational is an error
    val isError = !isSuccess

    val accentColor = when {
        isSuccess -> Color(0xFF22C55E)
        isError -> Color(0xFFEF4444)
        else -> Color(0xFF3B82F6)
    }

    val containerColor = MaterialTheme.colorScheme.inverseSurface

    val icon = when {
        isError -> Icons.Rounded.ErrorOutline
        isSuccess -> Icons.Rounded.CheckCircle
        else -> Icons.Rounded.Info
    }

    Card(
        modifier = Modifier
            .fillMaxWidth()
            .padding(horizontal = 16.dp)
            .animateContentSize(),
        shape = RoundedCornerShape(14.dp),
        colors = CardDefaults.cardColors(containerColor = containerColor),
        elevation = CardDefaults.cardElevation(defaultElevation = 6.dp)
    ) {
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .height(IntrinsicSize.Min),
            verticalAlignment = Alignment.CenterVertically
        ) {
            // Accent stripe on the left
            Box(
                modifier = Modifier
                    .width(4.dp)
                    .fillMaxHeight()
                    .background(accentColor)
            )

            // Icon
            Box(
                modifier = Modifier
                    .padding(start = 14.dp, top = 14.dp, bottom = 14.dp)
                    .size(28.dp)
                    .clip(RoundedCornerShape(8.dp))
                    .background(accentColor.copy(alpha = 0.15f)),
                contentAlignment = Alignment.Center
            ) {
                Icon(
                    imageVector = icon,
                    contentDescription = null,
                    tint = accentColor,
                    modifier = Modifier.size(18.dp)
                )
            }

            Spacer(modifier = Modifier.width(12.dp))

            // Message text
            Text(
                text = message,
                style = MaterialTheme.typography.bodyMedium,
                fontWeight = FontWeight.Medium,
                fontSize = 13.sp,
                color = MaterialTheme.colorScheme.inverseOnSurface,
                maxLines = 3,
                overflow = TextOverflow.Ellipsis,
                modifier = Modifier
                    .weight(1f)
                    .padding(vertical = 14.dp, horizontal = 2.dp)
            )

            Spacer(modifier = Modifier.width(14.dp))
        }
    }
}
