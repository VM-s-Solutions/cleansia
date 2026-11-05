import CleansiaCore
import CleansiaPartnerApi
import SwiftUI

/// Notes + issues block. Lists existing notes/issues (read from the parent
/// order payload), with per-row edit/delete shown only to the author (a client
/// UI gate; the backend enforces author-scoping). Add buttons appear only when
/// `canAdd` (OnTheWay/InProgress) and the section is read-only on terminal;
/// whether it appears at all is `OrderDetail.showsNotesAndIssues`, so the gate is
/// drivable without a UI harness. Mutations push through the VM, which signals
/// the parent to re-fetch.
struct NotesAndIssuesSection: View {
    let notes: [OrderNoteDto]
    let issues: [OrderIssueDto]
    let canAdd: Bool
    let isReadOnly: Bool
    @ObservedObject var vm: OrderNotesViewModel

    @Environment(\.locale) private var locale
    @State private var entry: EntryContext?
    @State private var deletion: DeletionContext?

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.xs) {
            DetailEyebrow(text: L10n.Orders.notesAndIssues)
            if !isReadOnly, canAdd {
                addButtons
            }
            ForEach(Array(notes.enumerated()), id: \.offset) { _, note in
                EntryRow(
                    head: [
                        vm.isAuthor(noteEmployeeId: note.employeeId) ? L10n.Orders.crewYou : nil,
                        time(note.createdOn)
                    ]
                    .compactMap { $0 }.map { Text($0) },
                    text: note.content ?? "",
                    showActions: !isReadOnly && vm.isAuthor(noteEmployeeId: note.employeeId),
                    isMutating: vm.mutatingId == note.id,
                    onEdit: { startEdit(noteId: note.id, text: note.content) },
                    onDelete: { deletion = .note(id: note.id ?? "") }
                )
            }
            ForEach(Array(issues.enumerated()), id: \.offset) { _, issue in
                EntryRow(
                    head: [
                        Text(L10n.Orders.reportIssue).foregroundColor(CleansiaColors.error),
                        time(issue.createdOn).map { Text($0) },
                        issue.isResolved == true ? nil : Text(L10n.Orders.issueNotResolved)
                    ].compactMap { $0 },
                    text: issue.description ?? "",
                    showActions: !isReadOnly && vm.isAuthor(noteEmployeeId: issue.reportedByEmployeeId),
                    isMutating: vm.mutatingId == issue.id,
                    onEdit: { startEditIssue(issueId: issue.id, text: issue.description) },
                    onDelete: { deletion = .issue(id: issue.id ?? "") }
                )
            }
        }
        .task { await vm.resolveCurrentEmployeeId() }
        .sheet(item: $entry) { context in
            TextEntrySheet(context: context, vm: vm) { entry = nil }
        }
        .alert(
            L10n.Orders.delete,
            isPresented: Binding(get: { deletion != nil }, set: { if !$0 { deletion = nil } }),
            presenting: deletion
        ) { deletion in
            Button(L10n.Orders.delete, role: .destructive) { confirmDelete(deletion) }
            Button(L10n.cancel, role: .cancel) {}
        } message: { deletion in
            Text(deletion.message)
        }
    }

    struct AddButton {
        let entry: EntryContext
        let title: String
        let icon: String
        let tone: TonalChip.Tone
    }

    static var addButtons: [AddButton] {
        [
            AddButton(entry: .addNote, title: L10n.Orders.addNote, icon: "pencil", tone: .primary),
            AddButton(entry: .reportIssue, title: L10n.Orders.reportIssue, icon: "flag", tone: .error)
        ]
    }

    /// Two equal halves on the photo columns' grid, each label centred in its half; one column once a label outgrows
    /// its half.
    private var addButtons: some View {
        let chips = Self.addButtons.map { button in
            TonalChip(title: button.title, icon: button.icon, tone: button.tone, fills: true) { entry = button.entry }
        }
        return ViewThatFits(in: .horizontal) {
            HStack(spacing: Spacing.s) {
                ForEach(chips.indices, id: \.self) { chips[$0] }
            }
            VStack(spacing: Spacing.xs) {
                ForEach(chips.indices, id: \.self) { chips[$0] }
            }
        }
    }

    private func time(_ date: Date?) -> String? {
        date.map { OrdersFormat.timeOnly($0, locale: locale) }
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

enum EntryContext: Identifiable, Equatable {
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

/// One note or issue: who and when on top, the words under it, and for its author a way to edit or delete it, centred
/// on the top line.
private struct EntryRow: View {
    let head: [Text]
    let text: String
    let showActions: Bool
    let isMutating: Bool
    let onEdit: () -> Void
    let onDelete: () -> Void

    var body: some View {
        HStack(alignment: .firstTextBaseline, spacing: Spacing.s) {
            VStack(alignment: .leading, spacing: Spacing.hair) {
                OrdersDotFlow(items: head, style: CleansiaTypography.scale.labelMedium)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
                Text(text)
                    .font(CleansiaTypography.bodyMedium)
                    .foregroundColor(CleansiaColors.onSurface)
                    .fixedSize(horizontal: false, vertical: true)
            }
            .frame(maxWidth: .infinity, alignment: .leading)
            if showActions {
                if isMutating {
                    ProgressView().capBandAligned(of: CleansiaTypography.scale.labelMedium)
                } else {
                    Button(action: onEdit) {
                        Image(systemName: "pencil").foregroundColor(CleansiaColors.onSurfaceVariant)
                    }
                    .capBandAligned(of: CleansiaTypography.scale.labelMedium)
                    Button(action: onDelete) {
                        Image(systemName: "trash").foregroundColor(CleansiaColors.error)
                    }
                    .capBandAligned(of: CleansiaTypography.scale.labelMedium)
                }
            }
        }
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
                CleansiaTextArea(value: $text, label: fieldLabel, minHeight: 120, enabled: !isSaving)
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
