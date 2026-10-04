package cz.cleansia.customer.features.profile

import android.content.ActivityNotFoundException
import android.content.ClipData
import android.content.ClipboardManager
import android.content.Context
import android.content.Intent
import android.net.Uri
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.outlined.ArrowBack
import androidx.compose.material.icons.automirrored.outlined.ArrowForwardIos
import androidx.compose.material.icons.outlined.Email
import androidx.compose.material.icons.outlined.ExpandLess
import androidx.compose.material.icons.outlined.ExpandMore
import androidx.compose.material.icons.outlined.Phone
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.material3.TopAppBar
import androidx.compose.material3.TopAppBarDefaults
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.semantics.heading
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import cz.cleansia.core.format.formatOrderPrice
import cz.cleansia.customer.R
import cz.cleansia.customer.core.market.InsuranceCoverage
import cz.cleansia.customer.ui.theme.CleansiaTheme
import cz.cleansia.core.ui.theme.Poppins

private data class FaqItem(val qRes: Int, val answer: @Composable () -> String)

/** The one support contact (owner ruling 2026-10-02) and the line the customer web footer prints. */
private const val SUPPORT_EMAIL = "support@cleansia.cz"
private const val SUPPORT_PHONE = "+420739788108"

/** Answer 3 states the insurance ceiling only when the market authored one (ADR-0060 D2). */
private fun faqs(insuranceCoverage: InsuranceCoverage?) = listOf(
    FaqItem(R.string.help_faq_q1) { stringResource(R.string.help_faq_a1) },
    FaqItem(R.string.help_faq_q2) { stringResource(R.string.help_faq_a2) },
    FaqItem(R.string.help_faq_q3) {
        insuranceCoverage?.let { coverage ->
            stringResource(R.string.help_faq_a3, formatOrderPrice(coverage.amount, coverage.currencyCode))
        } ?: stringResource(R.string.help_faq_a3_no_figure)
    },
    FaqItem(R.string.help_faq_q4) { stringResource(R.string.help_faq_a4) },
    FaqItem(R.string.help_faq_q5) { stringResource(R.string.help_faq_a5) },
)

@Composable
fun HelpSupportScreen(
    onBack: () -> Unit = {},
    viewModel: HelpSupportViewModel = hiltViewModel(),
) {
    val context = LocalContext.current
    val insuranceCoverage by viewModel.insuranceCoverage.collectAsStateWithLifecycle()
    HelpSupportScreenContent(
        insuranceCoverage = insuranceCoverage,
        onBack = onBack,
        onCall = {
            val dial = Intent(Intent.ACTION_DIAL, Uri.parse("tel:$SUPPORT_PHONE"))
            if (!context.openOrCopy(dial, SUPPORT_PHONE)) viewModel.onCallUnavailable()
        },
        onEmail = {
            val mail = Intent(Intent.ACTION_SENDTO, Uri.parse("mailto:$SUPPORT_EMAIL"))
            if (!context.openOrCopy(mail, SUPPORT_EMAIL)) viewModel.onEmailUnavailable()
        },
    )
}

/** Starts [intent]; when no app on the device handles it, copies [value] instead and returns false. */
private fun Context.openOrCopy(intent: Intent, value: String): Boolean = try {
    startActivity(intent)
    true
} catch (_: ActivityNotFoundException) {
    (getSystemService(Context.CLIPBOARD_SERVICE) as? ClipboardManager)
        ?.setPrimaryClip(ClipData.newPlainText(value, value))
    false
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun HelpSupportScreenContent(
    insuranceCoverage: InsuranceCoverage?,
    onBack: () -> Unit,
    onCall: () -> Unit,
    onEmail: () -> Unit,
) {
    val faqs = faqs(insuranceCoverage)
    Column(
        modifier = Modifier
            .fillMaxSize()
            .background(MaterialTheme.colorScheme.background),
    ) {
        TopAppBar(
            title = { Text(stringResource(R.string.help_title), style = MaterialTheme.typography.titleMedium.copy(fontFamily = Poppins, fontWeight = FontWeight.SemiBold)) },
            navigationIcon = {
                IconButton(onClick = onBack) { Icon(Icons.AutoMirrored.Outlined.ArrowBack, stringResource(R.string.common_back)) }
            },
            colors = TopAppBarDefaults.topAppBarColors(containerColor = MaterialTheme.colorScheme.surface),
        )

        // iOS's Help is the reference for this screen (owner, 2026-10-04): two labelled sections, the
        // contact rows in one card, then each question in a card of its own.
        Column(
            modifier = Modifier
                .verticalScroll(rememberScrollState())
                .padding(16.dp),
            verticalArrangement = Arrangement.spacedBy(24.dp),
        ) {
            HelpSection(stringResource(R.string.help_contact_title)) {
                Column(
                    modifier = Modifier
                        .fillMaxWidth()
                        .clip(RoundedCornerShape(16.dp))
                        .background(MaterialTheme.colorScheme.surface),
                ) {
                    ContactRow(
                        icon = Icons.Outlined.Email,
                        title = stringResource(R.string.help_email),
                        subtitle = SUPPORT_EMAIL,
                        onClick = onEmail,
                    )
                    HorizontalDivider(
                        modifier = Modifier.padding(start = 56.dp),
                        color = MaterialTheme.colorScheme.outlineVariant,
                    )
                    ContactRow(
                        icon = Icons.Outlined.Phone,
                        title = stringResource(R.string.help_call),
                        subtitle = stringResource(R.string.help_call_desc),
                        onClick = onCall,
                    )
                }
            }

            HelpSection(stringResource(R.string.help_faq_title)) {
                faqs.forEach { faq -> FaqRow(faq) }
            }
            Spacer(Modifier.height(8.dp))
        }
    }
}

/** A small upper-case label over its rows, as iOS's Help draws each section. */
@Composable
private fun HelpSection(title: String, content: @Composable () -> Unit) {
    Column(verticalArrangement = Arrangement.spacedBy(12.dp)) {
        Text(
            title.uppercase(),
            style = MaterialTheme.typography.labelSmall.copy(fontWeight = FontWeight.SemiBold),
            color = MaterialTheme.colorScheme.onSurfaceVariant,
            modifier = Modifier.semantics { heading() },
        )
        content()
    }
}

@Composable
private fun ContactRow(icon: ImageVector, title: String, subtitle: String, onClick: () -> Unit) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .clickable(role = Role.Button, onClick = onClick)
            .padding(16.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Icon(icon, null, tint = MaterialTheme.colorScheme.primary, modifier = Modifier.size(24.dp))
        Spacer(Modifier.width(16.dp))
        Column(modifier = Modifier.weight(1f)) {
            Text(title, style = MaterialTheme.typography.bodyLarge, color = MaterialTheme.colorScheme.onSurface)
            Text(subtitle, style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
        }
        Icon(
            Icons.AutoMirrored.Outlined.ArrowForwardIos,
            null,
            tint = MaterialTheme.colorScheme.onSurfaceVariant,
            modifier = Modifier.size(14.dp),
        )
    }
}

@Composable
private fun FaqRow(faq: FaqItem) {
    var expanded by remember { mutableStateOf(false) }
    Column(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(16.dp))
            .background(MaterialTheme.colorScheme.surface)
            .clickable(role = Role.Button) { expanded = !expanded }
            .padding(16.dp),
    ) {
        Row(verticalAlignment = Alignment.CenterVertically) {
            Text(
                stringResource(faq.qRes),
                style = MaterialTheme.typography.titleMedium,
                color = MaterialTheme.colorScheme.onSurface,
                modifier = Modifier.weight(1f),
            )
            Spacer(Modifier.width(8.dp))
            Icon(
                if (expanded) Icons.Outlined.ExpandLess else Icons.Outlined.ExpandMore,
                null,
                tint = MaterialTheme.colorScheme.onSurfaceVariant,
                modifier = Modifier.size(20.dp),
            )
        }
        if (expanded) {
            Spacer(Modifier.height(8.dp))
            Text(
                faq.answer(),
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
        }
    }
}

@Preview(widthDp = 390, heightDp = 900)
@Composable
private fun HelpPreview() {
    CleansiaTheme {
        HelpSupportScreenContent(insuranceCoverage = null, onBack = {}, onCall = {}, onEmail = {})
    }
}
