package cz.cleansia.partner.features.orders

import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.AnnotatedString
import androidx.compose.ui.text.SpanStyle
import androidx.compose.ui.text.buildAnnotatedString
import androidx.compose.ui.text.intl.LocaleList
import androidx.compose.ui.text.withStyle
import androidx.compose.ui.unit.dp
import cz.cleansia.partner.R

/**
 * The customer's words in the Customer section: *Notes* and *Special instructions*, each a label over the text as the
 * customer wrote it. Nothing when neither arrived.
 */
@Composable
fun FromCustomerNotesCard(
    customerNotes: String?,
    specialInstructions: String?,
    modifier: Modifier = Modifier,
) {
    val notes = customerNotes?.takeIf { it.isNotBlank() }
    val special = specialInstructions?.takeIf { it.isNotBlank() }
    if (notes == null && special == null) return
    Column(modifier) {
        notes?.let { CustomerWords(stringResource(R.string.note_general_label), it) }
        special?.let { CustomerWords(stringResource(R.string.note_special_label), it, Modifier.padding(top = if (notes != null) 12.dp else 0.dp)) }
    }
}

@Composable
private fun CustomerWords(label: String, text: String, modifier: Modifier = Modifier) {
    Column(modifier) {
        WordsLabel(label)
        Text(
            text = customerText(text),
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurface,
            modifier = Modifier.padding(top = 2.dp),
        )
    }
}

/** The customer's own words carry the company's language, so TalkBack reads them in Czech inside a uk or en app. */
internal fun customerText(text: String): AnnotatedString = buildAnnotatedString {
    withStyle(SpanStyle(localeList = LocaleList("cs"))) { append(OrdersBoardRules.tie(text)) }
}
