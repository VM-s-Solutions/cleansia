package cz.cleansia.partner.features.orders.components

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import cz.cleansia.partner.R

/**
 * "How to get in" card. Promoted to its own component (split out from
 * the old FromCustomerNotesCard) because access info is the cleaner's
 * first blocker once they're at the building — if they can't find the
 * front door, nothing else on the screen matters yet.
 *
 * Visually amber-tinted to stand out from the standard surface cards;
 * the cleaner shouldn't have to hunt for it in a sea of grey blocks.
 */
@Composable
fun AccessCard(
    accessInstructions: String,
    modifier: Modifier = Modifier,
) {
    // Amber 50 / amber 800-ish palette — picked to read on both light
    // and dark surface colors without needing a separate dark-mode
    // override. Solid (not translucent) so it doesn't pick up artifacts
    // when sitting over the map's blurred edge.
    val amberBg = Color(0xFFFFF7E0)
    val amberFg = Color(0xFF8A6100)
    val amberMedallion = Color(0xFFF59E0B)

    Surface(
        modifier = modifier.fillMaxWidth(),
        shape = RoundedCornerShape(16.dp),
        color = amberBg,
    ) {
        Column(modifier = Modifier.padding(16.dp)) {
            Row(
                verticalAlignment = Alignment.CenterVertically,
                horizontalArrangement = Arrangement.spacedBy(12.dp),
            ) {
                KeyMedallion(background = amberMedallion)
                Text(
                    text = stringResource(R.string.access_section_title),
                    style = MaterialTheme.typography.titleSmall.copy(fontWeight = FontWeight.Bold),
                    color = amberFg,
                )
            }
            Spacer(Modifier.height(10.dp))
            Text(
                text = accessInstructions,
                style = MaterialTheme.typography.bodyLarge.copy(fontWeight = FontWeight.Medium),
                color = Color(0xFF1F1A0B),
            )
        }
    }
}

@Composable
private fun KeyMedallion(background: Color) {
    androidx.compose.foundation.layout.Box(
        modifier = Modifier
            .size(32.dp)
            .background(color = background, shape = CircleShape),
        contentAlignment = Alignment.Center,
    ) {
        // Plain text glyph instead of a vector to keep the bundle slim
        // and the medallion legible at small sizes.
        Text(
            text = "🔑",
            style = MaterialTheme.typography.titleMedium,
        )
    }
}
