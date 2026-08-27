package cz.cleansia.core.ui.components

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.Checkbox
import androidx.compose.material3.CheckboxDefaults
import androidx.compose.material3.LocalMinimumInteractiveComponentSize
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.CompositionLocalProvider
import androidx.compose.runtime.remember
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.SpanStyle
import androidx.compose.ui.text.TextLinkStyles
import androidx.compose.ui.text.style.TextDecoration
import androidx.compose.ui.unit.Dp
import cz.cleansia.core.R
import cz.cleansia.core.ui.theme.Spacing

/**
 * A consent row whose sentence carries tappable legal links.
 *
 * The component this replaces made the WHOLE ROW clickable, which swallowed
 * every tap inside the label: a finger landing on "Privacy Policy" toggled the
 * checkbox instead of opening the page, so a link in the copy could never have
 * worked. Here only the checkbox is a control — the sentence is inert text
 * whose `LinkAnnotation`s reach `LocalUriHandler`. Same fix, same reason as
 * `CleansiaConsentCheckbox.swift` on iOS.
 *
 * [html] is a localized sentence carrying `<a href="cleansia://…">` markup —
 * see [ConsentMarkup] for why that markup must be CDATA-wrapped in the
 * `strings.xml` files, and what silently breaks when it isn't.
 */
@Composable
fun CleansiaConsentCheckbox(
    checked: Boolean,
    onCheckedChange: (Boolean) -> Unit,
    html: String,
    modifier: Modifier = Modifier,
    toggleContentDescription: String = stringResource(R.string.core_consent_toggle),
) {
    // Colour + underline are set as link styles rather than on the whole Text:
    // a Text-level colour would flatten the links into body copy, and colour
    // alone is not an accessible affordance.
    val linkColor = MaterialTheme.colorScheme.primary
    val sentence = remember(html, linkColor) {
        ConsentMarkup.annotated(
            html,
            TextLinkStyles(
                style = SpanStyle(color = linkColor, textDecoration = TextDecoration.Underline),
            ),
        )
    }

    // Cancels the 48dp layout box Material 3 reserves around the 20dp glyph, so the glyph
    // starts at the row's leading edge like every field above it. This affects LAYOUT ONLY:
    // Compose inflates a pointer-input node's hit bounds to the 48dp minimum regardless of
    // measured size, so the touch target is unchanged. The 12dp it now overflows each side
    // stays inside the callers' own 24dp horizontal padding.
    //
    // The iOS twin does the same thing with `.padding(-8)` on its glyph.
    CompositionLocalProvider(LocalMinimumInteractiveComponentSize provides Dp.Unspecified) {
        Row(
            modifier = modifier.padding(vertical = Spacing.XXS),
            verticalAlignment = Alignment.Top,
            horizontalArrangement = Arrangement.spacedBy(Spacing.XXS),
        ) {
            Checkbox(
                checked = checked,
                onCheckedChange = onCheckedChange,
                // A short label, NOT the sentence: the sentence is its own
                // accessibility element below (and carries the actionable links),
                // so repeating it here makes TalkBack read the legal text twice.
                modifier = Modifier.semantics {
                    contentDescription = toggleContentDescription
                },
                colors = CheckboxDefaults.colors(
                    checkedColor = MaterialTheme.colorScheme.primary,
                    uncheckedColor = MaterialTheme.colorScheme.outline,
                ),
            )
            // No top padding any more. That 12dp existed to push the sentence down to a glyph
            // sitting inside a 48dp box; with the box gone, a 24dp checkbox and a 20dp
            // bodyMedium first line top-align within about 2dp on their own.
            Text(
                text = sentence,
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurface,
                modifier = Modifier.weight(1f),
            )
        }
    }
}
