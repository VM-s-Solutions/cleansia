package cz.cleansia.partner.features.main

import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.tooling.preview.Preview
import cz.cleansia.core.ui.components.FloatingIslandBar
import cz.cleansia.core.ui.components.FloatingIslandSlot
import cz.cleansia.partner.ui.theme.CleansiaPartnerTheme

/** The partner's four tabs, evenly spaced in the shared floating island bar: it has no primary create action. */
@Composable
fun FloatingIslandBottomBar(
    selected: MainTab,
    onSelect: (MainTab) -> Unit,
    modifier: Modifier = Modifier,
) {
    FloatingIslandBar(modifier = modifier) {
        MainTab.values().forEach { tab ->
            FloatingIslandSlot(
                selected = tab == selected,
                icon = tab.icon,
                label = stringResource(tab.labelRes),
                onClick = { onSelect(tab) },
                modifier = Modifier.weight(1f),
            )
        }
    }
}

/**
 * 320dp is the narrowest width the bar has to survive, and uk/ru carry the
 * longest labels we ship — `dashboard` is "Панель керування" — so this pair is
 * where a label first stops fitting. Every label must read as one ellipsized
 * line inside the pill.
 */
@Preview(name = "Island bar — uk @ 320dp", widthDp = 320, heightDp = 120, locale = "uk")
@Preview(name = "Island bar — ru @ 320dp", widthDp = 320, heightDp = 120, locale = "ru")
@Composable
private fun FloatingIslandBottomBarNarrowPreview() {
    CleansiaPartnerTheme {
        FloatingIslandBottomBar(selected = MainTab.Dashboard, onSelect = {})
    }
}
