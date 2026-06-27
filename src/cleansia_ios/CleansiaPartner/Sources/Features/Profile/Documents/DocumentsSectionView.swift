import CleansiaCore
import CleansiaPartnerApi
import SwiftUI
import UniformTypeIdentifiers

struct DocumentsSectionView: View {
    @StateObject private var vm: DocumentsSectionViewModel
    @State private var importerOpen = false

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
    }

    private func handleImport(_ result: Result<[URL], Error>) {
        guard case let .success(urls) = result, let url = urls.first else { return }
        let accessed = url.startAccessingSecurityScopedResource()
        defer { if accessed { url.stopAccessingSecurityScopedResource() } }
        guard let data = try? Data(contentsOf: url) else { return }
        let contentType = UTType(filenameExtension: url.pathExtension)?.preferredMIMEType
            ?? "application/octet-stream"
        Task {
            await vm.upload(
                documentType: ._1,
                fileName: url.lastPathComponent,
                contentType: contentType,
                base64Content: data.base64EncodedString(),
                description: nil
            )
        }
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
