package cz.cleansia.core.ui.components

import androidx.compose.foundation.Image
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.painter.Painter
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.dp

/**
 * Friendly empty-state surface that puts a mascot illustration above a
 * single line of guidance text. The painter is passed in so each calling
 * app can supply its own drawable from its own `res/drawable*` folder —
 * `:core` does not own the artwork, only the layout.
 *
 * Two layout modes:
 *  - [verticallyCentered] = false (default): mascot anchored at a fixed
 *    distance from the top via [topSpacer]. Use this on screens that
 *    share a swipeable region with other tabs of differing chrome
 *    heights (Orders Available/Active/History) — otherwise the mascot
 *    visibly jumps as the user swipes between tabs.
 *  - [verticallyCentered] = true: mascot true-centered in the available
 *    region. Use this on stand-alone screens (Invoices) where there is
 *    no sibling tab whose chrome height the mascot must align to.
 */
@Composable
fun MascotEmptyState(
    painter: Painter,
    text: String,
    modifier: Modifier = Modifier,
    topSpacer: Dp = 220.dp,
    verticallyCentered: Boolean = false,
) {
    Column(
        modifier = modifier
            .fillMaxSize()
            .padding(horizontal = 32.dp),
        horizontalAlignment = Alignment.CenterHorizontally,
        verticalArrangement = if (verticallyCentered) Arrangement.Center else Arrangement.Top,
    ) {
        if (!verticallyCentered) {
            Spacer(Modifier.height(topSpacer))
        }
        Image(
            painter = painter,
            contentDescription = null,
            modifier = Modifier.size(180.dp),
        )
        Spacer(Modifier.height(16.dp))
        Text(
            text = text,
            style = MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.SemiBold),
            color = MaterialTheme.colorScheme.onSurface,
            textAlign = TextAlign.Center,
        )
    }
}
