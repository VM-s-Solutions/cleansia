package cz.cleansia.partner.ui.components

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.ErrorOutline
import androidx.compose.material.icons.filled.Refresh
import androidx.compose.material.icons.filled.WifiOff
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import cz.cleansia.partner.R
import cz.cleansia.partner.core.network.ApiError

@Composable
fun ErrorView(
    message: String,
    modifier: Modifier = Modifier,
    icon: ImageVector = Icons.Default.ErrorOutline,
    onRetry: (() -> Unit)? = null
) {
    Column(
        modifier = modifier
            .fillMaxSize()
            .padding(24.dp),
        horizontalAlignment = Alignment.CenterHorizontally,
        verticalArrangement = Arrangement.Center
    ) {
        Icon(
            imageVector = icon,
            contentDescription = null,
            modifier = Modifier.size(64.dp),
            tint = MaterialTheme.colorScheme.error
        )

        Spacer(modifier = Modifier.height(16.dp))

        Text(
            text = message,
            style = MaterialTheme.typography.bodyLarge,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
            textAlign = TextAlign.Center
        )

        if (onRetry != null) {
            Spacer(modifier = Modifier.height(24.dp))

            CleansiaButton(
                text = stringResource(R.string.retry),
                onClick = onRetry,
                style = CleansiaButtonStyle.OUTLINED,
                icon = Icons.Default.Refresh,
                fullWidth = false
            )
        }
    }
}

@Composable
fun NetworkErrorView(
    modifier: Modifier = Modifier,
    onRetry: (() -> Unit)? = null
) {
    ErrorView(
        message = stringResource(R.string.error_network),
        icon = Icons.Default.WifiOff,
        onRetry = onRetry,
        modifier = modifier
    )
}

@Composable
fun ApiErrorView(
    error: ApiError,
    modifier: Modifier = Modifier,
    onRetry: (() -> Unit)? = null
) {
    val icon = when (error) {
        is ApiError.Network -> Icons.Default.WifiOff
        else -> Icons.Default.ErrorOutline
    }

    ErrorView(
        message = error.getUserMessage(),
        icon = icon,
        onRetry = onRetry,
        modifier = modifier
    )
}
