package cz.cleansia.partner.features.profile

import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxHeight
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.navigationBarsPadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.outlined.KeyboardArrowRight
import androidx.compose.material.icons.outlined.CheckCircle
import androidx.compose.material.icons.outlined.Description
import androidx.compose.material.icons.outlined.ErrorOutline
import androidx.compose.material.icons.outlined.Info
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.ModalBottomSheet
import androidx.compose.material3.Text
import androidx.compose.material3.rememberModalBottomSheetState
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import cz.cleansia.core.format.formatOrderDateTime
import cz.cleansia.core.ui.components.CleansiaPrimaryButton
import cz.cleansia.core.ui.components.HtmlContentView
import cz.cleansia.core.ui.state.ActionState
import cz.cleansia.core.ui.theme.Spacing
import cz.cleansia.partner.R
import cz.cleansia.partner.api.model.LegalDocumentType
import cz.cleansia.partner.data.profile.CleanerLegalDocument
import cz.cleansia.partner.ui.theme.CleansiaPartnerTheme

/**
 * The open document is remembered by its type, not its text row, so a re-read after a newer version
 * came into force shows the new text in the sheet that is already open.
 */
@Composable
fun LegalDocumentsScreen(
    onNavigateBack: () -> Unit,
    viewModel: LegalDocumentsViewModel = hiltViewModel(),
) {
    val uiState by viewModel.uiState.collectAsStateWithLifecycle()
    val actionState by viewModel.actionState.collectAsStateWithLifecycle()
    val notice by viewModel.notice.collectAsStateWithLifecycle()
    var openType by rememberSaveable { mutableStateOf<Int?>(null) }

    LaunchedEffect(viewModel) { viewModel.accepted.collect { openType = null } }

    LegalDocumentsScreenContent(
        uiState = uiState,
        onNavigateBack = onNavigateBack,
        onRetry = viewModel::retry,
        onOpen = { openType = it.type.value },
    )

    val open = (uiState as? LegalDocumentsUiState.Loaded)?.documents?.firstOrNull { it.type.value == openType }
    if (open != null) {
        LegalDocumentSheet(
            document = open,
            notice = notice,
            actionState = actionState,
            onAccept = { viewModel.accept(open) },
            onDismiss = {
                openType = null
                viewModel.onDocumentClosed()
            },
        )
    }
}

@Composable
fun LegalDocumentsScreenContent(
    uiState: LegalDocumentsUiState,
    onNavigateBack: () -> Unit,
    onRetry: () -> Unit,
    onOpen: (CleanerLegalDocument) -> Unit,
) {
    SectionScaffold(
        title = stringResource(R.string.legal_documents_title),
        isLoading = uiState is LegalDocumentsUiState.Loading,
        onNavigateBack = onNavigateBack,
        isError = uiState is LegalDocumentsUiState.Error,
        onRetry = onRetry,
    ) {
        val documents = (uiState as? LegalDocumentsUiState.Loaded)?.documents.orEmpty()
        Text(
            text = stringResource(
                if (documents.isEmpty()) R.string.legal_documents_empty else R.string.legal_documents_intro,
            ),
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
        Spacer(Modifier.height(Spacing.M))
        Column(verticalArrangement = Arrangement.spacedBy(Spacing.S)) {
            documents.forEach { document ->
                LegalDocumentRow(document = document, onClick = { onOpen(document) })
            }
        }
    }
}

@Composable
private fun LegalDocumentRow(document: CleanerLegalDocument, onClick: () -> Unit) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(16.dp))
            .background(MaterialTheme.colorScheme.surface)
            .border(1.dp, MaterialTheme.colorScheme.outlineVariant, RoundedCornerShape(16.dp))
            .clickable(onClick = onClick)
            .padding(Spacing.S),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Icon(
            imageVector = Icons.Outlined.Description,
            contentDescription = null,
            tint = MaterialTheme.colorScheme.primary,
            modifier = Modifier.size(24.dp),
        )
        Spacer(Modifier.width(Spacing.S))
        Column(modifier = Modifier.weight(1f)) {
            Text(
                text = document.title,
                style = MaterialTheme.typography.titleSmall.copy(fontWeight = FontWeight.SemiBold),
                color = MaterialTheme.colorScheme.onSurface,
            )
            Text(
                text = stringResource(R.string.legal_documents_version, document.version),
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
            AcceptanceLine(document = document)
        }
        Icon(
            imageVector = Icons.AutoMirrored.Outlined.KeyboardArrowRight,
            contentDescription = null,
            tint = MaterialTheme.colorScheme.onSurfaceVariant,
        )
    }
}

@Composable
private fun AcceptanceLine(document: CleanerLegalDocument) {
    val accepted = document.isAccepted
    Row(verticalAlignment = Alignment.CenterVertically) {
        Icon(
            imageVector = if (accepted) Icons.Outlined.CheckCircle else Icons.Outlined.ErrorOutline,
            contentDescription = null,
            tint = if (accepted) MaterialTheme.colorScheme.primary else MaterialTheme.colorScheme.error,
            modifier = Modifier.size(14.dp),
        )
        Spacer(Modifier.width(4.dp))
        Text(
            text = when {
                accepted -> stringResource(
                    R.string.legal_documents_accepted_on,
                    formatOrderDateTime(document.acceptedAt),
                    document.acceptedVersion ?: document.version,
                )
                document.acceptedVersion != null ->
                    stringResource(R.string.legal_documents_new_version, document.acceptedVersion)
                else -> stringResource(R.string.legal_documents_awaiting)
            },
            style = MaterialTheme.typography.bodySmall.copy(fontWeight = FontWeight.Medium),
            color = if (accepted) MaterialTheme.colorScheme.primary else MaterialTheme.colorScheme.error,
        )
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun LegalDocumentSheet(
    document: CleanerLegalDocument,
    notice: LegalDocumentNotice?,
    actionState: ActionState,
    onAccept: () -> Unit,
    onDismiss: () -> Unit,
) {
    val submitting = actionState is ActionState.Submitting
    ModalBottomSheet(
        onDismissRequest = { if (!submitting) onDismiss() },
        sheetState = rememberModalBottomSheetState(skipPartiallyExpanded = true),
        containerColor = MaterialTheme.colorScheme.surface,
    ) {
        LegalDocumentSheetContent(
            document = document,
            notice = notice,
            error = (actionState as? ActionState.Error)?.message,
            submitting = submitting,
            onAccept = onAccept,
            onClose = onDismiss,
        )
    }
}

@Composable
fun LegalDocumentSheetContent(
    document: CleanerLegalDocument,
    notice: LegalDocumentNotice?,
    error: String?,
    submitting: Boolean,
    onAccept: () -> Unit,
    onClose: () -> Unit,
) {
    Column(
        modifier = Modifier
            .fillMaxWidth()
            .fillMaxHeight()
            .padding(horizontal = Spacing.M)
            .navigationBarsPadding(),
    ) {
        Text(
            text = document.title,
            style = MaterialTheme.typography.titleLarge.copy(fontWeight = FontWeight.Bold),
            color = MaterialTheme.colorScheme.onSurface,
        )
        Text(
            text = stringResource(R.string.legal_documents_version, document.version),
            style = MaterialTheme.typography.labelMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
        if (notice == LegalDocumentNotice.TextUpdated) {
            Spacer(Modifier.height(Spacing.S))
            Row(
                modifier = Modifier
                    .fillMaxWidth()
                    .background(MaterialTheme.colorScheme.tertiaryContainer, RoundedCornerShape(12.dp))
                    .padding(Spacing.S),
                verticalAlignment = Alignment.CenterVertically,
                horizontalArrangement = Arrangement.spacedBy(Spacing.XS),
            ) {
                Icon(
                    imageVector = Icons.Outlined.Info,
                    contentDescription = null,
                    tint = MaterialTheme.colorScheme.onTertiaryContainer,
                    modifier = Modifier.size(18.dp),
                )
                Text(
                    text = stringResource(R.string.legal_documents_text_updated),
                    style = MaterialTheme.typography.bodySmall.copy(fontWeight = FontWeight.SemiBold),
                    color = MaterialTheme.colorScheme.onTertiaryContainer,
                )
            }
        }
        Spacer(Modifier.height(Spacing.S))
        HorizontalDivider(color = MaterialTheme.colorScheme.outlineVariant)
        HtmlContentView(
            html = document.contentHtml,
            modifier = Modifier
                .fillMaxWidth()
                .weight(1f),
        )
        HorizontalDivider(color = MaterialTheme.colorScheme.outlineVariant)
        Spacer(Modifier.height(Spacing.S))
        error?.let {
            Text(
                text = it,
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.error,
            )
            Spacer(Modifier.height(Spacing.S))
        }
        if (document.isAccepted) {
            AcceptanceLine(document = document)
            Spacer(Modifier.height(Spacing.S))
            CleansiaPrimaryButton(text = stringResource(R.string.close), onClick = onClose)
        } else {
            CleansiaPrimaryButton(
                text = stringResource(R.string.legal_documents_accept),
                onClick = onAccept,
                loading = submitting,
            )
        }
        Spacer(Modifier.height(Spacing.M))
    }
}

private val previewDocument = CleanerLegalDocument(
    type = LegalDocumentType._3,
    legalDocumentTextId = "text-1",
    version = "2026-12-01",
    title = "Framework contract",
    contentHtml = "<h2>Parties</h2><p>This framework contract is concluded between the company and the partner.</p>",
    isAccepted = false,
    acceptedVersion = "2026-10-01",
    acceptedAt = "2026-10-02T08:30:00Z",
)

@Preview
@Composable
private fun LegalDocumentsScreenPreview() {
    CleansiaPartnerTheme {
        LegalDocumentsScreenContent(
            uiState = LegalDocumentsUiState.Loaded(
                listOf(
                    previewDocument,
                    previewDocument.copy(
                        type = LegalDocumentType._5,
                        legalDocumentTextId = "text-2",
                        title = "Data processing agreement",
                        isAccepted = true,
                        acceptedVersion = "2026-12-01",
                    ),
                ),
            ),
            onNavigateBack = {},
            onRetry = {},
            onOpen = {},
        )
    }
}

@Preview
@Composable
private fun LegalDocumentSheetPreview() {
    CleansiaPartnerTheme {
        LegalDocumentSheetContent(
            document = previewDocument,
            notice = LegalDocumentNotice.TextUpdated,
            error = null,
            submitting = false,
            onAccept = {},
            onClose = {},
        )
    }
}
