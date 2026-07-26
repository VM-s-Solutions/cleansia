package cz.cleansia.core.ui.components

import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.defaultMinSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.luminance
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.unit.dp

enum class CleansiaButtonSize(
    val minHeight: androidx.compose.ui.unit.Dp,
    val horizontalPadding: androidx.compose.ui.unit.Dp,
) {
    Small(40.dp, 16.dp),
    Medium(48.dp, 24.dp),
    Large(56.dp, 32.dp),
}

/** Solid primary CTA — sky-600 fill, white text, pill shape. */
@Composable
fun CleansiaPrimaryButton(
    text: String,
    onClick: () -> Unit,
    modifier: Modifier = Modifier,
    size: CleansiaButtonSize = CleansiaButtonSize.Large,
    leadingIcon: ImageVector? = null,
    trailingIcon: ImageVector? = null,
    loading: Boolean = false,
    enabled: Boolean = true,
) {
    Button(
        onClick = onClick,
        modifier = modifier
            .fillMaxWidth()
            .defaultMinSize(minHeight = size.minHeight),
        enabled = enabled && !loading,
        shape = CircleShape,
        contentPadding = PaddingValues(horizontal = size.horizontalPadding, vertical = 12.dp),
        colors = ButtonDefaults.buttonColors(
            containerColor = MaterialTheme.colorScheme.primary,
            contentColor = MaterialTheme.colorScheme.onPrimary,
        ),
    ) {
        if (loading) {
            CircularProgressIndicator(
                modifier = Modifier.size(20.dp),
                color = MaterialTheme.colorScheme.onPrimary,
                strokeWidth = 2.dp,
            )
        } else {
            if (leadingIcon != null) {
                Icon(leadingIcon, contentDescription = null, modifier = Modifier.size(18.dp))
                Spacer(Modifier.width(8.dp))
            }
            Text(text = text, style = MaterialTheme.typography.titleMedium)
            if (trailingIcon != null) {
                Spacer(Modifier.width(8.dp))
                Icon(trailingIcon, contentDescription = null, modifier = Modifier.size(18.dp))
            }
        }
    }
}

/**
 * Irreversible-action CTA (delete account, permanently discard, …).
 *
 * Uses a FIXED red container in both themes, deliberately NOT
 * `colorScheme.error`. This is a visual-hierarchy fix, not a contrast fix —
 * `error` / `onError` is the correct M3 pairing and clears WCAG in both
 * themes, so anyone auditing contrast here will (rightly) find nothing wrong.
 * The problem is rank: in dark mode `error` is red-300 (0xFFFCA5A5), so a
 * container painted with it becomes the highest-luminance element on a
 * Slate-900 page — brighter than the Sky400 primary — and the destroy-account
 * button ends up reading as the most inviting affordance on screen. Danger
 * must not out-rank the primary; it must read as danger.
 *
 * The light/dark pair is picked from `surface`'s luminance rather than
 * `isSystemInDarkTheme()` so it tracks the theme actually in force —
 * `CleansiaTheme(darkTheme = …)` accepts an explicit override, and a
 * system-setting read would desynchronise the moment anyone uses it.
 *
 * Signature mirrors [CleansiaPrimaryButton] exactly so the two are drop-in
 * siblings.
 */
@Composable
fun CleansiaDestructiveButton(
    text: String,
    onClick: () -> Unit,
    modifier: Modifier = Modifier,
    size: CleansiaButtonSize = CleansiaButtonSize.Large,
    leadingIcon: ImageVector? = null,
    loading: Boolean = false,
    enabled: Boolean = true,
) {
    // red-600 on a light page, red-700 on a dark one: deep enough in dark mode
    // to sit BELOW the primary in the luminance order while still reading red.
    val container = if (MaterialTheme.colorScheme.surface.luminance() < 0.5f) {
        Color(0xFFB91C1C)
    } else {
        Color(0xFFDC2626)
    }
    Button(
        onClick = onClick,
        modifier = modifier
            .fillMaxWidth()
            .defaultMinSize(minHeight = size.minHeight),
        enabled = enabled && !loading,
        shape = CircleShape,
        contentPadding = PaddingValues(horizontal = size.horizontalPadding, vertical = 12.dp),
        colors = ButtonDefaults.buttonColors(
            containerColor = container,
            contentColor = Color.White,
            // 38% is the M3 disabled convention; applied to our own colours
            // because ButtonDefaults' disabled tokens are surface-derived and
            // would drop the red entirely.
            disabledContainerColor = container.copy(alpha = 0.38f),
            disabledContentColor = Color.White.copy(alpha = 0.38f),
        ),
    ) {
        if (loading) {
            CircularProgressIndicator(
                modifier = Modifier.size(20.dp),
                color = Color.White,
                strokeWidth = 2.dp,
            )
        } else {
            if (leadingIcon != null) {
                Icon(leadingIcon, contentDescription = null, modifier = Modifier.size(18.dp))
                Spacer(Modifier.width(8.dp))
            }
            Text(text = text, style = MaterialTheme.typography.titleMedium)
        }
    }
}

/** Outlined button — used for "Continue with Google", secondary CTAs. */
@Composable
fun CleansiaOutlinedButton(
    text: String,
    onClick: () -> Unit,
    modifier: Modifier = Modifier,
    size: CleansiaButtonSize = CleansiaButtonSize.Large,
    leadingIcon: ImageVector? = null,
    enabled: Boolean = true,
) {
    OutlinedButton(
        onClick = onClick,
        modifier = modifier
            .fillMaxWidth()
            .defaultMinSize(minHeight = size.minHeight),
        enabled = enabled,
        shape = CircleShape,
        border = BorderStroke(1.dp, MaterialTheme.colorScheme.outline),
        contentPadding = PaddingValues(horizontal = size.horizontalPadding, vertical = 12.dp),
    ) {
        if (leadingIcon != null) {
            Icon(leadingIcon, contentDescription = null, modifier = Modifier.size(20.dp))
            Spacer(Modifier.width(12.dp))
        }
        Text(
            text = text,
            style = MaterialTheme.typography.titleMedium,
            color = MaterialTheme.colorScheme.onSurface,
        )
    }
}

/** Inline link — primary-colored text, no chrome. */
@Composable
fun CleansiaTextLink(
    text: String,
    onClick: () -> Unit,
    modifier: Modifier = Modifier,
) {
    TextButton(
        onClick = onClick,
        modifier = modifier,
        contentPadding = PaddingValues(horizontal = 4.dp, vertical = 4.dp),
    ) {
        Text(
            text = text,
            style = MaterialTheme.typography.labelLarge,
            color = MaterialTheme.colorScheme.primary,
        )
    }
}
