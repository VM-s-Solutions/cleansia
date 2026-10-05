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
import androidx.compose.foundation.layout.height
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
import androidx.compose.material.icons.outlined.Check
import androidx.compose.material.icons.outlined.Info
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import cz.cleansia.customer.R
import cz.cleansia.customer.core.booking.DirtinessLevel
import cz.cleansia.customer.ui.theme.CleansiaTheme
import cz.cleansia.customer.ui.theme.selectionTint
import cz.cleansia.customer.ui.theme.primaryText

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

/** The booking wizard's level step; its title is the sheet header. */
@Composable
internal fun DirtinessStep(selected: DirtinessLevel?, onSelect: (DirtinessLevel) -> Unit) {
    Column(
        modifier = Modifier
            .fillMaxSize()
            .verticalScroll(rememberScrollState())
            .padding(20.dp),
    ) {
        DirtinessLevelPicker(selected = selected, onSelect = onSelect)
    }
}

/** The three levels with the descriptions the customer chooses by, shared by the booking and the recurring wizard. */
@Composable
internal fun DirtinessLevelPicker(
    selected: DirtinessLevel?,
    onSelect: (DirtinessLevel) -> Unit,
    modifier: Modifier = Modifier,
) {
    Column(modifier = modifier.fillMaxWidth(), verticalArrangement = Arrangement.spacedBy(12.dp)) {
        Text(
            stringResource(R.string.dirtiness_intro),
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .clip(RoundedCornerShape(12.dp))
                .background(MaterialTheme.colorScheme.primary.copy(alpha = 0.08f))
                .padding(12.dp),
            verticalAlignment = Alignment.Top,
        ) {
            Icon(
                Icons.Outlined.Info,
                contentDescription = null,
                tint = MaterialTheme.colorScheme.primary,
                modifier = Modifier.size(18.dp),
            )
            Spacer(Modifier.width(8.dp))
            Text(
                stringResource(R.string.dirtiness_hint),
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurface,
            )
        }
        Column(
            modifier = Modifier.selectableGroup(),
            verticalArrangement = Arrangement.spacedBy(10.dp),
        ) {
            DirtinessLevel.entries.forEach { level ->
                LevelCard(level = level, selected = level == selected, onClick = { onSelect(level) })
            }
        }
    }
}

@Composable
private fun LevelCard(level: DirtinessLevel, selected: Boolean, onClick: () -> Unit) {
    val shape = RoundedCornerShape(16.dp)
    Column(
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
            .padding(14.dp),
    ) {
        Row(verticalAlignment = Alignment.CenterVertically) {
            Text(
                stringResource(level.titleRes()),
                style = MaterialTheme.typography.titleSmall.copy(fontWeight = FontWeight.SemiBold),
                color = MaterialTheme.colorScheme.onSurface,
                modifier = Modifier.weight(1f),
            )
            Text(
                stringResource(level.priceRes()),
                style = MaterialTheme.typography.labelLarge.copy(fontWeight = FontWeight.SemiBold),
                color = if (level == DirtinessLevel.Normal) {
                    MaterialTheme.colorScheme.onSurfaceVariant
                } else {
                    primaryText()
                },
            )
            if (selected) {
                Spacer(Modifier.width(8.dp))
                Box(
                    modifier = Modifier
                        .size(22.dp)
                        .background(MaterialTheme.colorScheme.primary, CircleShape),
                    contentAlignment = Alignment.Center,
                ) {
                    Icon(Icons.Outlined.Check, contentDescription = null, tint = Color.White, modifier = Modifier.size(14.dp))
                }
            }
        }
        Spacer(Modifier.height(6.dp))
        Text(
            stringResource(level.whenRes()),
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurface,
        )
        Spacer(Modifier.height(6.dp))
        level.detailRes().forEach { res ->
            Row(modifier = Modifier.padding(vertical = 2.dp)) {
                Text(
                    "•",
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
                Spacer(Modifier.width(6.dp))
                Text(
                    stringResource(res),
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
            }
        }
    }
}

@Preview(widthDp = 390, heightDp = 1100)
@Composable
private fun DirtinessStepPreview() {
    CleansiaTheme {
        DirtinessStep(selected = DirtinessLevel.Increased, onSelect = {})
    }
}

@Preview(locale = "ru", widthDp = 320, heightDp = 1300)
@Composable
private fun DirtinessStepRussianNarrowPreview() {
    CleansiaTheme {
        DirtinessStep(selected = null, onSelect = {})
    }
}
