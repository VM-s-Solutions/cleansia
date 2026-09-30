package cz.cleansia.partner.features.orders

import androidx.annotation.StringRes
import cz.cleansia.partner.R
import cz.cleansia.partner.api.model.DirtinessLevel

/** Null when the wire carried no level — a server that predates it. */
@StringRes
fun dirtinessLevelLabelRes(level: DirtinessLevel?): Int? = when (level) {
    DirtinessLevel._0 -> R.string.dirtiness_level_normal
    DirtinessLevel._1 -> R.string.dirtiness_level_increased
    DirtinessLevel._2 -> R.string.dirtiness_level_heavy
    null -> null
}

/** The board card flags only a dirtier-than-normal home; the detail names every level. */
@StringRes
fun dirtinessChipLabelRes(level: DirtinessLevel?): Int? =
    if (level == DirtinessLevel._0) null else dirtinessLevelLabelRes(level)
