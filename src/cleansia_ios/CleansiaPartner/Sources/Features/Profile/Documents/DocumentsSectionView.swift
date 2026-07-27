import CleansiaCore
import CleansiaPartnerApi
import SwiftUI
import UniformTypeIdentifiers

/// A file the cleaner has picked but not yet uploaded: it is held here while
/// the metadata dialog collects a document type (required) and a description.
private struct PendingUpload: Equatable {
    let fileName: String
    let contentType: String
    let base64: String
}

struct DocumentsSectionView: View {
    @StateObject private var vm: DocumentsSectionViewModel
    @State private var importerOpen = false
    @State private var pending: PendingUpload?
    @State private var pendingType: String?
    @State private var pendingDescription = ""

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
                                isDeleting: vm.deletingId == document.documentId,
                                onDelete: {
                                    guard let id = document.documentId else { return }
                                    Task { await vm.delete(documentId: id) }
                                }
                            )
                        }
                    }
                    CleansiaPrimaryButton(
                        L10n.Profile.uploadDocument,
                        leadingIcon: "arrow.up.doc",
                        loading: vm.action.isSubmitting,
                        enabled: !vm.action.isSubmitting,
                        action: { importerOpen = true }
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
    }

    /// Deliberately an in-tree overlay, not a `.sheet`. `.fileImporter` is
    /// itself a presentation, and state set from its completion handler that
    /// would trigger a second sheet gets swallowed while the importer
    /// dismisses. `CleansiaDialog` is a ZStack overlay, so it just appears.
    @ViewBuilder
    private var uploadDialog: some View {
        if let pending {
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
                                CleansiaDropdownOption(id: DocumentPresentation.optionId($0.type), label: $0.label())
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

    /// Reads the file and parks it; the upload itself waits for the dialog.
    /// Uploading straight from here is what made every document land as
    /// IdentityCard with no description.
    private func handleImport(_ result: Result<[URL], Error>) {
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
            base64: data.base64EncodedString()
        )
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
    let isDeleting: Bool
    let onDelete: () -> Void

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
            if isDeleting {
                ProgressView()
            } else {
                Button(action: onDelete) {
                    Image(systemName: "trash")
                        .foregroundColor(CleansiaColors.error)
                }
                .buttonStyle(.plain)
                .accessibilityLabel(L10n.Profile.documentsDelete)
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
