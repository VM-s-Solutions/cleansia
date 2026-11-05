package cz.cleansia.partner.features.orders

import androidx.annotation.StringRes
import cz.cleansia.partner.R
import cz.cleansia.partner.api.model.DirtinessLevel

/** Dirt is said only when it is more than normal; null for a normal home or a server that predates the level. */
@StringRes
fun dirtinessLevelLabelRes(level: DirtinessLevel?): Int? = when (level) {
    DirtinessLevel._1 -> R.string.dirtiness_level_increased
    DirtinessLevel._2 -> R.string.dirtiness_level_heavy
    DirtinessLevel._0, null -> null
}

/** The board card and the detail flag the same levels. */
@StringRes
fun dirtinessChipLabelRes(level: DirtinessLevel?): Int? = dirtinessLevelLabelRes(level)
