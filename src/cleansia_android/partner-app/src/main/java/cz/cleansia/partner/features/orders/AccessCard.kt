package cz.cleansia.partner.features.orders

import android.content.ClipData
import android.content.ClipboardManager
import android.content.Context
import android.os.PersistableBundle
import android.widget.Toast
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.offset
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.ContentCopy
import androidx.compose.material.icons.outlined.DoorFront
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.layout.FirstBaseline
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.AnnotatedString
import androidx.compose.ui.unit.dp
import cz.cleansia.core.ui.components.CleansiaTextLink
import cz.cleansia.core.ui.theme.capBandAligned
import cz.cleansia.partner.R

/**
 * How to get in: the mode as a line with its door, floor · flat, and the customer's own words in a well with *Copy*.
 * Each piece is left out when it did not arrive.
 */
@Composable
fun AccessCard(
    mode: String?,
    floorAndFlat: List<String>,
    accessInstructions: String?,
    modifier: Modifier = Modifier,
) {
    val context = LocalContext.current
    Column(modifier = modifier.fillMaxWidth(), verticalArrangement = Arrangement.spacedBy(6.dp)) {
        if (mode != null) {
            val style = MaterialTheme.typography.bodyLarge
            Row {
                Icon(Icons.Outlined.DoorFront, contentDescription = null, tint = MaterialTheme.colorScheme.onSurfaceVariant, modifier = capBandAligned(style).padding(end = 8.dp).size(18.dp))
                Text(text = mode, style = style, color = MaterialTheme.colorScheme.onSurface, modifier = Modifier.alignBy(FirstBaseline))
            }
        }
        if (floorAndFlat.isNotEmpty()) {
            DotFlow(
                items = floorAndFlat.map { AnnotatedString(it) },
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
        }
        if (accessInstructions != null) {
            val copied = stringResource(R.string.instructions_copied)
            Column(
                Modifier
                    .padding(top = 2.dp)
                    .fillMaxWidth()
                    .background(MaterialTheme.colorScheme.surfaceVariant, RoundedCornerShape(16.dp))
                    .padding(start = 16.dp, end = 16.dp, top = 14.dp, bottom = 2.dp),
            ) {
                Text(
                    text = customerText(accessInstructions),
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurface,
                )
                CleansiaTextLink(
                    text = stringResource(R.string.instructions_copy),
                    onClick = { copy(context, accessInstructions, copied) },
                    leadingIcon = Icons.Outlined.ContentCopy,
                    modifier = Modifier.offset(x = (-4).dp),
                )
            }
        }
    }
}

private fun copy(context: Context, text: String, copied: String) {
    val clipboard = context.getSystemService(Context.CLIPBOARD_SERVICE) as? ClipboardManager ?: return
    clipboard.setPrimaryClip(sensitiveClip(copied, text))
    Toast.makeText(context, copied, Toast.LENGTH_SHORT).show()
}

/**
 * The customer's entry instructions open a home, so the clip is marked sensitive (`ClipDescription.EXTRA_IS_SENSITIVE`,
 * API 33, written out for API 26+): the system preview hides it and keyboards keep it out of their history, which no
 * sign-out of ours can clear.
 */
internal fun sensitiveClip(label: String, text: String): ClipData = ClipData.newPlainText(label, text).apply {
    description.extras = PersistableBundle().apply { putBoolean("android.content.extra.IS_SENSITIVE", true) }
}
