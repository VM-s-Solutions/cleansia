package cz.cleansia.core.ui.components

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.dp
import cz.cleansia.core.ui.theme.primaryIconOnContainer

/** The halo's diameter, for a row that lines its other lines up past it. */
val IconHaloSize: Dp = 44.dp

/** A card's leading glyph on the brand-tinted primaryContainer disc. */
@Composable
fun IconHalo(icon: ImageVector) {
    Box(
        modifier = Modifier
            .size(IconHaloSize)
            .background(MaterialTheme.colorScheme.primaryContainer, CircleShape),
        contentAlignment = Alignment.Center,
    ) {
        Icon(
            imageVector = icon,
            contentDescription = null,
            tint = MaterialTheme.colorScheme.primaryIconOnContainer,
            modifier = Modifier.size(22.dp),
        )
    }
}
