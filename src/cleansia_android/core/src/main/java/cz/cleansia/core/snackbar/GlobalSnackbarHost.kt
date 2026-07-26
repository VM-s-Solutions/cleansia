package cz.cleansia.core.snackbar

import androidx.compose.animation.AnimatedVisibility
import androidx.compose.animation.fadeIn
import androidx.compose.animation.fadeOut
import androidx.compose.animation.slideInVertically
import androidx.compose.animation.slideOutVertically
import androidx.compose.foundation.background
import androidx.compose.foundation.border
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
import androidx.compose.ui.graphics.luminance
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import cz.cleansia.core.R
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
    // TODO(W3.3): refactor to VM injection — host composable mounted at the
    // root of the nav tree, before any nav destinations exist; a holder VM
    // here would need careful scoping. Acceptable residue for now.
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

    // INVERTED, not `surface`: a surface-coloured pill sits on a surface/background-
    // coloured page and all that separates them is a hairline and a soft shadow, so
    // the snackbar reads as part of the screen and is easy to miss entirely.
    // Inverting it (dark pill in light theme, light pill in dark theme) is the
    // standard treatment for a transient overlay and makes the message unmissable in
    // both themes. Mirrors the decision iOS shipped in PR #148.
    val pill = MaterialTheme.colorScheme.inverseSurface
    val onPill = MaterialTheme.colorScheme.inverseOnSurface

    Row(
        modifier = Modifier
            .navigationBarsPadding()
            .padding(start = 16.dp, end = 16.dp, top = 16.dp, bottom = extraBottom)
            .fillMaxWidth()
            .shadow(elevation = 12.dp, shape = RoundedCornerShape(14.dp), clip = false)
            .clip(RoundedCornerShape(14.dp))
            .background(pill)
            // Hairline so the pill still has an edge when it lands on a scrim or a
            // photo rather than on the page background.
            .border(0.5.dp, onPill.copy(alpha = 0.12f), RoundedCornerShape(14.dp))
            .padding(start = 14.dp, end = 4.dp, top = 4.dp, bottom = 4.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Icon(
            imageVector = palette.icon,
            contentDescription = null,
            tint = palette.accent,
            modifier = Modifier.size(20.dp),
        )
        Spacer(Modifier.width(12.dp))
        Text(
            text = text,
            // SemiBold, not Medium: Nunito ships no Medium(500), so the CSS-style
            // weight matcher silently resolved `Medium` down to Regular and the
            // .copy() was a no-op. SemiBold is the nearest weight we actually bundle.
            style = MaterialTheme.typography.bodyMedium.copy(fontWeight = FontWeight.SemiBold),
            color = onPill,
            modifier = Modifier
                .weight(1f)
                .padding(vertical = 10.dp),
        )
        IconButton(onClick = onDismiss) {
            Icon(
                imageVector = Icons.Outlined.Close,
                contentDescription = stringResource(R.string.core_snackbar_dismiss),
                tint = onPill.copy(alpha = 0.7f),
                modifier = Modifier.size(18.dp),
            )
        }
    }
}

private data class Palette(
    val accent: Color,
    val icon: ImageVector,
)

@Composable
private fun paletteFor(severity: Severity): Palette {
    // The accent is keyed to the PILL it sits on, not to the page. Because the pill
    // is inverted, the pairing is the opposite of a normal surface-coloured control:
    // a light theme gives a near-black pill, on which the brighter 500 tone
    // separates; a dark theme gives a near-white pill, on which the deeper 600 tone
    // does. Tones taken verbatim from iOS PR #148 so both platforms read identically.
    //
    // Resolved from the pill's own luminance rather than isSystemInDarkTheme():
    // CleansiaTheme(darkTheme = ...) accepts an explicit override, so reading the
    // system setting would desynchronise the accent from the pill the moment anyone
    // forces a theme. The pill's luminance cannot lie about what the pill is.
    val pillIsDark = MaterialTheme.colorScheme.inverseSurface.luminance() < 0.5f
    return when (severity) {
        Severity.Error -> Palette(
            accent = if (pillIsDark) Color(0xFFEF4444) else Color(0xFFDC2626), // red-500 / red-600
            icon = Icons.Outlined.ErrorOutline,
        )
        Severity.Success -> Palette(
            accent = if (pillIsDark) Color(0xFF22C55E) else Color(0xFF16A34A), // green-500 / green-600
            icon = Icons.Outlined.CheckCircle,
        )
        // Info rides the Sky brand ramp exactly as it does on iOS.
        Severity.Info -> Palette(
            accent = if (pillIsDark) Color(0xFF0EA5E9) else Color(0xFF0284C7), // sky-500 / sky-600
            icon = Icons.Outlined.Info,
        )
        Severity.Warning -> Palette(
            accent = if (pillIsDark) Color(0xFFF59E0B) else Color(0xFFD97706), // amber-500 / amber-600
            icon = Icons.Outlined.WarningAmber,
        )
    }
}
