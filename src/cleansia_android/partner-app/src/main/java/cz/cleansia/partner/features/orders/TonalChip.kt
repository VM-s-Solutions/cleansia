package cz.cleansia.partner.features.orders

import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.layout.FirstBaseline
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.dp
import cz.cleansia.core.ui.theme.capBandAligned

internal enum class ChipTone { Primary, Error }

/**
 * The partner's secondary action: a capsule outlined in the outline, 40 high in Material's 48 tap target, its word in
 * the text ink and only its icon in the tone, the primary or, for [ChipTone.Error], the error. The contacts, Note and
 * Issue are drawn with it; [icon] null or [padding] narrower when a row runs short. Disabled, its content dims as the
 * outlined button's does.
 */
@Composable
internal fun TonalChip(
    label: String,
    icon: ImageVector?,
    onClick: () -> Unit,
    modifier: Modifier = Modifier,
    enabled: Boolean = true,
    padding: Dp = 10.dp,
    tone: ChipTone = ChipTone.Primary,
) {
    val alpha = if (enabled) 1f else 0.38f
    val ink = MaterialTheme.colorScheme.onSurface.copy(alpha = alpha)
    val tint = (if (tone == ChipTone.Error) MaterialTheme.colorScheme.error else MaterialTheme.colorScheme.primary).copy(alpha = alpha)
    Surface(
        onClick = onClick,
        enabled = enabled,
        shape = CircleShape,
        color = Color.Transparent,
        border = BorderStroke(1.dp, MaterialTheme.colorScheme.outline),
        modifier = modifier,
    ) {
        Box(Modifier.heightIn(min = 40.dp).padding(horizontal = padding), contentAlignment = Alignment.Center) {
            Row {
                val style = tonalChipLabel()
                if (icon != null) Icon(imageVector = icon, contentDescription = null, tint = tint, modifier = capBandAligned(style).padding(end = 6.dp).size(18.dp))
                Text(text = label, style = style, color = ink, softWrap = false, maxLines = 1, modifier = Modifier.alignBy(FirstBaseline))
            }
        }
    }
}

@Composable
internal fun tonalChipLabel(): TextStyle = MaterialTheme.typography.labelLarge.copy(fontWeight = FontWeight.SemiBold)
