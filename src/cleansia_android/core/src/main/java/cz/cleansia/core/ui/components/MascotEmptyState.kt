package cz.cleansia.core.ui.components

import androidx.compose.foundation.Image
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.widthIn
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
 *
 * An empty state that has a single obvious next step (book your first
 * cleaning, find work) can pass [actionLabel] + [onAction] to get a primary
 * CTA under the text. Both must be non-null or nothing is rendered — the
 * dead-end variant stays the default, because most empty states here are
 * genuinely "nothing to do yet" rather than "do this".
 *
 * The action params sit at the END of the list, after [verticallyCentered],
 * on purpose: every existing call site passes `painter`/`text` and then the
 * rest by name, and slotting a new parameter into the middle would silently
 * rebind any future positional argument. Keep new params here.
 */
@Composable
fun MascotEmptyState(
    painter: Painter,
    text: String,
    modifier: Modifier = Modifier,
    topSpacer: Dp = 220.dp,
    verticallyCentered: Boolean = false,
    actionLabel: String? = null,
    onAction: (() -> Unit)? = null,
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
        if (actionLabel != null && onAction != null) {
            Spacer(Modifier.height(24.dp))
            CleansiaPrimaryButton(
                text = actionLabel,
                onClick = onAction,
                size = CleansiaButtonSize.Medium,
                // The button fills its width; the cap stops it stretching to
                // the full 32.dp-inset column on tablets, where a full-bleed
                // pill under a 180.dp mascot looks like a page footer.
                modifier = Modifier.widthIn(max = 280.dp),
            )
        }
    }
}
