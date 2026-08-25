package cz.cleansia.partner.features.profile

import android.net.Uri
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.outlined.ArrowBack
import androidx.compose.material.icons.outlined.Add
import androidx.compose.material.icons.outlined.Delete
import androidx.compose.material.icons.outlined.SwapHoriz
import androidx.compose.material.icons.outlined.Description
import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.FloatingActionButton
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
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import cz.cleansia.core.ui.components.CleansiaDialog
import cz.cleansia.core.ui.components.CleansiaTextField
import cz.cleansia.core.ui.theme.Spacing
import cz.cleansia.partner.R
import cz.cleansia.partner.api.model.DocumentStatus
import cz.cleansia.partner.api.model.DocumentType
import cz.cleansia.partner.api.model.GetMyDocumentsMyDocumentDto
import cz.cleansia.partner.api.model.MyDocumentRequirementDto

/**
 * My-documents screen — what the cleaner's country asks for, what they have uploaded, and the two
 * things they can do to a document they already own.
 *
 * **Replacing and requesting deletion are deliberately different doors.** Replacing needs no admin
 * because the slot never empties — the new version is created before the old one is retired, so the
 * registration lock never re-engages. Removal is for the case where nothing should be there at all,
 * and that one an employer has to agree with: the request changes nothing until an admin answers it.
 *
 * Both are behind a confirmation. The delete button this replaced removed the document on the first
 * tap with no dialog on either platform, and the soft-delete re-engaged the registration lock.
 */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun DocumentsSectionScreen(
    onNavigateBack: () -> Unit,
    viewModel: DocumentsSectionViewModel = hiltViewModel(),
) {
    val uiState by viewModel.uiState.collectAsStateWithLifecycle()
    val uploadState by viewModel.uploadState.collectAsStateWithLifecycle()
    val deletingId by viewModel.deletingId.collectAsStateWithLifecycle()
    val uploading = uploadState is cz.cleansia.core.ui.state.ActionState.Submitting
    val documents = (uiState as? DocumentsSectionUiState.Loaded)?.documents.orEmpty()

    // Pending pick — the VM reads and encodes the picked file off the main
    // thread and publishes it here; the metadata dialog opens on it. Null again
    // after the upload is fired or the dialog is cancelled.
    val pendingFile by viewModel.pendingFile.collectAsStateWithLifecycle()
    val preparing by viewModel.isPreparing.collectAsStateWithLifecycle()
    val requirements by viewModel.requirements.collectAsStateWithLifecycle()

    // Which document the next pick replaces, and which one is being asked about. rememberSaveable:
    // a rotation mid-decision must not silently drop the flow the cleaner was in.
    var replacingDocumentId by rememberSaveable { mutableStateOf<String?>(null) }
    var deletionTarget by rememberSaveable { mutableStateOf<String?>(null) }

    // Hands the Uri straight to the VM. This callback runs on the MAIN thread,
    // so the openInputStream + readBytes + base64 that used to live here froze
    // the UI for the length of the read — and the picker has no MIME filter, so
    // that could be a 10 MB PDF.
    val pickFile = rememberLauncherForActivityResult(
        contract = ActivityResultContracts.GetContent(),
    ) { uri: Uri? ->
        val target = replacingDocumentId
        replacingDocumentId = null
        uri ?: return@rememberLauncherForActivityResult
        viewModel.stageFile(uri, replacesDocumentId = target)
    }

    Scaffold(
        topBar = {
            TopAppBar(
                title = {
                    Text(
                        text = stringResource(R.string.my_documents),
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
                    titleContentColor = MaterialTheme.colorScheme.onBackground,
                    navigationIconContentColor = MaterialTheme.colorScheme.onBackground,
                ),
            )
        },
        floatingActionButton = {
            // Reading and compressing takes a beat, and the dialog only opens
            // once it finishes — without a spinner here the file pick would
            // look like it did nothing.
            FloatingActionButton(
                onClick = { if (!preparing) pickFile.launch("*/*") },
                containerColor = MaterialTheme.colorScheme.primary,
            ) {
                if (preparing) {
                    CircularProgressIndicator(
                        modifier = Modifier.size(24.dp),
                        color = MaterialTheme.colorScheme.onPrimary,
                        strokeWidth = 2.dp,
                    )
                } else {
                    Icon(Icons.Outlined.Add, contentDescription = stringResource(R.string.add_document))
                }
            }
        },
        containerColor = MaterialTheme.colorScheme.background,
    ) { paddingValues ->
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(paddingValues),
        ) {
            when {
                uiState is DocumentsSectionUiState.Loading -> {
                    Box(Modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
                        CircularProgressIndicator()
                    }
                }
                else -> {
                    LazyColumn(
                        modifier = Modifier
                            .fillMaxSize()
                            .padding(horizontal = Spacing.M),
                        contentPadding = androidx.compose.foundation.layout.PaddingValues(vertical = Spacing.S),
                    ) {
                        // The checklist leads, uploaded or not. It is the answer to "what do you want
                        // from me" that this screen used to leave to support.
                        if (requirements.isNotEmpty()) {
                            item(key = "requirements") {
                                RequirementsCard(requirements = requirements)
                                Spacer(Modifier.height(Spacing.M))
                            }
                        }

                        if (documents.isEmpty()) {
                            item(key = "empty") { NoDocumentsYet() }
                        }

                        items(documents, key = { it.documentId.orEmpty() }) { doc ->
                            DocumentRow(
                                doc = doc,
                                isBusy = deletingId == doc.documentId,
                                onReplace = {
                                    doc.documentId?.let {
                                        replacingDocumentId = it
                                        pickFile.launch("*/*")
                                    }
                                },
                                onRequestDeletion = { doc.documentId?.let { deletionTarget = it } },
                            )
                        }
                    }
                }
            }
        }
    }

    pendingFile?.let { pending ->
        val replaces = pending.replacesDocumentId
        if (replaces == null) {
            UploadDialog(
                pending = pending,
                isUploading = uploading,
                onDismiss = { viewModel.clearPendingFile() },
                onConfirm = { type, description ->
                    viewModel.upload(
                        documentType = type,
                        fileName = pending.fileName,
                        contentType = pending.contentType,
                        base64Content = pending.base64,
                        description = description,
                    )
                    viewModel.clearPendingFile()
                },
            )
        } else {
            // No type picker: the server carries the type over from the version being replaced, so
            // offering one here would promise a choice the request cannot express.
            ReplaceDialog(
                pending = pending,
                isUploading = uploading,
                onDismiss = { viewModel.clearPendingFile() },
                onConfirm = { description ->
                    viewModel.replace(
                        documentId = replaces,
                        fileName = pending.fileName,
                        contentType = pending.contentType,
                        base64Content = pending.base64,
                        description = description,
                    )
                    viewModel.clearPendingFile()
                },
            )
        }
    }

    deletionTarget?.let { documentId ->
        RequestDeletionDialog(
            isSubmitting = deletingId == documentId,
            onDismiss = { deletionTarget = null },
            onConfirm = { reason ->
                deletionTarget = null
                viewModel.requestDeletion(documentId, reason)
            },
        )
    }
}

/** The state this screen exists for: a country that wants papers, and a cleaner who has none yet. */
@Composable
private fun NoDocumentsYet() {
    Column(
        modifier = Modifier
            .fillMaxWidth()
            .padding(vertical = Spacing.XL),
        horizontalAlignment = Alignment.CenterHorizontally,
    ) {
        Icon(
            imageVector = Icons.Outlined.Description,
            contentDescription = null,
            modifier = Modifier.size(64.dp),
            tint = MaterialTheme.colorScheme.onSurfaceVariant,
        )
        Spacer(Modifier.height(Spacing.S))
        Text(
            text = stringResource(R.string.no_documents),
            style = MaterialTheme.typography.bodyLarge,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
    }
}

/**
 * What the cleaner's country asks for, resolved against what they have uploaded.
 *
 * The server keys this on the WORK country falling back to the address country
 * (GetMyDocumentRequirements), because work country is only set at approval and this screen
 * exists for people who are not approved yet. Saying "work country" here would name a key that
 * is usually not the one in play.
 *
 * Optional rows are listed too — that is the difference between "we would like this" and "you cannot
 * start without this", and both are worth telling somebody.
 */
@Composable
private fun RequirementsCard(requirements: List<MyDocumentRequirementDto>) {
    Surface(
        modifier = Modifier.fillMaxWidth(),
        color = MaterialTheme.colorScheme.surface,
        shape = RoundedCornerShape(16.dp),
        border = BorderStroke(1.dp, MaterialTheme.colorScheme.outlineVariant),
    ) {
        Column(modifier = Modifier.padding(Spacing.M)) {
            Text(
                text = stringResource(R.string.document_requirements_title),
                style = MaterialTheme.typography.titleMedium,
                color = MaterialTheme.colorScheme.onSurface,
                fontWeight = FontWeight.Medium,
            )
            Spacer(Modifier.height(Spacing.XXS))
            Text(
                text = stringResource(R.string.document_requirements_subtitle),
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
            requirements.sortedBy { it.sortOrder ?: 0 }.forEach { requirement ->
                Spacer(Modifier.height(Spacing.S))
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Column(modifier = Modifier.weight(1f)) {
                        Text(
                            text = documentTypeLabel(requirement.documentType),
                            style = MaterialTheme.typography.bodyMedium,
                            color = MaterialTheme.colorScheme.onSurface,
                        )
                        Text(
                            text = stringResource(
                                if (requirement.isRequired == true) {
                                    R.string.document_requirement_required
                                } else {
                                    R.string.document_requirement_optional
                                },
                            ),
                            style = MaterialTheme.typography.bodySmall,
                            color = MaterialTheme.colorScheme.onSurfaceVariant,
                        )
                    }
                    if (requirement.status == null) {
                        Text(
                            text = stringResource(R.string.document_requirement_missing),
                            style = MaterialTheme.typography.bodySmall,
                            color = MaterialTheme.colorScheme.onSurfaceVariant,
                            fontWeight = FontWeight.Medium,
                        )
                    } else {
                        StatusBadge(requirement.status)
                    }
                }
            }
        }
    }
}

/**
 * Confirms a replacement. The message names the file, matching the upload dialog — the thing most
 * worth checking before confirming is that the right file was picked.
 */
@Composable
private fun ReplaceDialog(
    pending: PendingUpload,
    isUploading: Boolean,
    onDismiss: () -> Unit,
    onConfirm: (String?) -> Unit,
) {
    var description by remember { mutableStateOf("") }

    CleansiaDialog(
        onDismiss = onDismiss,
        title = stringResource(R.string.document_replace_title),
        message = stringResource(R.string.document_replace_message, pending.fileName),
        icon = Icons.Outlined.SwapHoriz,
        confirmLabel = stringResource(R.string.document_replace),
        onConfirm = { onConfirm(description) },
        confirmEnabled = !isUploading,
        dismissLabel = stringResource(R.string.cancel),
    ) {
        CleansiaTextField(
            value = description,
            onValueChange = { description = it },
            label = stringResource(R.string.description_optional),
            enabled = !isUploading,
        )
    }
}

/**
 * Confirms a deletion REQUEST. The reason is required by the server and required here — without one
 * an admin is being asked to rule on nothing, which is the whole point of routing this past a person.
 */
@Composable
private fun RequestDeletionDialog(
    isSubmitting: Boolean,
    onDismiss: () -> Unit,
    onConfirm: (String) -> Unit,
) {
    var reason by remember { mutableStateOf("") }

    CleansiaDialog(
        onDismiss = onDismiss,
        title = stringResource(R.string.document_request_deletion_title),
        message = stringResource(R.string.document_request_deletion_message),
        icon = Icons.Outlined.Delete,
        destructive = true,
        confirmLabel = stringResource(R.string.document_request_deletion),
        onConfirm = { onConfirm(reason.trim()) },
        confirmEnabled = reason.isNotBlank() && !isSubmitting,
        dismissLabel = stringResource(R.string.cancel),
    ) {
        CleansiaTextField(
            value = reason,
            onValueChange = { reason = it },
            label = stringResource(R.string.document_deletion_reason),
            enabled = !isSubmitting,
        )
    }
}

@Composable
private fun DocumentRow(
    doc: GetMyDocumentsMyDocumentDto,
    isBusy: Boolean,
    onReplace: () -> Unit,
    onRequestDeletion: () -> Unit,
) {
    // Flat row: matches the dashboard / profile card family. Border
    // does the visual lifting; no shadow or tonal elevation.
    Surface(
        modifier = Modifier
            .fillMaxWidth()
            .padding(vertical = Spacing.XXS),
        color = MaterialTheme.colorScheme.surface,
        shape = RoundedCornerShape(16.dp),
        border = BorderStroke(1.dp, MaterialTheme.colorScheme.outlineVariant),
    ) {
        Row(
            modifier = Modifier.padding(Spacing.M),
            verticalAlignment = Alignment.CenterVertically,
        ) {
            Icon(
                imageVector = Icons.Outlined.Description,
                contentDescription = null,
                tint = MaterialTheme.colorScheme.primary,
                modifier = Modifier.size(28.dp),
            )
            Spacer(Modifier.width(Spacing.M))
            Column(modifier = Modifier.weight(1f)) {
                Text(
                    text = doc.fileName ?: "—",
                    style = MaterialTheme.typography.bodyLarge,
                    color = MaterialTheme.colorScheme.onSurface,
                    fontWeight = FontWeight.Medium,
                )
                Spacer(Modifier.height(2.dp))
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Text(
                        text = documentTypeLabel(doc.documentType),
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                    )
                    Text(
                        text = " · ",
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                    )
                    StatusBadge(doc.status)
                }
            }
            // Replacing is not tinted as destructive on purpose: it needs no admin and costs the
            // cleaner nothing, where asking for removal is the one that hands the decision away.
            IconButton(onClick = onReplace, enabled = !isBusy) {
                Icon(
                    imageVector = Icons.Outlined.SwapHoriz,
                    contentDescription = stringResource(R.string.document_replace),
                    tint = MaterialTheme.colorScheme.primary,
                )
            }
            IconButton(onClick = onRequestDeletion, enabled = !isBusy) {
                if (isBusy) {
                    CircularProgressIndicator(modifier = Modifier.size(20.dp), strokeWidth = 2.dp)
                } else {
                    Icon(
                        imageVector = Icons.Outlined.Delete,
                        contentDescription = stringResource(R.string.document_request_deletion),
                        tint = MaterialTheme.colorScheme.error,
                    )
                }
            }
        }
    }
}

@Composable
private fun StatusBadge(status: DocumentStatus?) {
    val (label, color) = when (status) {
        DocumentStatus._1 -> stringResource(R.string.document_status_pending) to MaterialTheme.colorScheme.tertiary
        DocumentStatus._2 -> stringResource(R.string.document_status_approved) to MaterialTheme.colorScheme.primary
        DocumentStatus._3 -> stringResource(R.string.document_status_rejected) to MaterialTheme.colorScheme.error
        null -> "—" to MaterialTheme.colorScheme.onSurfaceVariant
    }
    Text(
        text = label,
        style = MaterialTheme.typography.bodySmall,
        color = color,
        fontWeight = FontWeight.Medium,
    )
}

/**
 * The document types offered by the upload picker, in the order the API
 * enumerates them.
 *
 * Deliberately spelled out rather than [DocumentType.entries]: the list and
 * [documentTypeLabel] have to stay in lockstep, and `entries` would let a
 * regenerated enum add a type that reaches the picker with no label. The
 * mapper's `when` fails to compile in that case; `DocumentTypeOptionsTest`
 * covers the other direction — a type that exists but is never offered.
 */
internal val documentTypeOptions: List<DocumentType> = listOf(
    DocumentType._1,
    DocumentType._2,
    DocumentType._3,
    DocumentType._4,
    DocumentType._5,
    DocumentType._6,
    DocumentType._7,
    DocumentType._8,
    DocumentType._9,
    DocumentType._10,
)

@Composable
private fun documentTypeLabel(type: DocumentType?): String = when (type) {
    DocumentType._1 -> stringResource(R.string.document_type_identity)
    DocumentType._2 -> stringResource(R.string.document_type_passport)
    DocumentType._3 -> stringResource(R.string.document_type_drivers_license)
    DocumentType._4 -> stringResource(R.string.document_type_work_permit)
    DocumentType._5 -> stringResource(R.string.document_type_contract)
    DocumentType._6 -> stringResource(R.string.document_type_certificate)
    DocumentType._7 -> stringResource(R.string.document_type_bank_statement)
    DocumentType._8 -> stringResource(R.string.document_type_tax)
    DocumentType._9 -> stringResource(R.string.document_type_insurance)
    DocumentType._10 -> stringResource(R.string.document_type_other)
    null -> "—"
}

@Composable
private fun UploadDialog(
    pending: PendingUpload,
    isUploading: Boolean,
    onDismiss: () -> Unit,
    onConfirm: (DocumentType, String?) -> Unit,
) {
    var selectedType by remember { mutableStateOf<DocumentType?>(null) }
    var description by remember { mutableStateOf("") }

    // Same labels the document rows show — not remembered, because
    // documentTypeLabel reads string resources and so must run inside
    // composition. Ten lookups per recomposition of an open dialog is free.
    val typeOptions: List<Pair<DocumentType, String>> =
        documentTypeOptions.map { it to documentTypeLabel(it) }

    CleansiaDialog(
        onDismiss = onDismiss,
        title = stringResource(R.string.upload_document),
        message = pending.fileName,
        confirmLabel = stringResource(R.string.save),
        onConfirm = { selectedType?.let { onConfirm(it, description) } },
        confirmEnabled = selectedType != null && !isUploading,
        dismissLabel = stringResource(R.string.cancel),
    ) {
        Column {
            PickerDropdown(
                selectedId = selectedType?.value?.toString(),
                options = typeOptions.map { (t, label) -> t.value.toString() to label },
                onSelected = { id ->
                    selectedType = DocumentType.values().firstOrNull { it.value.toString() == id }
                },
                label = stringResource(R.string.document_type),
                enabled = !isUploading,
            )
            Spacer(Modifier.height(Spacing.S))
            CleansiaTextField(
                value = description,
                onValueChange = { description = it },
                label = stringResource(R.string.description_optional),
                enabled = !isUploading,
            )
        }
    }
}
