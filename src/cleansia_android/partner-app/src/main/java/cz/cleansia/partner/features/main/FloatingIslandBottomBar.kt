package cz.cleansia.partner.features.main

import androidx.compose.animation.core.animateDpAsState
import androidx.compose.animation.core.tween
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.interaction.MutableInteractionSource
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.remember
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import cz.cleansia.partner.ui.theme.CleansiaPartnerTheme

/**
 * Floating island bottom bar — pill-shaped surface inset from the screen
 * edges + gesture area. Customer-app uses the same shape with a center FAB
 * for "Book"; partner doesn't have a primary create action so we use 4
 * evenly-spaced slots instead.
 *
 * Animated active indicator (3dp pill that grows in under the selected
 * tab's label) gives the same visual signature without copy-pasting the
 * customer code verbatim.
 */
@Composable
fun FloatingIslandBottomBar(
    selected: MainTab,
    onSelect: (MainTab) -> Unit,
    modifier: Modifier = Modifier,
) {
    Box(
        modifier = modifier
            .fillMaxWidth()
            .padding(horizontal = 16.dp, vertical = 12.dp),
    ) {
        // Matches customer-app's CustomBottomBar exactly: clipped pill
        // + outline-variant border + no shadow. The two apps share a
        // visual home so the bar must read as the same component.
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .height(64.dp)
                .clip(RoundedCornerShape(32.dp))
                .background(MaterialTheme.colorScheme.surface)
                .border(
                    width = 1.dp,
                    color = MaterialTheme.colorScheme.outlineVariant,
                    shape = RoundedCornerShape(32.dp),
                )
                .padding(horizontal = 8.dp),
            horizontalArrangement = Arrangement.SpaceEvenly,
            verticalAlignment = Alignment.CenterVertically,
        ) {
            MainTab.values().forEach { tab ->
                NavSlot(
                    tab = tab,
                    icon = tab.icon,
                    labelRes = tab.labelRes,
                    selected = selected,
                    onSelect = onSelect,
                    modifier = Modifier.weight(1f),
                )
            }
        }
    }
}

@Composable
private fun NavSlot(
    tab: MainTab,
    icon: ImageVector,
    labelRes: Int,
    selected: MainTab,
    onSelect: (MainTab) -> Unit,
    modifier: Modifier = Modifier,
) {
    val isSelected = tab == selected
    val color = if (isSelected) MaterialTheme.colorScheme.primary
    else MaterialTheme.colorScheme.onSurfaceVariant

    val dotWidth by animateDpAsState(
        targetValue = if (isSelected) 20.dp else 0.dp,
        animationSpec = tween(durationMillis = 200),
        label = "nav-dot",
    )

    Column(
        modifier = modifier
            .clickable(
                interactionSource = remember { MutableInteractionSource() },
                indication = null,
            ) { onSelect(tab) }
            .padding(horizontal = 4.dp, vertical = 6.dp),
        horizontalAlignment = Alignment.CenterHorizontally,
    ) {
        Icon(
            imageVector = icon,
            contentDescription = null,
            tint = color,
            modifier = Modifier.size(24.dp),
        )
        Spacer(Modifier.height(2.dp))
        Text(
            text = stringResource(labelRes),
            style = MaterialTheme.typography.labelSmall.copy(
                fontWeight = if (isSelected) FontWeight.SemiBold else FontWeight.Normal,
            ),
            color = color,
            maxLines = 1,
            overflow = TextOverflow.Ellipsis,
            textAlign = TextAlign.Center,
        )
        Spacer(Modifier.height(3.dp))
        Box(
            modifier = Modifier
                .size(width = dotWidth, height = 3.dp)
                .clip(RoundedCornerShape(999.dp))
                .background(MaterialTheme.colorScheme.primary),
        )
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
