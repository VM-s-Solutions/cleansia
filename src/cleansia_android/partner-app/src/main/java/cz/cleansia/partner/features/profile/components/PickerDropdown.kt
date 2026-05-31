package cz.cleansia.partner.features.profile.components

import androidx.compose.runtime.Composable
import cz.cleansia.core.ui.components.CleansiaDropdown
import cz.cleansia.core.ui.components.CleansiaDropdownOption

/**
 * Thin adapter over [CleansiaDropdown] kept around for the existing
 * section editors that call it with `Pair<id, label>`. New screens
 * should use [CleansiaDropdown] directly.
 */
@Composable
fun PickerDropdown(
    selectedId: String?,
    options: List<Pair<String, String>>,
    onSelected: (String) -> Unit,
    label: String,
    errorText: String? = null,
    enabled: Boolean = true,
    searchable: Boolean = false,
) {
    CleansiaDropdown(
        selectedId = selectedId,
        options = options.map { (id, name) -> CleansiaDropdownOption(id, name) },
        onSelected = onSelected,
        label = label,
        errorText = errorText,
        enabled = enabled,
        searchable = searchable,
    )
}
