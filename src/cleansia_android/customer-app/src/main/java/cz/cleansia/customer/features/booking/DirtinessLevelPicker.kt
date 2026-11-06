package cz.cleansia.customer.features.booking

import androidx.annotation.StringRes
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.selection.selectable
import androidx.compose.foundation.selection.selectableGroup
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.Info
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.RadioButton
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import cz.cleansia.core.ui.theme.primaryText
import cz.cleansia.customer.R
import cz.cleansia.customer.core.booking.DirtinessLevel
import cz.cleansia.customer.ui.theme.CleansiaTheme
import cz.cleansia.customer.ui.theme.selectionTint

@StringRes
internal fun DirtinessLevel.titleRes(): Int = when (this) {
    DirtinessLevel.Normal -> R.string.dirtiness_normal_title
    DirtinessLevel.Increased -> R.string.dirtiness_increased_title
    DirtinessLevel.Heavy -> R.string.dirtiness_heavy_title
}

/** Null for the level that adds nothing, so no summary ever shows a zero surcharge row. */
@StringRes
internal fun DirtinessLevel.surchargeLineRes(): Int? = when (this) {
    DirtinessLevel.Normal -> null
    DirtinessLevel.Increased -> R.string.dirtiness_surcharge_increased
    DirtinessLevel.Heavy -> R.string.dirtiness_surcharge_heavy
}

@StringRes
private fun DirtinessLevel.priceRes(): Int = when (this) {
    DirtinessLevel.Normal -> R.string.dirtiness_normal_price
    DirtinessLevel.Increased -> R.string.dirtiness_increased_price
    DirtinessLevel.Heavy -> R.string.dirtiness_heavy_price
}

@StringRes
private fun DirtinessLevel.whenRes(): Int = when (this) {
    DirtinessLevel.Normal -> R.string.dirtiness_normal_when
    DirtinessLevel.Increased -> R.string.dirtiness_increased_when
    DirtinessLevel.Heavy -> R.string.dirtiness_heavy_when
}

private fun DirtinessLevel.detailRes(): List<Int> = when (this) {
    DirtinessLevel.Normal -> listOf(
        R.string.dirtiness_normal_detail_1,
        R.string.dirtiness_normal_detail_2,
        R.string.dirtiness_normal_detail_3,
        R.string.dirtiness_normal_detail_4,
    )
    DirtinessLevel.Increased -> listOf(
        R.string.dirtiness_increased_detail_1,
        R.string.dirtiness_increased_detail_2,
        R.string.dirtiness_increased_detail_3,
        R.string.dirtiness_increased_detail_4,
    )
    DirtinessLevel.Heavy -> listOf(
        R.string.dirtiness_heavy_detail_1,
        R.string.dirtiness_heavy_detail_2,
        R.string.dirtiness_heavy_detail_3,
        R.string.dirtiness_heavy_detail_4,
    )
}

/** Only the chosen card lists what its level looks like at home, so the three levels fit a small phone. */
internal fun DirtinessLevel.signsShownRes(selected: DirtinessLevel?): List<Int> =
    if (this == selected) detailRes() else emptyList()

/** The booking wizard's level step; its title is the sheet header. */
@Composable
internal fun DirtinessStep(selected: DirtinessLevel?, onSelect: (DirtinessLevel) -> Unit) {
    Column(
        modifier = Modifier
            .fillMaxSize()
            .verticalScroll(rememberScrollState())
            .padding(horizontal = 16.dp, vertical = 8.dp),
    ) {
        DirtinessLevelPicker(selected = selected, onSelect = onSelect)
    }
}

/** The three levels with when each applies and the advice to pick the higher one, shared by the booking and the recurring wizard. */
@Composable
internal fun DirtinessLevelPicker(
    selected: DirtinessLevel?,
    onSelect: (DirtinessLevel) -> Unit,
    modifier: Modifier = Modifier,
) {
    Column(modifier = modifier.fillMaxWidth(), verticalArrangement = Arrangement.spacedBy(8.dp)) {
        Column(
            modifier = Modifier.selectableGroup(),
            verticalArrangement = Arrangement.spacedBy(8.dp),
        ) {
            DirtinessLevel.entries.forEach { level ->
                LevelCard(level = level, chosen = selected, onClick = { onSelect(level) })
            }
        }
        Row(verticalAlignment = Alignment.Top) {
            Icon(
                Icons.Outlined.Info,
                contentDescription = null,
                tint = MaterialTheme.colorScheme.onSurfaceVariant,
                modifier = Modifier.size(16.dp),
            )
            Spacer(Modifier.width(8.dp))
            Text(
                stringResource(R.string.dirtiness_torn_hint),
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
        }
    }
}

@Composable
private fun LevelCard(level: DirtinessLevel, chosen: DirtinessLevel?, onClick: () -> Unit) {
    val selected = level == chosen
    val shape = RoundedCornerShape(16.dp)
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .clip(shape)
            .background(if (selected) selectionTint() else MaterialTheme.colorScheme.surface)
            .border(
                width = if (selected) 2.dp else 1.dp,
                color = if (selected) MaterialTheme.colorScheme.primary else MaterialTheme.colorScheme.outlineVariant,
                shape = shape,
            )
            .selectable(selected = selected, role = Role.RadioButton, onClick = onClick)
            .padding(12.dp),
        verticalAlignment = Alignment.Top,
    ) {
        RadioButton(selected = selected, onClick = null)
        Spacer(Modifier.width(8.dp))
        Column(modifier = Modifier.weight(1f), verticalArrangement = Arrangement.spacedBy(2.dp)) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Text(
                    stringResource(level.titleRes()),
                    style = MaterialTheme.typography.titleMedium,
                    color = MaterialTheme.colorScheme.onSurface,
                    modifier = Modifier.weight(1f),
                )
                Spacer(Modifier.width(8.dp))
                Text(
                    stringResource(level.priceRes()),
                    style = MaterialTheme.typography.labelLarge,
                    color = primaryText(),
                )
            }
            Text(
                stringResource(level.whenRes()),
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
            val signs = level.signsShownRes(chosen)
            if (signs.isNotEmpty()) {
                Column(modifier = Modifier.padding(top = 2.dp), verticalArrangement = Arrangement.spacedBy(2.dp)) {
                    signs.forEach { res ->
                        Row(verticalAlignment = Alignment.Top) {
                            Box(
                                modifier = Modifier
                                    .padding(top = 6.dp)
                                    .size(4.dp)
                                    .background(MaterialTheme.colorScheme.primary, CircleShape),
                            )
                            Spacer(Modifier.width(8.dp))
                            Text(
                                stringResource(res),
                                style = MaterialTheme.typography.bodySmall,
                                color = MaterialTheme.colorScheme.onSurface,
                            )
                        }
                    }
                }
            }
        }
    }
}

@Preview(widthDp = 360, heightDp = 640)
@Composable
private fun DirtinessStepPreview() {
    CleansiaTheme {
        DirtinessStep(selected = DirtinessLevel.Increased, onSelect = {})
    }
}

@Preview(locale = "ru", widthDp = 320, heightDp = 900)
@Composable
private fun DirtinessStepRussianNarrowPreview() {
    CleansiaTheme {
        DirtinessStep(selected = null, onSelect = {})
    }
}
