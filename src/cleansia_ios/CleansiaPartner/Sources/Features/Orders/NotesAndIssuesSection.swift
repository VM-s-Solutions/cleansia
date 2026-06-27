import CleansiaCore
import CleansiaPartnerApi
import SwiftUI

/// Notes + issues block. Lists existing notes/issues (read from the parent
/// order payload), with per-row edit/delete shown only to the author (a client
/// UI gate; the backend enforces author-scoping). Add buttons appear only when
/// `canAdd` (OnTheWay/InProgress); the section is read-only on terminal and
/// hides entirely when there's nothing to show and nothing to add.
/// Mutations push through the VM, which signals the parent to re-fetch.
struct NotesAndIssuesSection: View {
    let notes: [OrderNoteDto]
    let issues: [OrderIssueDto]
    let canAdd: Bool
    let isReadOnly: Bool
    @ObservedObject var vm: OrderNotesViewModel

    @State private var entry: EntryContext?
    @State private var deletion: DeletionContext?

    private var hidden: Bool {
        !canAdd && notes.isEmpty && issues.isEmpty
    }

    var body: some View {
        if !hidden {
            OrderSectionCard(title: L10n.Orders.notesAndIssues, systemImage: "note.text") {
                VStack(alignment: .leading, spacing: Spacing.s) {
                    ForEach(notes, id: \.id) { note in
                        EntryRow(
                            text: note.content ?? "",
                            accent: CleansiaColors.primary,
                            showActions: !isReadOnly && vm.isAuthor(noteEmployeeId: note.employeeId),
                            isMutating: vm.mutatingId == note.id,
                            onEdit: { startEdit(noteId: note.id, text: note.content) },
                            onDelete: { deletion = .note(id: note.id ?? "") }
                        )
                    }
                    ForEach(issues, id: \.id) { issue in
                        EntryRow(
                            text: issue.description ?? "",
                            accent: CleansiaColors.error,
                            showActions: !isReadOnly && vm.isAuthor(noteEmployeeId: issue.reportedByEmployeeId),
                            isMutating: vm.mutatingId == issue.id,
                            onEdit: { startEditIssue(issueId: issue.id, text: issue.description) },
                            onDelete: { deletion = .issue(id: issue.id ?? "") }
                        )
                    }
                    if !isReadOnly, canAdd {
                        addButtons
                    }
                }
            }
            .task { await vm.resolveCurrentEmployeeId() }
            .sheet(item: $entry) { context in
                TextEntrySheet(context: context, vm: vm) { entry = nil }
            }
            .overlay { deletionDialog }
        }
    }

    private var addButtons: some View {
        HStack(spacing: Spacing.xs) {
            CleansiaOutlinedButton(L10n.Orders.addNote, size: .medium, leadingIcon: "plus") {
                entry = .addNote
            }
            CleansiaOutlinedButton(L10n.Orders.reportIssue, size: .medium, leadingIcon: "exclamationmark.triangle") {
                entry = .reportIssue
            }
        }
    }

    @ViewBuilder
    private var deletionDialog: some View {
        if let deletion {
            CleansiaDialog(
                title: L10n.Orders.delete,
                confirmLabel: L10n.Orders.delete,
                onConfirm: { confirmDelete(deletion) },
                onDismiss: { self.deletion = nil },
                message: deletion.message,
                dismissLabel: L10n.cancel,
                icon: "trash",
                destructive: true,
                content: { EmptyView() }
            )
        }
    }

    private func startEdit(noteId: String?, text: String?) {
        guard let noteId else { return }
        entry = .editNote(id: noteId, text: text ?? "")
    }

    private func startEditIssue(issueId: String?, text: String?) {
        guard let issueId else { return }
        entry = .editIssue(id: issueId, text: text ?? "")
    }

    private func confirmDelete(_ deletion: DeletionContext) {
        self.deletion = nil
        Task {
            switch deletion {
            case let .note(id): await vm.deleteNote(id)
            case let .issue(id): await vm.deleteIssue(id)
            }
        }
    }
}

private enum DeletionContext {
    case note(id: String)
    case issue(id: String)

    var message: String {
        switch self {
        case .note: L10n.Orders.deleteNoteConfirm
        case .issue: L10n.Orders.deleteIssueConfirm
        }
    }
}

enum EntryContext: Identifiable {
    case addNote
    case reportIssue
    case editNote(id: String, text: String)
    case editIssue(id: String, text: String)

    var id: String {
        switch self {
        case .addNote: "addNote"
        case .reportIssue: "reportIssue"
        case let .editNote(id, _): "editNote:\(id)"
        case let .editIssue(id, _): "editIssue:\(id)"
        }
    }
}

private struct EntryRow: View {
    let text: String
    let accent: Color
    let showActions: Bool
    let isMutating: Bool
    let onEdit: () -> Void
    let onDelete: () -> Void

    var body: some View {
        HStack(spacing: Spacing.s) {
            RoundedRectangle(cornerRadius: 2)
                .fill(accent)
                .frame(width: 3, height: 28)
            Text(text)
                .font(CleansiaTypography.bodyMedium)
                .foregroundColor(CleansiaColors.onSurface)
                .frame(maxWidth: .infinity, alignment: .leading)
            if showActions {
                if isMutating {
                    ProgressView()
                } else {
                    Button(action: onEdit) {
                        Image(systemName: "pencil").foregroundColor(CleansiaColors.onSurfaceVariant)
                    }
                    Button(action: onDelete) {
                        Image(systemName: "trash").foregroundColor(CleansiaColors.error)
                    }
                }
            }
        }
        .padding(Spacing.xs)
        .background(CleansiaColors.surfaceVariant.opacity(0.4), in: RoundedRectangle(cornerRadius: CornerRadius.small))
    }
}

private struct TextEntrySheet: View {
    let context: EntryContext
    @ObservedObject var vm: OrderNotesViewModel
    let onClose: () -> Void

    @State private var text: String = ""

    private var isIssue: Bool {
        switch context {
        case .reportIssue, .editIssue: true
        case .addNote, .editNote: false
        }
    }

    private var title: String {
        switch context {
        case .addNote: L10n.Orders.addNote
        case .reportIssue: L10n.Orders.reportIssue
        case .editNote: L10n.Orders.editNote
        case .editIssue: L10n.Orders.editIssue
        }
    }

    private var fieldLabel: String {
        isIssue ? L10n.Orders.issueDescription : L10n.Orders.noteContent
    }

    private var isSaving: Bool {
        vm.isSavingNote || vm.isReportingIssue || vm.mutatingId != nil
    }

    var body: some View {
        NavigationStack {
            VStack(spacing: Spacing.m) {
                Text(description)
                    .font(CleansiaTypography.bodyMedium)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
                    .multilineTextAlignment(.center)
                CleansiaTextField(value: $text, label: fieldLabel, enabled: !isSaving)
                CleansiaPrimaryButton(L10n.Orders.save, loading: isSaving, enabled: !text.isBlank, action: submit)
                Spacer()
            }
            .padding(Spacing.l)
            .navigationTitle(title)
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .cancellationAction) {
                    Button(L10n.cancel, action: onClose).disabled(isSaving)
                }
            }
            .background(CleansiaColors.background.ignoresSafeArea())
            .onAppear { text = initialText }
        }
        .presentationDetents([.medium, .large])
    }

    private var description: String {
        isIssue ? L10n.Orders.reportIssueDesc : L10n.Orders.addNoteDesc
    }

    private var initialText: String {
        switch context {
        case .addNote, .reportIssue: ""
        case let .editNote(_, text): text
        case let .editIssue(_, text): text
        }
    }

    private func submit() {
        Task {
            switch context {
            case .addNote: await vm.addNote(text)
            case .reportIssue: await vm.reportIssue(text)
            case let .editNote(id, _): await vm.updateNote(id, text)
            case let .editIssue(id, _): await vm.updateIssue(id, text)
            }
            onClose()
        }
    }
}
