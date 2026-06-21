package cz.cleansia.partner.features.orders

import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.WindowInsets
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.imePadding
import androidx.compose.foundation.layout.navigationBars
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.layout.windowInsetsPadding
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.Add
import androidx.compose.material.icons.outlined.Delete
import androidx.compose.material.icons.outlined.DeleteOutline
import androidx.compose.material.icons.outlined.Edit
import androidx.compose.material.icons.outlined.ReportProblem
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.ModalBottomSheet
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.rememberModalBottomSheetState
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import cz.cleansia.core.ui.components.CleansiaDialog
import cz.cleansia.core.ui.components.CleansiaOutlinedButton
import cz.cleansia.core.ui.components.CleansiaPrimaryButton
import cz.cleansia.core.ui.components.CleansiaTextField
import cz.cleansia.core.ui.theme.Spacing
import cz.cleansia.partner.R
import cz.cleansia.partner.api.model.OrderIssueDto
import cz.cleansia.partner.api.model.OrderNoteDto

/**
 * Notes + issues block on Order Details.
 *
 * Lists the existing notes and issues (from the parent order payload),
 * with per-row edit and delete affordances visible only to the row's
 * author. Add Note / Report Issue actions live at the bottom of the
 * block — hidden when [isReadOnly] is true (e.g. terminal orders,
 * historical view).
 *
 * On every mutation the VM bumps `mutationVersion`; we forward that to
 * [onMutated] so the parent screen can refresh and pick up the new
 * notes/issues list.
 *
 * Success and error feedback flows through the app-wide
 * [SnackbarController] (pushed directly from the VM), not a native
 * Material snackbar — matches the rest of the app.
 */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun NotesAndIssuesSection(
    notes: List<OrderNoteDto>,
    issues: List<OrderIssueDto>,
    onMutated: () -> Unit,
    isReadOnly: Boolean = false,
    canAddNotes: Boolean = false,
    viewModel: OrderNotesViewModel = hiltViewModel(),
) {
    val uiState by viewModel.uiState.collectAsStateWithLifecycle()
    val currentEmployeeId by viewModel.currentEmployeeId.collectAsStateWithLifecycle()
    var noteSheetOpen by remember { mutableStateOf(false) }
    var issueSheetOpen by remember { mutableStateOf(false) }
    var editingNote by remember { mutableStateOf<OrderNoteDto?>(null) }
    var editingIssue by remember { mutableStateOf<OrderIssueDto?>(null) }
    var deletingNote by remember { mutableStateOf<OrderNoteDto?>(null) }
    var deletingIssue by remember { mutableStateOf<OrderIssueDto?>(null) }

    LaunchedEffect(uiState.noteSaved) {
        if (uiState.noteSaved) {
            noteSheetOpen = false
            editingNote = null
            viewModel.clearNoteSaved()
        }
    }
    LaunchedEffect(uiState.issueReported) {
        if (uiState.issueReported) {
            issueSheetOpen = false
            editingIssue = null
            viewModel.clearIssueReported()
        }
    }
    // Bubble mutations up so the parent refetches the order and the
    // notes/issues list re-renders with the new content.
    LaunchedEffect(uiState.mutationVersion) {
        if (uiState.mutationVersion > 0) onMutated()
    }

    // Hide the whole section when there's nothing to show AND nothing
    // to add — keeps orders without add capability and no historical
    // notes from rendering an empty card. Applies both to terminal
    // orders (isReadOnly) and to pre-OnTheWay statuses where adds are
    // gated off (canAddNotes=false).
    if (!canAddNotes && notes.isEmpty() && issues.isEmpty()) return

    Surface(
        modifier = Modifier.fillMaxWidth(),
        color = MaterialTheme.colorScheme.surface,
        shape = MaterialTheme.shapes.medium,
        tonalElevation = 1.dp,
    ) {
        Column(modifier = Modifier.padding(Spacing.M)) {
            Text(
                text = stringResource(R.string.notes_and_issues),
                style = MaterialTheme.typography.titleSmall,
                color = MaterialTheme.colorScheme.onSurface,
                fontWeight = FontWeight.SemiBold,
            )
            HorizontalDivider(
                modifier = Modifier.padding(vertical = Spacing.XS),
                color = MaterialTheme.colorScheme.outlineVariant,
            )

            if (notes.isNotEmpty() || issues.isNotEmpty()) {
                Column(
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(top = Spacing.XS),
                    verticalArrangement = Arrangement.spacedBy(Spacing.S),
                ) {
                    notes.forEach { note ->
                        NoteRow(
                            note = note,
                            isMine = note.employeeId != null && note.employeeId == currentEmployeeId,
                            isMutating = uiState.mutatingId == note.id,
                            editable = !isReadOnly,
                            onEdit = { editingNote = note },
                            onDelete = { deletingNote = note },
                        )
                    }
                    issues.forEach { issue ->
                        IssueRow(
                            issue = issue,
                            isMine = issue.reportedByEmployeeId != null && issue.reportedByEmployeeId == currentEmployeeId,
                            isMutating = uiState.mutatingId == issue.id,
                            editable = !isReadOnly,
                            onEdit = { editingIssue = issue },
                            onDelete = { deletingIssue = issue },
                        )
                    }
                }
            }

            if (!isReadOnly && canAddNotes) {
                Spacer(Modifier.height(Spacing.S))
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.spacedBy(Spacing.XS),
                ) {
                    CleansiaOutlinedButton(
                        text = stringResource(R.string.add_note),
                        onClick = { noteSheetOpen = true },
                        leadingIcon = Icons.Outlined.Add,
                        enabled = !uiState.isSavingNote && !uiState.isReportingIssue,
                        modifier = Modifier.weight(1f),
                    )
                    IssueButton(
                        onClick = { issueSheetOpen = true },
                        enabled = !uiState.isSavingNote && !uiState.isReportingIssue,
                        modifier = Modifier.weight(1f),
                    )
                }
            }
        }
    }

    if (noteSheetOpen) {
        TextEntryBottomSheet(
            title = stringResource(R.string.add_note),
            description = stringResource(R.string.add_note_desc),
            label = stringResource(R.string.note_content),
            initialText = "",
            isSaving = uiState.isSavingNote,
            accent = SheetAccent.Brand,
            onDismiss = { if (!uiState.isSavingNote) noteSheetOpen = false },
            onConfirm = viewModel::addNote,
        )
    }
    if (issueSheetOpen) {
        TextEntryBottomSheet(
            title = stringResource(R.string.report_issue),
            description = stringResource(R.string.report_issue_desc),
            label = stringResource(R.string.issue_description),
            initialText = "",
            isSaving = uiState.isReportingIssue,
            accent = SheetAccent.Danger,
            onDismiss = { if (!uiState.isReportingIssue) issueSheetOpen = false },
            onConfirm = viewModel::reportIssue,
        )
    }

    editingNote?.let { note ->
        val noteId = note.id
        if (noteId != null) {
            TextEntryBottomSheet(
                title = stringResource(R.string.edit_note),
                description = stringResource(R.string.add_note_desc),
                label = stringResource(R.string.note_content),
                initialText = note.content.orEmpty(),
                isSaving = uiState.mutatingId == noteId,
                accent = SheetAccent.Brand,
                onDismiss = {
                    if (uiState.mutatingId != noteId) editingNote = null
                },
                onConfirm = { viewModel.updateNote(noteId, it) },
            )
        }
    }
    editingIssue?.let { issue ->
        val issueId = issue.id
        if (issueId != null) {
            TextEntryBottomSheet(
                title = stringResource(R.string.edit_issue),
                description = stringResource(R.string.report_issue_desc),
                label = stringResource(R.string.issue_description),
                initialText = issue.description.orEmpty(),
                isSaving = uiState.mutatingId == issueId,
                accent = SheetAccent.Danger,
                onDismiss = {
                    if (uiState.mutatingId != issueId) editingIssue = null
                },
                onConfirm = { viewModel.updateIssue(issueId, it) },
            )
        }
    }

    deletingNote?.let { note ->
        ConfirmDeleteDialog(
            message = stringResource(R.string.delete_note_confirm),
            isWorking = uiState.mutatingId == note.id,
            onDismiss = { deletingNote = null },
            onConfirm = {
                note.id?.let(viewModel::deleteNote)
                deletingNote = null
            },
        )
    }
    deletingIssue?.let { issue ->
        ConfirmDeleteDialog(
            message = stringResource(R.string.delete_issue_confirm),
            isWorking = uiState.mutatingId == issue.id,
            onDismiss = { deletingIssue = null },
            onConfirm = {
                issue.id?.let(viewModel::deleteIssue)
                deletingIssue = null
            },
        )
    }
}

@Composable
private fun NoteRow(
    note: OrderNoteDto,
    isMine: Boolean,
    isMutating: Boolean,
    editable: Boolean,
    onEdit: () -> Unit,
    onDelete: () -> Unit,
) {
    EntryRow(
        text = note.content.orEmpty(),
        accent = MaterialTheme.colorScheme.primary,
        showActions = isMine && editable,
        isMutating = isMutating,
        onEdit = onEdit,
        onDelete = onDelete,
    )
}

@Composable
private fun IssueRow(
    issue: OrderIssueDto,
    isMine: Boolean,
    isMutating: Boolean,
    editable: Boolean,
    onEdit: () -> Unit,
    onDelete: () -> Unit,
) {
    EntryRow(
        text = issue.description.orEmpty(),
        accent = IssueRed,
        showActions = isMine && editable,
        isMutating = isMutating,
        onEdit = onEdit,
        onDelete = onDelete,
    )
}

/**
 * Shared row chrome for notes and issues — left accent bar (brand for
 * notes, red for issues) + body text + right-aligned edit/delete on
 * the author's own entries. Resolved-issue + read-only paths just hide
 * the right-side actions.
 */
@Composable
private fun EntryRow(
    text: String,
    accent: Color,
    showActions: Boolean,
    isMutating: Boolean,
    onEdit: () -> Unit,
    onDelete: () -> Unit,
) {
    Surface(
        modifier = Modifier.fillMaxWidth(),
        color = MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.4f),
        shape = RoundedCornerShape(10.dp),
        tonalElevation = 0.dp,
    ) {
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .padding(start = 4.dp, end = Spacing.XS, top = Spacing.XS, bottom = Spacing.XS),
            verticalAlignment = Alignment.CenterVertically,
        ) {
            // Thin accent bar — gives the row a clear category tint
            // without spending color on a full chip background.
            Box(
                modifier = Modifier
                    .width(3.dp)
                    .height(28.dp)
                    .padding(end = 0.dp),
            ) {
                Surface(
                    modifier = Modifier.fillMaxWidth().height(28.dp),
                    color = accent,
                    shape = RoundedCornerShape(2.dp),
                    content = {},
                )
            }
            Text(
                text = text,
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurface,
                modifier = Modifier
                    .weight(1f)
                    .padding(horizontal = Spacing.S),
            )
            if (showActions) {
                if (isMutating) {
                    CircularProgressIndicator(
                        modifier = Modifier.size(20.dp),
                        strokeWidth = 2.dp,
                    )
                } else {
                    IconButton(onClick = onEdit, modifier = Modifier.size(32.dp)) {
                        Icon(
                            imageVector = Icons.Outlined.Edit,
                            contentDescription = stringResource(R.string.edit_note),
                            tint = MaterialTheme.colorScheme.onSurfaceVariant,
                            modifier = Modifier.size(18.dp),
                        )
                    }
                    IconButton(onClick = onDelete, modifier = Modifier.size(32.dp)) {
                        Icon(
                            imageVector = Icons.Outlined.Delete,
                            contentDescription = stringResource(R.string.delete_note),
                            tint = IssueRed,
                            modifier = Modifier.size(18.dp),
                        )
                    }
                }
            }
        }
    }
}

@Composable
private fun ConfirmDeleteDialog(
    message: String,
    isWorking: Boolean,
    onDismiss: () -> Unit,
    onConfirm: () -> Unit,
) {
    // Custom CleansiaDialog — same shape, halo, and choreography as
    // the profile logout confirm in the customer app, so destructive
    // confirms read consistently across the platform instead of
    // dropping the user into a stock Material AlertDialog.
    CleansiaDialog(
        onDismiss = { if (!isWorking) onDismiss() },
        title = stringResource(R.string.delete),
        message = message,
        icon = Icons.Outlined.DeleteOutline,
        destructive = true,
        confirmLabel = stringResource(R.string.delete),
        confirmEnabled = !isWorking,
        onConfirm = onConfirm,
        dismissLabel = stringResource(R.string.cancel),
    )
}

/**
 * Red-tinted Issue button. Matches PaymentCard's Failed pill so the
 * platform has one consistent "needs attention" hue.
 */
@Composable
private fun IssueButton(
    onClick: () -> Unit,
    enabled: Boolean,
    modifier: Modifier = Modifier,
) {
    OutlinedButton(
        onClick = onClick,
        modifier = modifier
            .fillMaxWidth()
            .height(48.dp),
        enabled = enabled,
        shape = CircleShape,
        border = BorderStroke(1.5.dp, if (enabled) IssueRed else IssueRed.copy(alpha = 0.4f)),
        contentPadding = PaddingValues(horizontal = 16.dp),
    ) {
        Icon(
            imageVector = Icons.Outlined.ReportProblem,
            contentDescription = null,
            modifier = Modifier.size(20.dp),
            tint = if (enabled) IssueRed else IssueRed.copy(alpha = 0.4f),
        )
        Spacer(Modifier.width(8.dp))
        Text(
            text = stringResource(R.string.report_issue),
            style = MaterialTheme.typography.labelLarge.copy(fontWeight = FontWeight.SemiBold),
            color = if (enabled) IssueRed else IssueRed.copy(alpha = 0.4f),
        )
    }
}

private enum class SheetAccent { Brand, Danger }

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun TextEntryBottomSheet(
    title: String,
    description: String,
    label: String,
    initialText: String,
    isSaving: Boolean,
    accent: SheetAccent,
    onDismiss: () -> Unit,
    onConfirm: (String) -> Unit,
) {
    // remember(initialText) keys the buffer to the source so reopening
    // the sheet for a different note doesn't carry over the previous
    // edit. Plain `mutableStateOf` would persist forever otherwise.
    var text by remember(initialText) { mutableStateOf(initialText) }
    val sheetState = rememberModalBottomSheetState(skipPartiallyExpanded = true)
    val titleColor = when (accent) {
        SheetAccent.Brand -> MaterialTheme.colorScheme.onSurface
        SheetAccent.Danger -> IssueRed
    }

    ModalBottomSheet(
        onDismissRequest = onDismiss,
        sheetState = sheetState,
        containerColor = MaterialTheme.colorScheme.surface,
        modifier = Modifier
            .imePadding()
            .windowInsetsPadding(WindowInsets.navigationBars),
    ) {
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .padding(horizontal = Spacing.L, vertical = Spacing.M),
            verticalArrangement = Arrangement.spacedBy(Spacing.M),
            horizontalAlignment = Alignment.CenterHorizontally,
        ) {
            Text(
                text = title,
                style = MaterialTheme.typography.headlineSmall.copy(fontWeight = FontWeight.Bold),
                color = titleColor,
                textAlign = TextAlign.Center,
                modifier = Modifier.fillMaxWidth(),
            )
            Text(
                text = description,
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
                textAlign = TextAlign.Center,
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(bottom = Spacing.XS),
            )
            CleansiaTextField(
                value = text,
                onValueChange = { text = it },
                label = label,
                enabled = !isSaving,
                singleLine = false,
                modifier = Modifier.heightIn(min = 140.dp),
            )
            CleansiaPrimaryButton(
                text = stringResource(R.string.save),
                onClick = { onConfirm(text) },
                loading = isSaving,
                enabled = text.isNotBlank() && !isSaving,
            )
        }
    }
}

/** Brand-consistent issue/danger red. Matches PaymentCard's Failed pill. */
private val IssueRed = Color(0xFFDC2626)
