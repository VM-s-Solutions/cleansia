package cz.cleansia.partner.features.profile

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.navigationBarsPadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.outlined.ArrowBack
import androidx.compose.material.icons.outlined.CheckCircle
import androidx.compose.material.icons.outlined.DeleteForever
import androidx.compose.material.icons.outlined.Description
import androidx.compose.material.icons.outlined.Handshake
import androidx.compose.material.icons.outlined.ReceiptLong
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Surface
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
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import cz.cleansia.core.ui.components.CleansiaDialog
import cz.cleansia.core.ui.components.CleansiaDestructiveButton
import cz.cleansia.core.ui.state.ActionState
import cz.cleansia.core.ui.theme.Spacing
import cz.cleansia.partner.R

/**
 * Request account deletion — the partner counterpart of the customer app's DeleteAccountScreen,
 * and deliberately a different screen rather than a shared one. → /decisions/adr-0052
 *
 * Three things differ, and each is the point:
 *
 *  1. **It asks, it does not delete.** The CTA files a request an admin fulfils after the
 *     cooperation has been formally ended and the paperwork signed. The copy says so; announcing a
 *     deletion here would be a lie the endpoint used to be able to tell truthfully.
 *  2. **No typed-email gate.** The customer screen makes you retype your address because that action
 *     is irreversible on the spot. This one is a reversible request, so the confirmation dialog
 *     alone is the right amount of friction — more would be theatre.
 *  3. **The session survives.** The cleaner keeps working; there are jobs assigned to them.
 *
 * The "what is kept" list is not decoration. Both stores require the app to say what survives a
 * deletion, and for a cleaner the honest answer is that the financial record does.
 */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun DeleteAccountScreen(
    onNavigateBack: () -> Unit,
    viewModel: DeleteAccountViewModel = hiltViewModel(),
) {
    val state by viewModel.state.collectAsStateWithLifecycle()
    val requested by viewModel.requested.collectAsStateWithLifecycle()
    val submitting = state is ActionState.Submitting

    var confirming by remember { mutableStateOf(false) }

    Scaffold(
        topBar = {
            TopAppBar(
                title = {
                    Text(
                        text = stringResource(R.string.delete_account_title),
                        style = MaterialTheme.typography.titleLarge,
                    )
                },
                navigationIcon = {
                    IconButton(onClick = onNavigateBack) {
                        Icon(
                            imageVector = Icons.AutoMirrored.Outlined.ArrowBack,
                            contentDescription = stringResource(R.string.back),
                        )
                    }
                },
                colors = TopAppBarDefaults.topAppBarColors(
                    containerColor = MaterialTheme.colorScheme.background,
                ),
            )
        },
        containerColor = MaterialTheme.colorScheme.background,
    ) { padding ->
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(padding)
                .verticalScroll(rememberScrollState())
                .padding(horizontal = Spacing.M),
        ) {
            Box(
                modifier = Modifier
                    .padding(top = Spacing.M)
                    .size(56.dp)
                    .clip(CircleShape)
                    .background(MaterialTheme.colorScheme.errorContainer),
                contentAlignment = Alignment.Center,
            ) {
                Icon(
                    imageVector = if (requested) Icons.Outlined.CheckCircle else Icons.Outlined.DeleteForever,
                    contentDescription = null,
                    tint = MaterialTheme.colorScheme.onErrorContainer,
                )
            }

            Spacer(Modifier.height(Spacing.M))

            Text(
                text = stringResource(
                    if (requested) R.string.delete_account_requested_headline
                    else R.string.delete_account_headline,
                ),
                style = MaterialTheme.typography.headlineSmall,
                fontWeight = FontWeight.SemiBold,
            )

            Spacer(Modifier.height(Spacing.XS))

            Text(
                text = stringResource(
                    if (requested) R.string.delete_account_requested_body
                    else R.string.delete_account_body,
                ),
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )

            if (!requested) {
                Spacer(Modifier.height(Spacing.L))

                Text(
                    text = stringResource(R.string.delete_account_kept_label).uppercase(),
                    style = MaterialTheme.typography.labelSmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )

                Spacer(Modifier.height(Spacing.XS))

                Surface(
                    shape = RoundedCornerShape(12.dp),
                    color = MaterialTheme.colorScheme.surface,
                    modifier = Modifier.fillMaxWidth(),
                ) {
                    Column(modifier = Modifier.padding(Spacing.M)) {
                        KeptRow(Icons.Outlined.ReceiptLong, R.string.delete_account_kept_invoices)
                        KeptRow(Icons.Outlined.Description, R.string.delete_account_kept_pay)
                        KeptRow(Icons.Outlined.Handshake, R.string.delete_account_kept_agreement)
                    }
                }

                Spacer(Modifier.height(Spacing.L))

                CleansiaDestructiveButton(
                    text = stringResource(R.string.delete_account_cta),
                    // The dialog is the only gate. Never call submit() straight from here — that is
                    // how a mis-tap becomes a filed request against a colleague's account on a
                    // shared device.
                    onClick = { confirming = true },
                    enabled = !submitting,
                    loading = submitting,
                    modifier = Modifier.fillMaxWidth(),
                )
            }

            Spacer(Modifier.height(Spacing.XL))
            Spacer(Modifier.navigationBarsPadding())
        }
    }

    if (confirming) {
        CleansiaDialog(
            onDismiss = { confirming = false },
            title = stringResource(R.string.delete_account_confirm_title),
            message = stringResource(R.string.delete_account_confirm_message),
            confirmLabel = stringResource(R.string.delete_account_confirm_yes),
            dismissLabel = stringResource(R.string.cancel),
            icon = Icons.Outlined.DeleteForever,
            destructive = true,
            confirmEnabled = !submitting,
            onConfirm = {
                confirming = false
                viewModel.submit()
            },
        )
    }
}

@Composable
private fun KeptRow(icon: ImageVector, textRes: Int) {
    Row(
        verticalAlignment = Alignment.CenterVertically,
        horizontalArrangement = Arrangement.spacedBy(Spacing.S),
        modifier = Modifier.padding(vertical = Spacing.XS),
    ) {
        Icon(
            imageVector = icon,
            contentDescription = null,
            tint = MaterialTheme.colorScheme.onSurfaceVariant,
            modifier = Modifier.size(20.dp),
        )
        Text(
            text = stringResource(textRes),
            style = MaterialTheme.typography.bodyMedium,
        )
    }
}
