import CleansiaCore
import CleansiaPartnerApi
import SwiftUI
import UniformTypeIdentifiers

/// A file the cleaner has picked but not yet sent: it is held here while a dialog collects the rest.
///
/// For a fresh upload that dialog asks for a document type (required) and a description. For a
/// REPLACEMENT it asks only for a description — the server carries the type over from the version
/// being replaced, so offering a picker would promise a choice the request cannot express.
private struct PendingUpload: Equatable {
    let fileName: String
    let contentType: String
    let base64: String

    /// Set when this file supersedes an existing document rather than adding one. The two flows share
    /// the importer and the read and differ only in which endpoint the dialog calls, so the target
    /// rides along with the file instead of in a second piece of state that could disagree with it.
    var replacesDocumentId: String?
}

struct DocumentsSectionView: View {
    @StateObject private var vm: DocumentsSectionViewModel
    @State private var importerOpen = false
    @State private var pending: PendingUpload?
    @State private var pendingType: String?
    @State private var pendingDescription = ""

    /// Which document the next pick replaces, and which one a removal is being asked about. Both are
    /// decisions in flight, and both are behind a confirmation — the button this replaced removed the
    /// document on the first tap, with no dialog on either platform.
    @State private var replacingDocumentId: String?
    @State private var deletionTarget: String?
    @State private var deletionReason = ""

    init(client: PartnerProfileClient, snackbar: SnackbarController) {
        _vm = StateObject(wrappedValue: DocumentsSectionViewModel(client: client, snackbar: snackbar))
    }

    var body: some View {
        SectionScaffold(
            title: L10n.Profile.myDocuments,
            isLoading: vm.state.isLoading,
            form: {
                switch vm.state {
                case .loading:
                    EmptyView()
                case .error:
                    DocumentsErrorState(onRetry: { Task { await vm.load() } })
                case let .loaded(documents):
                    // The checklist leads, uploaded or not. It is the answer to "what do you want
                    // from me" that this screen used to leave to support.
                    if !vm.requirements.isEmpty {
                        RequirementsCard(requirements: vm.requirements)
                    }
                    if documents.isEmpty {
                        Text(L10n.Profile.documentsEmpty)
                            .font(CleansiaTypography.bodyMedium)
                            .foregroundColor(CleansiaColors.onSurfaceVariant)
                            .frame(maxWidth: .infinity, alignment: .center)
                            .padding(.vertical, Spacing.l)
                    } else {
                        ForEach(documents, id: \.documentId) { document in
                            DocumentRow(
                                document: document,
                                isBusy: vm.busyDocumentId == document.documentId,
                                onReplace: {
                                    guard let id = document.documentId else { return }
                                    replacingDocumentId = id
                                    importerOpen = true
                                },
                                onRequestDeletion: {
                                    guard let id = document.documentId else { return }
                                    deletionReason = ""
                                    deletionTarget = id
                                }
                            )
                        }
                    }
                    CleansiaPrimaryButton(
                        L10n.Profile.uploadDocument,
                        leadingIcon: "arrow.up.doc",
                        loading: vm.action.isSubmitting,
                        enabled: !vm.action.isSubmitting,
                        // Cleared HERE, where a fresh upload starts, and not only in handleImport:
                        // .fileImporter has no onCancellation before iOS 17 and does not reliably
                        // call back when the browser is dismissed, so a target set by Replace can
                        // outlive the pick that was meant to consume it. Android needs no equivalent
                        // — GetContent() always fires with a null Uri.
                        action: {
                            replacingDocumentId = nil
                            importerOpen = true
                        }
                    )
                    .padding(.top, Spacing.s)
                }
            }
        )
        .task { await vm.load() }
        .fileImporter(
            isPresented: $importerOpen,
            allowedContentTypes: [.pdf, .image],
            allowsMultipleSelection: false
        ) { result in
            handleImport(result)
        }
        .overlay { uploadDialog }
        .overlay { deletionDialog }
    }

    /// Deliberately an in-tree overlay, not a `.sheet`. `.fileImporter` is
    /// itself a presentation, and state set from its completion handler that
    /// would trigger a second sheet gets swallowed while the importer
    /// dismisses. `CleansiaDialog` is a ZStack overlay, so it just appears.
    @ViewBuilder
    private var uploadDialog: some View {
        if let pending {
            if let replaces = pending.replacesDocumentId {
                CleansiaDialog(
                    title: L10n.Profile.documentReplaceTitle,
                    confirmLabel: L10n.Profile.documentReplace,
                    onConfirm: { confirmReplace(pending, documentId: replaces) },
                    onDismiss: clearPending,
                    message: L10n.Profile.documentReplaceMessage(pending.fileName),
                    dismissLabel: L10n.cancel,
                    icon: "arrow.triangle.2.circlepath",
                    confirmEnabled: !vm.action.isSubmitting,
                    content: {
                        CleansiaTextField(
                            value: $pendingDescription,
                            label: L10n.Profile.descriptionOptional,
                            enabled: !vm.action.isSubmitting
                        )
                    }
                )
            } else {
                CleansiaDialog(
                    title: L10n.Profile.uploadDocument,
                    confirmLabel: L10n.Profile.save,
                    onConfirm: { confirmUpload(pending) },
                    onDismiss: clearPending,
                    message: pending.fileName,
                    dismissLabel: L10n.cancel,
                    confirmEnabled: pendingType != nil && !vm.action.isSubmitting,
                    content: {
                        VStack(spacing: Spacing.s) {
                            CleansiaDropdown(
                                selectedId: $pendingType,
                                options: DocumentPresentation.types.map {
                                    CleansiaDropdownOption(
                                        id: DocumentPresentation.optionId($0.type),
                                        label: $0.label()
                                    )
                                },
                                label: L10n.Profile.documentType,
                                placeholder: L10n.Profile.documentType,
                                enabled: !vm.action.isSubmitting
                            )
                            CleansiaTextField(
                                value: $pendingDescription,
                                label: L10n.Profile.descriptionOptional,
                                enabled: !vm.action.isSubmitting
                            )
                        }
                    }
                )
            }
        }
    }

    /// The reason is required by the server and required here — without one an admin is being asked
    /// to rule on nothing, which is the whole point of routing this past a person.
    @ViewBuilder
    private var deletionDialog: some View {
        if let documentId = deletionTarget {
            CleansiaDialog(
                title: L10n.Profile.documentRequestDeletionTitle,
                confirmLabel: L10n.Profile.documentRequestDeletion,
                onConfirm: {
                    let reason = deletionReason.trimmingCharacters(in: .whitespacesAndNewlines)
                    deletionTarget = nil
                    Task { await vm.requestDeletion(documentId: documentId, reason: reason) }
                },
                onDismiss: { deletionTarget = nil },
                message: L10n.Profile.documentRequestDeletionMessage,
                dismissLabel: L10n.cancel,
                icon: "trash",
                destructive: true,
                confirmEnabled: !deletionReason.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
                    && vm.busyDocumentId == nil,
                content: {
                    CleansiaTextField(
                        value: $deletionReason,
                        label: L10n.Profile.documentDeletionReason,
                        enabled: vm.busyDocumentId == nil
                    )
                }
            )
        }
    }

    /// Reads the file and parks it; the upload itself waits for the dialog.
    /// Uploading straight from here is what made every document land as
    /// IdentityCard with no description.
    private func handleImport(_ result: Result<[URL], Error>) {
        // Taken and cleared BEFORE the first early return. Cancelling the importer, an unreadable
        // file and an oversize one all leave this function without publishing anything, and a target
        // left standing would make the NEXT plain upload silently supersede whatever was last
        // tapped. Android clears it at the top of its picker callback for the same reason.
        let replaces = replacingDocumentId
        replacingDocumentId = nil

        guard case let .success(urls) = result, let url = urls.first else { return }
        let accessed = url.startAccessingSecurityScopedResource()
        defer { if accessed { url.stopAccessingSecurityScopedResource() } }
        guard let data = try? Data(contentsOf: url) else { return }
        guard data.count <= DocumentPresentation.maxDocumentBytes else {
            vm.showTooLarge()
            return
        }
        let contentType = UTType(filenameExtension: url.pathExtension)?.preferredMIMEType
            ?? "application/octet-stream"
        pending = PendingUpload(
            fileName: url.lastPathComponent,
            contentType: contentType,
            base64: data.base64EncodedString(),
            replacesDocumentId: replaces
        )
    }

    private func confirmReplace(_ upload: PendingUpload, documentId: String) {
        let description = pendingDescription.trimmedOrNil
        clearPending()
        Task {
            await vm.replace(
                documentId: documentId,
                fileName: upload.fileName,
                contentType: upload.contentType,
                base64Content: upload.base64,
                description: description
            )
        }
    }

    private func confirmUpload(_ upload: PendingUpload) {
        guard let documentType = DocumentPresentation.type(forOptionId: pendingType) else { return }
        let description = pendingDescription.trimmedOrNil
        clearPending()
        Task {
            await vm.upload(
                documentType: documentType,
                fileName: upload.fileName,
                contentType: upload.contentType,
                base64Content: upload.base64,
                description: description
            )
        }
    }

    private func clearPending() {
        pending = nil
        pendingType = nil
        pendingDescription = ""
    }
}

private struct DocumentsErrorState: View {
    let onRetry: () -> Void

    var body: some View {
        VStack(spacing: Spacing.m) {
            Text(L10n.Profile.errorGeneric)
                .font(CleansiaTypography.bodyMedium)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
                .multilineTextAlignment(.center)
            CleansiaOutlinedButton(L10n.retry, size: .medium, action: onRetry)
                .fixedSize()
        }
        .frame(maxWidth: .infinity)
        .padding(.vertical, Spacing.l)
    }
}

private struct DocumentRow: View {
    let document: GetMyDocumentsMyDocumentDto
    let isBusy: Bool
    let onReplace: () -> Void
    let onRequestDeletion: () -> Void

    var body: some View {
        HStack(spacing: Spacing.s) {
            Image(systemName: "doc.text")
                .foregroundColor(CleansiaColors.primary)
            VStack(alignment: .leading, spacing: 2) {
                Text(document.fileName ?? L10n.Profile.noData)
                    .font(CleansiaTypography.titleMedium)
                    .foregroundColor(CleansiaColors.onSurface)
                    .lineLimit(1)
                // Type tells the cleaner (and matches what admin sees for
                // verification); status is the only place a rejection surfaces.
                HStack(spacing: 0) {
                    Text(DocumentPresentation.typeLabel(document.documentType))
                        .font(CleansiaTypography.labelMedium)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                    Text(" · ")
                        .font(CleansiaTypography.labelMedium)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                    Text(DocumentPresentation.statusLabel(document.status))
                        .font(CleansiaTypography.labelMedium)
                        .foregroundColor(DocumentPresentation.statusTint(document.status))
                }
                .lineLimit(1)
            }
            Spacer()
            if isBusy {
                ProgressView()
            } else {
                // Replacing is not tinted as destructive on purpose: it needs no admin and costs the
                // cleaner nothing, where asking for removal hands the decision away.
                Button(action: onReplace) {
                    Image(systemName: "arrow.triangle.2.circlepath")
                        .foregroundColor(CleansiaColors.primary)
                }
                .buttonStyle(.plain)
                .accessibilityLabel(L10n.Profile.documentReplace)
                Button(action: onRequestDeletion) {
                    Image(systemName: "trash")
                        .foregroundColor(CleansiaColors.error)
                }
                .buttonStyle(.plain)
                .accessibilityLabel(L10n.Profile.documentRequestDeletion)
            }
        }
        .padding(Spacing.m)
        .frame(maxWidth: .infinity)
        .background(CleansiaColors.surface)
        .overlay(
            RoundedRectangle(cornerRadius: CornerRadius.medium)
                .stroke(CleansiaColors.outline, lineWidth: 1)
        )
        .clipShape(RoundedRectangle(cornerRadius: CornerRadius.medium))
    }
}

/// What the cleaner's country asks for, resolved against what they have uploaded.
///
/// Optional rows are listed too — that is the difference between "we would like this" and "you cannot
/// start without this", and both are worth telling somebody.
private struct RequirementsCard: View {
    let requirements: [MyDocumentRequirementDto]

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.xs) {
            Text(L10n.Profile.documentRequirementsTitle)
                .font(CleansiaTypography.titleMedium)
                .foregroundColor(CleansiaColors.onSurface)
            Text(L10n.Profile.documentRequirementsSubtitle)
                .font(CleansiaTypography.labelMedium)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
            ForEach(requirements, id: \.documentType) { requirement in
                HStack(alignment: .top, spacing: Spacing.s) {
                    VStack(alignment: .leading, spacing: 2) {
                        Text(DocumentPresentation.typeLabel(requirement.documentType))
                            .font(CleansiaTypography.bodyMedium)
                            .foregroundColor(CleansiaColors.onSurface)
                        Text(
                            requirement.isRequired == true
                                ? L10n.Profile.documentRequirementRequired
                                : L10n.Profile.documentRequirementOptional
                        )
                        .font(CleansiaTypography.labelMedium)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                    }
                    Spacer()
                    if let status = requirement.status {
                        Text(DocumentPresentation.statusLabel(status))
                            .font(CleansiaTypography.labelMedium)
                            .foregroundColor(DocumentPresentation.statusTint(status))
                    } else {
                        Text(L10n.Profile.documentRequirementMissing)
                            .font(CleansiaTypography.labelMedium)
                            .foregroundColor(CleansiaColors.onSurfaceVariant)
                    }
                }
                .padding(.top, Spacing.xs)
            }
        }
        .padding(Spacing.m)
        .frame(maxWidth: .infinity, alignment: .leading)
        .background(CleansiaColors.surface)
        .overlay(
            RoundedRectangle(cornerRadius: CornerRadius.medium)
                .stroke(CleansiaColors.outline, lineWidth: 1)
        )
        .clipShape(RoundedRectangle(cornerRadius: CornerRadius.medium))
    }
}
