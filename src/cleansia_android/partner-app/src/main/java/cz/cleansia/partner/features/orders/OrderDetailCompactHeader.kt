package cz.cleansia.partner.features.orders

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.MaterialTheme
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.unit.dp

/**
 * Drag-handle slot content for the [BottomSheetScaffold]. Just the
 * grabber + clearance space for the [FloatingMascot] to land on; all
 * order metadata moved into the scroll content below
 * (see [OrderMetadataRow]) so the timer card can be the first thing
 * the cleaner sees.
 */
@Composable
fun OrderDetailCompactHeader(modifier: Modifier = Modifier) {
    Column(
        modifier = modifier
            .fillMaxWidth()
            .padding(top = 8.dp, bottom = 0.dp),
        horizontalAlignment = Alignment.CenterHorizontally,
    ) {
        Grabber()
        // The 128dp mascot's bottom half (~64dp) lands over the sheet,
        // but it's right-aligned with 16dp right padding, so its left
        // edge sits ~144dp from the screen's right side. The timer
        // card content is left-aligned (text starts at sheet's left
        // padding) and the mascot can overlap its right side without
        // covering anything important. So we want just a small gap
        // here so the timer card pulls up close to the sheet border.
        Spacer(Modifier.height(8.dp))
    }
}

@Composable
private fun Grabber() {
    Box(
        modifier = Modifier
            .width(36.dp)
            .height(4.dp)
            .clip(RoundedCornerShape(2.dp))
            .background(MaterialTheme.colorScheme.outlineVariant),
    )
}
