package cz.cleansia.customer.ui.snackbar

import androidx.compose.animation.AnimatedVisibility
import androidx.compose.animation.fadeIn
import androidx.compose.animation.fadeOut
import androidx.compose.animation.slideInVertically
import androidx.compose.animation.slideOutVertically
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.navigationBarsPadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.CheckCircle
import androidx.compose.material.icons.outlined.Close
import androidx.compose.material.icons.outlined.ErrorOutline
import androidx.compose.material.icons.outlined.Info
import androidx.compose.material.icons.outlined.WarningAmber
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.draw.shadow
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import dagger.hilt.EntryPoint
import dagger.hilt.InstallIn
import dagger.hilt.android.EntryPointAccessors
import dagger.hilt.components.SingletonComponent
import kotlinx.coroutines.delay

@EntryPoint
@InstallIn(SingletonComponent::class)
interface SnackbarControllerEntryPoint {
    fun snackbarController(): SnackbarController
}

/**
 * Observes the app's [SnackbarController] and renders one message at a time.
 * Place ONCE at the root of the nav tree so it survives screen transitions.
 *
 * Dismiss rules:
 *  - Success / Info / Warning: auto-dismiss after 3.5s
 *  - Error: stays for 6s (errors deserve more reading time)
 *
 * Tap anywhere on the pill to dismiss early.
 */
@Composable
fun GlobalSnackbarHost(modifier: Modifier = Modifier) {
    val context = LocalContext.current
    val controller = remember {
        EntryPointAccessors.fromApplication(context, SnackbarControllerEntryPoint::class.java)
            .snackbarController()
    }

    var current by remember { mutableStateOf<SnackbarMessage?>(null) }

    LaunchedEffect(Unit) {
        controller.messages.collect { message ->
            current = message
            val durationMs = if (message.severity == Severity.Error) 6_000L else 3_500L
            delay(durationMs)
            // Only clear if still the same message (a newer one would have replaced it).
            if (current === message) current = null
        }
    }

    // Fill the whole screen so the BottomCenter alignment actually anchors to the
    // bottom. Also fall through touches — the Box itself has no background, so
    // interactive content underneath stays tappable.
    Box(modifier = modifier.fillMaxSize()) {
        AnimatedVisibility(
            visible = current != null,
            enter = fadeIn() + slideInVertically { it },
            exit = fadeOut() + slideOutVertically { it },
            modifier = Modifier.align(Alignment.BottomCenter),
        ) {
            current?.let { message ->
                CleansiaSnackbar(
                    message = message,
                    onDismiss = { current = null },
                )
            }
        }
    }
}

@Composable
private fun CleansiaSnackbar(
    message: SnackbarMessage,
    onDismiss: () -> Unit,
) {
    val palette = paletteFor(message.severity)
    val text = when (message) {
        is SnackbarMessage.FromString -> message.text
        is SnackbarMessage.FromRes -> stringResource(message.stringRes)
    }

    // Bottom inset is published by the currently-visible screen via SnackbarInsetState
    // (see SnackbarInsetScope). Screens with persistent bottom chrome (bottom nav,
    // sticky CTA, sheet) push a bigger value so the pill clears them.
    val extraBottom by SnackbarInsetState.insetDp.collectAsState()

    Row(
        modifier = Modifier
            .navigationBarsPadding()
            .padding(start = 16.dp, end = 16.dp, top = 16.dp, bottom = extraBottom)
            .fillMaxWidth()
            .shadow(elevation = 12.dp, shape = RoundedCornerShape(14.dp), clip = false)
            .clip(RoundedCornerShape(14.dp))
            .background(palette.background)
            .padding(start = 14.dp, end = 4.dp, top = 4.dp, bottom = 4.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Icon(
            imageVector = palette.icon,
            contentDescription = null,
            tint = palette.foreground,
            modifier = Modifier.size(20.dp),
        )
        Spacer(Modifier.width(12.dp))
        Text(
            text = text,
            style = MaterialTheme.typography.bodyMedium.copy(fontWeight = FontWeight.Medium),
            color = palette.foreground,
            modifier = Modifier
                .weight(1f)
                .padding(vertical = 10.dp),
        )
        IconButton(onClick = onDismiss) {
            Icon(
                imageVector = Icons.Outlined.Close,
                contentDescription = stringResource(android.R.string.cancel),
                tint = palette.foreground,
                modifier = Modifier.size(18.dp),
            )
        }
    }
}

private data class Palette(
    val background: Color,
    val foreground: Color,
    val icon: ImageVector,
)

@Composable
private fun paletteFor(severity: Severity): Palette = when (severity) {
    // Tailwind-derived tints that match the rest of the app. Dark mode: slightly
    // desaturated backgrounds to avoid screaming against slate-900.
    Severity.Error -> Palette(
        background = Color(0xFFFEE2E2), // red-100 in light; we rely on content contrast across modes
        foreground = Color(0xFFB91C1C), // red-700
        icon = Icons.Outlined.ErrorOutline,
    )
    Severity.Success -> Palette(
        background = Color(0xFFDCFCE7), // green-100
        foreground = Color(0xFF15803D), // green-700
        icon = Icons.Outlined.CheckCircle,
    )
    Severity.Info -> Palette(
        background = Color(0xFFE0F2FE), // sky-100
        foreground = Color(0xFF0369A1), // sky-700
        icon = Icons.Outlined.Info,
    )
    Severity.Warning -> Palette(
        background = Color(0xFFFEF3C7), // amber-100
        foreground = Color(0xFFB45309), // amber-700
        icon = Icons.Outlined.WarningAmber,
    )
}
