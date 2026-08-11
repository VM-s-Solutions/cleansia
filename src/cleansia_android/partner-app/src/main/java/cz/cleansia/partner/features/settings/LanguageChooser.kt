package cz.cleansia.partner.features.settings

import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.ArrowDropDown
import androidx.compose.material.icons.outlined.Language
import androidx.compose.material3.DropdownMenu
import androidx.compose.material3.DropdownMenuItem
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import cz.cleansia.core.ui.theme.Spacing
import cz.cleansia.partner.R
import cz.cleansia.partner.core.settings.LanguageLabels
import cz.cleansia.partner.core.settings.LanguagePreference

/**
 * Compact language dropdown for the surfaces a cleaner reaches before the
 * profile settings exist: the intro and the registration lock.
 *
 * A dropdown, not a pushed picker route: both of those screens are reached
 * before the cleaner is approved and neither has an app bar to go back to, so
 * a full screen would need a bespoke return path. The trigger shows the
 * language *the app is in right now* — the native name, or the translated
 * "System" label when following the device — so it is legible to someone who
 * cannot read the language currently on screen.
 */
@Composable
internal fun LanguageChooser(
    selected: LanguagePreference,
    onSelect: (LanguagePreference) -> Unit,
    modifier: Modifier = Modifier,
) {
    val tint = MaterialTheme.colorScheme.primary
    var expanded by remember { mutableStateOf(false) }
    val systemLabel = stringResource(R.string.language_system)
    val label = LanguageLabels.nativeName(selected) ?: systemLabel
    // The trigger has no visible caption — "Language" is what a screen reader
    // must announce, and that string already ships in all five locales.
    val accessibilityLabel = stringResource(R.string.language)

    Box(modifier = modifier) {
        Row(
            modifier = Modifier
                .clip(CircleShape)
                .clickable { expanded = true }
                .padding(horizontal = Spacing.S, vertical = Spacing.XS)
                .semantics { contentDescription = accessibilityLabel },
            verticalAlignment = Alignment.CenterVertically,
        ) {
            Icon(
                imageVector = Icons.Outlined.Language,
                contentDescription = null,
                tint = tint,
                modifier = Modifier.size(20.dp),
            )
            Spacer(Modifier.size(Spacing.XS))
            Text(
                text = label,
                style = MaterialTheme.typography.labelLarge,
                color = tint,
            )
            Icon(
                imageVector = Icons.Outlined.ArrowDropDown,
                contentDescription = null,
                tint = tint,
                modifier = Modifier.size(20.dp),
            )
        }

        DropdownMenu(expanded = expanded, onDismissRequest = { expanded = false }) {
            LanguageLabels.ordered.forEach { option ->
                DropdownMenuItem(
                    text = {
                        Text(
                            text = LanguageLabels.nativeName(option) ?: systemLabel,
                            style = MaterialTheme.typography.bodyLarge.copy(
                                fontWeight = if (option == selected) FontWeight.SemiBold else FontWeight.Normal,
                            ),
                            color = if (option == selected) {
                                MaterialTheme.colorScheme.primary
                            } else {
                                MaterialTheme.colorScheme.onSurface
                            },
                        )
                    },
                    onClick = {
                        expanded = false
                        onSelect(option)
                    },
                )
            }
        }
    }
}
