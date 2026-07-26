package cz.cleansia.core.ui.components

import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.CloudOff
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import cz.cleansia.core.ui.theme.Poppins

/**
 * Full-screen load-failure surface: offline glyph, title, explanation, an
 * optional retry CTA and an always-present way back out.
 *
 * Lifted verbatim from the customer order-detail screen, which had the only
 * complete implementation in the repo; the copy is parameterised the same way
 * [MascotEmptyState] parameterises its painter, so `:core` owns the layout and
 * each app supplies its own wording from its own `strings.xml`.
 *
 * [onRetry] is optional because not every failure is retryable — a 404 on a
 * deleted order is permanent, and offering "Try again" there is a lie. Pass
 * [retryLabel] and [onRetry] together or neither; the button renders only when
 * both are non-null. [onBack] is not optional: a dead-end error screen with no
 * exit is how users end up force-quitting the app.
 *
 * The title hard-codes [Poppins] rather than inheriting `titleMedium`'s family.
 * That is deliberate and carried over from the original: `titleMedium` is
 * Nunito, and an error heading in body copy's own face stops reading as a
 * heading at all.
 */
@Composable
fun CleansiaErrorState(
    title: String,
    message: String,
    backLabel: String,
    modifier: Modifier = Modifier,
    retryLabel: String? = null,
    onRetry: (() -> Unit)? = null,
    onBack: () -> Unit,
) {
    Column(
        modifier = modifier
            .fillMaxSize()
            .padding(32.dp),
        horizontalAlignment = Alignment.CenterHorizontally,
        verticalArrangement = Arrangement.Center,
    ) {
        Icon(
            Icons.Outlined.CloudOff,
            contentDescription = null,
            tint = MaterialTheme.colorScheme.onSurfaceVariant,
            modifier = Modifier.size(48.dp),
        )
        Spacer(Modifier.height(16.dp))
        Text(
            text = title,
            style = MaterialTheme.typography.titleMedium.copy(
                fontFamily = Poppins,
                fontWeight = FontWeight.SemiBold,
            ),
            color = MaterialTheme.colorScheme.onBackground,
            textAlign = TextAlign.Center,
        )
        Spacer(Modifier.height(8.dp))
        Text(
            text = message,
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
            textAlign = TextAlign.Center,
        )
        Spacer(Modifier.height(24.dp))
        if (retryLabel != null && onRetry != null) {
            CleansiaPrimaryButton(
                text = retryLabel,
                onClick = onRetry,
            )
            Spacer(Modifier.height(8.dp))
        }
        Text(
            text = backLabel,
            style = MaterialTheme.typography.labelLarge.copy(fontWeight = FontWeight.SemiBold),
            color = MaterialTheme.colorScheme.primary,
            modifier = Modifier
                .clip(RoundedCornerShape(999.dp))
                .clickable(onClick = onBack)
                .padding(horizontal = 16.dp, vertical = 8.dp),
        )
    }
}
