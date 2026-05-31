package cz.cleansia.partner.features.profile.screens

import android.net.Uri
import android.util.Base64
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
import androidx.compose.material3.SnackbarHost
import androidx.compose.material3.SnackbarHostState
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.TopAppBar
import androidx.compose.material3.TopAppBarDefaults
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import cz.cleansia.core.ui.components.CleansiaDialog
import cz.cleansia.core.ui.components.CleansiaTextField
import cz.cleansia.core.ui.theme.Spacing
import cz.cleansia.partner.R
import cz.cleansia.partner.api.model.DocumentStatus
import cz.cleansia.partner.api.model.DocumentType
import cz.cleansia.partner.api.model.GetMyDocumentsMyDocumentDto
import cz.cleansia.partner.features.profile.components.PickerDropdown
import cz.cleansia.partner.features.profile.viewmodels.DocumentsSectionViewModel

/**
 * My-documents screen — list of uploaded documents (filename, type, status
 * pill, delete button) + FAB to add. Tapping FAB opens a system file picker;
 * once a file is selected the user picks the document type and an optional
 * description in a dialog before upload.
 */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun DocumentsSectionScreen(
    onNavigateBack: () -> Unit,
    viewModel: DocumentsSectionViewModel = hiltViewModel(),
) {
    val uiState by viewModel.uiState.collectAsState()
    val context = LocalContext.current
    val snackbarHostState = remember { SnackbarHostState() }

    // Pending pick — once the user picks a file, we hold its Uri here and
    // open the metadata dialog. Null again after upload starts or dialog
    // is cancelled.
    var pendingFile by remember { mutableStateOf<PendingUpload?>(null) }

    val pickFile = rememberLauncherForActivityResult(
        contract = ActivityResultContracts.GetContent(),
    ) { uri: Uri? ->
        uri ?: return@rememberLauncherForActivityResult
        val resolver = context.contentResolver
        val name = uri.lastPathSegment?.substringAfterLast('/') ?: "document"
        val contentType = resolver.getType(uri) ?: "application/octet-stream"
        val bytes = runCatching {
            resolver.openInputStream(uri)?.use { it.readBytes() }
        }.getOrNull() ?: return@rememberLauncherForActivityResult
        val base64 = Base64.encodeToString(bytes, Base64.NO_WRAP)
        pendingFile = PendingUpload(fileName = name, contentType = contentType, base64 = base64)
    }

    LaunchedEffect(uiState.error) {
        uiState.error?.let { err ->
            snackbarHostState.showSnackbar(err)
            viewModel.clearError()
        }
    }
    LaunchedEffect(uiState.uploadSuccess) {
        if (uiState.uploadSuccess) viewModel.clearUploadSuccess()
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
        snackbarHost = { SnackbarHost(hostState = snackbarHostState) },
        floatingActionButton = {
            FloatingActionButton(
                onClick = { pickFile.launch("*/*") },
                containerColor = MaterialTheme.colorScheme.primary,
            ) {
                Icon(Icons.Outlined.Add, contentDescription = stringResource(R.string.add_document))
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
                uiState.isLoading && uiState.documents.isEmpty() -> {
                    Box(Modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
                        CircularProgressIndicator()
                    }
                }
                uiState.documents.isEmpty() -> {
                    Column(
                        modifier = Modifier
                            .fillMaxSize()
                            .padding(Spacing.M),
                        horizontalAlignment = Alignment.CenterHorizontally,
                        verticalArrangement = Arrangement.Center,
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
                else -> {
                    LazyColumn(
                        modifier = Modifier
                            .fillMaxSize()
                            .padding(horizontal = Spacing.M),
                        contentPadding = androidx.compose.foundation.layout.PaddingValues(vertical = Spacing.S),
                    ) {
                        items(uiState.documents, key = { it.documentId.orEmpty() }) { doc ->
                            DocumentRow(
                                doc = doc,
                                isDeleting = uiState.deletingId == doc.documentId,
                                onDelete = { doc.documentId?.let { viewModel.delete(it) } },
                            )
                        }
                    }
                }
            }
        }
    }

    pendingFile?.let { pending ->
        UploadDialog(
            pending = pending,
            isUploading = uiState.isUploading,
            onDismiss = { pendingFile = null },
            onConfirm = { type, description ->
                viewModel.upload(
                    documentType = type,
                    fileName = pending.fileName,
                    contentType = pending.contentType,
                    base64Content = pending.base64,
                    description = description,
                )
                pendingFile = null
            },
        )
    }
}

private data class PendingUpload(
    val fileName: String,
    val contentType: String,
    val base64: String,
)

@Composable
private fun DocumentRow(
    doc: GetMyDocumentsMyDocumentDto,
    isDeleting: Boolean,
    onDelete: () -> Unit,
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
            IconButton(onClick = onDelete, enabled = !isDeleting) {
                if (isDeleting) {
                    CircularProgressIndicator(modifier = Modifier.size(20.dp), strokeWidth = 2.dp)
                } else {
                    Icon(
                        imageVector = Icons.Outlined.Delete,
                        contentDescription = stringResource(R.string.delete),
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

    val typeOptions = remember {
        listOf(
            DocumentType._1 to "Identity card",
            DocumentType._2 to "Passport",
            DocumentType._3 to "Driver's license",
            DocumentType._4 to "Work permit",
            DocumentType._5 to "Contract",
            DocumentType._6 to "Certificate",
            DocumentType._7 to "Bank statement",
            DocumentType._8 to "Tax document",
            DocumentType._9 to "Insurance document",
            DocumentType._10 to "Other",
        )
    }

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
