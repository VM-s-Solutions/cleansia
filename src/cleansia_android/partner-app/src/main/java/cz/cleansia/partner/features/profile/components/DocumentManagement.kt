package cz.cleansia.partner.features.profile.components

import android.net.Uri
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Add
import androidx.compose.material.icons.filled.Description
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.DropdownMenuItem
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.ExposedDropdownMenuBox
import androidx.compose.material3.ExposedDropdownMenuDefaults
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.MenuAnchorType
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.TextField
import androidx.compose.material3.TextFieldDefaults
import androidx.compose.ui.graphics.Color
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberUpdatedState
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import cz.cleansia.partner.R
import cz.cleansia.partner.domain.models.profile.DocumentStatus
import cz.cleansia.partner.domain.models.profile.DocumentType
import cz.cleansia.partner.domain.models.profile.EmployeeDocument
import cz.cleansia.partner.features.profile.components.documents.DocumentGroup
import cz.cleansia.partner.features.profile.components.documents.SmallButton
import cz.cleansia.partner.features.profile.components.documents.getDocumentTypeName

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun DocumentManagementSection(
    documents: List<EmployeeDocument>,
    isUploading: Boolean,
    isDeleting: Boolean,
    onUploadDocument: (ByteArray, String, DocumentType) -> Unit,
    onDeleteDocument: (String) -> Unit,
    onDownloadDocument: ((String, String) -> Unit)? = null,
    modifier: Modifier = Modifier
) {
    val context = LocalContext.current
    var documentToDelete by remember { mutableStateOf<EmployeeDocument?>(null) }
    var selectedDocumentType by remember { mutableStateOf(DocumentType.OTHER) }
    var showDocumentTypeDropdown by remember { mutableStateOf(false) }

    // Use rememberUpdatedState so the file picker callback always reads the latest selected type
    val currentDocumentType by rememberUpdatedState(selectedDocumentType)
    val currentOnUpload by rememberUpdatedState(onUploadDocument)

    val filePickerLauncher = rememberLauncherForActivityResult(
        contract = ActivityResultContracts.GetContent()
    ) { uri: Uri? ->
        uri?.let {
            context.contentResolver.openInputStream(it)?.use { inputStream ->
                val bytes = inputStream.readBytes()
                val fileName = it.lastPathSegment ?: "document.pdf"
                currentOnUpload(bytes, fileName, currentDocumentType)
            }
        }
    }

    // Delete confirmation dialog
    documentToDelete?.let { doc ->
        AlertDialog(
            onDismissRequest = { documentToDelete = null },
            title = { Text(stringResource(R.string.delete)) },
            text = { Text(stringResource(R.string.delete_document_confirm, doc.fileName ?: "document")) },
            confirmButton = {
                TextButton(
                    onClick = {
                        onDeleteDocument(doc.id)
                        documentToDelete = null
                    }
                ) {
                    Text(stringResource(R.string.delete), color = MaterialTheme.colorScheme.error)
                }
            },
            dismissButton = {
                TextButton(onClick = { documentToDelete = null }) {
                    Text(stringResource(R.string.cancel))
                }
            }
        )
    }

    Card(
        modifier = modifier.fillMaxWidth(),
        colors = CardDefaults.cardColors(
            containerColor = MaterialTheme.colorScheme.surface
        ),
        elevation = CardDefaults.cardElevation(defaultElevation = 1.dp),
        shape = RoundedCornerShape(16.dp)
    ) {
        Column(
            modifier = Modifier.padding(16.dp),
            verticalArrangement = Arrangement.spacedBy(12.dp)
        ) {
            // Header
            Row(
                modifier = Modifier.fillMaxWidth(),
                verticalAlignment = Alignment.CenterVertically
            ) {
                Box(
                    modifier = Modifier
                        .size(32.dp)
                        .clip(RoundedCornerShape(8.dp))
                        .background(MaterialTheme.colorScheme.primaryContainer.copy(alpha = 0.5f)),
                    contentAlignment = Alignment.Center
                ) {
                    Icon(
                        imageVector = Icons.Default.Description,
                        contentDescription = null,
                        tint = MaterialTheme.colorScheme.primary,
                        modifier = Modifier.size(18.dp)
                    )
                }
                Spacer(modifier = Modifier.width(10.dp))
                Text(
                    text = stringResource(R.string.documents),
                    style = MaterialTheme.typography.titleMedium,
                    fontWeight = FontWeight.SemiBold,
                    color = MaterialTheme.colorScheme.onSurface,
                    modifier = Modifier.weight(1f)
                )
            }

            // Upload section: document type selector + upload button
            Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                // Document type dropdown
                ExposedDropdownMenuBox(
                    expanded = showDocumentTypeDropdown,
                    onExpandedChange = { showDocumentTypeDropdown = it }
                ) {
                    TextField(
                        value = getDocumentTypeName(selectedDocumentType),
                        onValueChange = {},
                        readOnly = true,
                        label = { Text(stringResource(R.string.document_type_label)) },
                        trailingIcon = {
                            ExposedDropdownMenuDefaults.TrailingIcon(expanded = showDocumentTypeDropdown)
                        },
                        modifier = Modifier
                            .fillMaxWidth()
                            .menuAnchor(MenuAnchorType.PrimaryNotEditable),
                        singleLine = true,
                        shape = RoundedCornerShape(12.dp),
                        colors = TextFieldDefaults.colors(
                            focusedContainerColor = MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.5f),
                            unfocusedContainerColor = MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.3f),
                            disabledContainerColor = MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.3f),
                            focusedIndicatorColor = Color.Transparent,
                            unfocusedIndicatorColor = Color.Transparent,
                            disabledIndicatorColor = Color.Transparent,
                            errorIndicatorColor = Color.Transparent
                        )
                    )
                    ExposedDropdownMenu(
                        expanded = showDocumentTypeDropdown,
                        onDismissRequest = { showDocumentTypeDropdown = false }
                    ) {
                        DocumentType.entries.forEach { type ->
                            DropdownMenuItem(
                                text = { Text(getDocumentTypeName(type)) },
                                onClick = {
                                    selectedDocumentType = type
                                    showDocumentTypeDropdown = false
                                }
                            )
                        }
                    }
                }

                // Upload button
                SmallButton(
                    text = stringResource(R.string.select_files),
                    onClick = { filePickerLauncher.launch("*/*") },
                    icon = Icons.Default.Add,
                    isLoading = isUploading
                )

                Text(
                    text = stringResource(R.string.upload_requirements),
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant.copy(alpha = 0.7f)
                )
            }

            // Documents grouped by status
            if (documents.isEmpty()) {
                Text(
                    text = stringResource(R.string.no_documents),
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )
            } else {
                val pendingDocs = documents.filter { it.documentStatus == DocumentStatus.PENDING }
                val approvedDocs = documents.filter { it.documentStatus == DocumentStatus.APPROVED }
                val rejectedDocs = documents.filter { it.documentStatus == DocumentStatus.REJECTED }
                val expiredDocs = documents.filter { it.documentStatus == DocumentStatus.EXPIRED }

                if (pendingDocs.isNotEmpty()) {
                    DocumentGroup(
                        title = stringResource(R.string.pending_documents),
                        documents = pendingDocs,
                        isDeleting = isDeleting,
                        onDelete = { documentToDelete = it },
                        onDownload = onDownloadDocument
                    )
                }

                if (approvedDocs.isNotEmpty()) {
                    DocumentGroup(
                        title = stringResource(R.string.approved_documents),
                        documents = approvedDocs,
                        isDeleting = isDeleting,
                        onDelete = { documentToDelete = it },
                        onDownload = onDownloadDocument
                    )
                }

                if (rejectedDocs.isNotEmpty()) {
                    DocumentGroup(
                        title = stringResource(R.string.rejected_documents),
                        documents = rejectedDocs,
                        isDeleting = isDeleting,
                        onDelete = { documentToDelete = it },
                        onDownload = onDownloadDocument
                    )
                }

                if (expiredDocs.isNotEmpty()) {
                    DocumentGroup(
                        title = stringResource(R.string.expired_documents),
                        documents = expiredDocs,
                        isDeleting = isDeleting,
                        onDelete = { documentToDelete = it },
                        onDownload = onDownloadDocument
                    )
                }
            }
        }
    }
}
