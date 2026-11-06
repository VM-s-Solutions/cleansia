package cz.cleansia.partner.features.orders

import androidx.annotation.StringRes
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.BoxWithConstraints
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.WindowInsets
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.imePadding
import androidx.compose.foundation.layout.navigationBars
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.windowInsetsPadding
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.outlined.Add
import androidx.compose.material.icons.outlined.Delete
import androidx.compose.material.icons.outlined.Edit
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.ModalBottomSheet
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
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
import cz.cleansia.core.ui.components.CleansiaPrimaryButton
import cz.cleansia.core.ui.components.CleansiaTextButton
import cz.cleansia.core.ui.components.CleansiaTextField
import cz.cleansia.core.ui.theme.Spacing
import cz.cleansia.partner.R
import cz.cleansia.partner.api.model.OrderIssueDto
import cz.cleansia.partner.api.model.OrderNoteDto
import cz.cleansia.core.format.formatOrderTime
import androidx.compose.material.icons.outlined.Flag
import androidx.compose.ui.text.AnnotatedString
import androidx.compose.ui.text.SpanStyle

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

    NotesAndIssuesBody(
        notes = notes,
        issues = issues,
        currentEmployeeId = currentEmployeeId,
        mutatingId = uiState.mutatingId,
        editable = !isReadOnly,
        canAdd = !isReadOnly && canAddNotes,
        addEnabled = !uiState.isSavingNote && !uiState.isReportingIssue,
        onAddNote = { noteSheetOpen = true },
        onAddIssue = { issueSheetOpen = true },
        onEditNote = { editingNote = it },
        onDeleteNote = { deletingNote = it },
        onEditIssue = { editingIssue = it },
        onDeleteIssue = { deletingIssue = it },
    )

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
            onDismiss = { deletingIssue = null },
            onConfirm = {
                issue.id?.let(viewModel::deleteIssue)
                deletingIssue = null
            },
        )
    }
}

/** What she may add to the work record, in the order the buttons stand: Note in the app's tint, Issue in the error tone. */
internal enum class RecordKind(@StringRes val label: Int, val tone: ChipTone) {
    Note(R.string.add_note, ChipTone.Primary),
    Issue(R.string.report_issue, ChipTone.Error),
}

/** Note and Issue in the photos' two columns, each filling its column; one column where the photos stack. */
@Composable
private fun PairColumns(modifier: Modifier = Modifier, cell: @Composable (RecordKind, Modifier) -> Unit) {
    BoxWithConstraints(modifier.fillMaxWidth()) {
        if (maxWidth >= PhotoColumnsMinWidth) {
            Row(horizontalArrangement = Arrangement.spacedBy(PhotoColumnGap)) {
                RecordKind.entries.forEach { cell(it, Modifier.weight(1f)) }
            }
        } else {
            Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                RecordKind.entries.forEach { cell(it, Modifier.fillMaxWidth()) }
            }
        }
    }
}

/**
 * *Notes & issues*: the title with *Note* and *Issue* while she may add, then each record, *You · 14:40* or
 * *Issue · 14:52 · not resolved*, over its text. Her own records keep their edit and delete.
 */
@Composable
internal fun NotesAndIssuesBody(
    notes: List<OrderNoteDto>,
    issues: List<OrderIssueDto>,
    currentEmployeeId: String?,
    mutatingId: String?,
    editable: Boolean,
    canAdd: Boolean,
    addEnabled: Boolean,
    onAddNote: () -> Unit,
    onAddIssue: () -> Unit,
    onEditNote: (OrderNoteDto) -> Unit,
    onDeleteNote: (OrderNoteDto) -> Unit,
    onEditIssue: (OrderIssueDto) -> Unit,
    onDeleteIssue: (OrderIssueDto) -> Unit,
    modifier: Modifier = Modifier,
) {
    Column(modifier.fillMaxWidth()) {
        PartHeading(stringResource(R.string.notes_and_issues))
        if (canAdd) {
            PairColumns(Modifier.padding(top = 8.dp)) { kind, slot ->
                TonalChip(
                    label = stringResource(kind.label),
                    icon = if (kind == RecordKind.Note) Icons.Outlined.Edit else Icons.Outlined.Flag,
                    onClick = if (kind == RecordKind.Note) onAddNote else onAddIssue,
                    enabled = addEnabled,
                    tone = kind.tone,
                    modifier = slot,
                )
            }
        }
        val you = stringResource(R.string.crew_you)
        notes.forEach { note ->
            val mine = note.employeeId != null && note.employeeId == currentEmployeeId
            Record(
                head = listOfNotNull(you.takeIf { mine }?.let { AnnotatedString(it) }, note.createdOn?.let { AnnotatedString(formatOrderTime(it)) }),
                text = note.content.orEmpty(),
                actions = mine && editable,
                isMutating = mutatingId == note.id,
                onEdit = { onEditNote(note) },
                onDelete = { onDeleteNote(note) },
            )
        }
        val issueWord = stringResource(R.string.report_issue)
        val open = stringResource(R.string.issue_not_resolved)
        issues.forEach { issue ->
            val mine = issue.reportedByEmployeeId != null && issue.reportedByEmployeeId == currentEmployeeId
            Record(
                head = listOfNotNull(
                    styled(issueWord, SpanStyle(color = MaterialTheme.colorScheme.error)),
                    issue.createdOn?.let { AnnotatedString(formatOrderTime(it)) },
                    AnnotatedString(open).takeIf { issue.isResolved != true },
                ),
                text = issue.description.orEmpty(),
                actions = mine && editable,
                isMutating = mutatingId == issue.id,
                onEdit = { onEditIssue(issue) },
                onDelete = { onDeleteIssue(issue) },
            )
        }
    }
}

@Composable
private fun Record(
    head: List<AnnotatedString>,
    text: String,
    actions: Boolean,
    isMutating: Boolean,
    onEdit: () -> Unit,
    onDelete: () -> Unit,
) {
    Row(Modifier.fillMaxWidth().padding(top = 10.dp, bottom = 2.dp), verticalAlignment = Alignment.Top) {
        Column(Modifier.weight(1f)) {
            DotFlow(items = head, style = MaterialTheme.typography.bodySmall.copy(fontFeatureSettings = "tnum"), color = MaterialTheme.colorScheme.onSurfaceVariant)
            Text(text = OrdersBoardRules.tie(text), style = MaterialTheme.typography.bodyMedium, color = MaterialTheme.colorScheme.onSurface, modifier = Modifier.padding(top = 2.dp))
        }
        if (actions) {
            if (isMutating) {
                CircularProgressIndicator(modifier = Modifier.padding(8.dp).size(20.dp), strokeWidth = 2.dp)
            } else {
                IconButton(onClick = onEdit, modifier = Modifier.size(40.dp)) {
                    Icon(Icons.Outlined.Edit, contentDescription = stringResource(R.string.edit_note), tint = MaterialTheme.colorScheme.onSurfaceVariant, modifier = Modifier.size(18.dp))
                }
                IconButton(onClick = onDelete, modifier = Modifier.size(40.dp)) {
                    Icon(Icons.Outlined.Delete, contentDescription = stringResource(R.string.delete_note), tint = MaterialTheme.colorScheme.error, modifier = Modifier.size(18.dp))
                }
            }
        }
    }
}

/**
 * The system confirm closes on the tap; the row it was raised from shows the deletion running, and the
 * row's own actions stay hidden until it ends.
 */
@Composable
private fun ConfirmDeleteDialog(
    message: String,
    onDismiss: () -> Unit,
    onConfirm: () -> Unit,
) {
    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text(stringResource(R.string.delete)) },
        text = { Text(message) },
        confirmButton = {
            TextButton(
                onClick = onConfirm,
                colors = ButtonDefaults.textButtonColors(contentColor = MaterialTheme.colorScheme.error),
            ) { Text(stringResource(R.string.delete)) }
        },
        dismissButton = {
            CleansiaTextButton(onClick = onDismiss) { Text(stringResource(R.string.cancel)) }
        },
    )
}

internal enum class SheetAccent { Brand, Danger }

@OptIn(ExperimentalMaterial3Api::class)
@Composable
internal fun TextEntryBottomSheet(
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

/** Brand-consistent issue/danger red. */
private val IssueRed = Color(0xFFDC2626)
