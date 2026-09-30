import CleansiaCore
import CleansiaPartnerApi
import SwiftUI

/// The open document is remembered by its type, not its text row, so a re-read after a newer version
/// came into force shows the new text in the sheet that is already open.
struct LegalDocumentsView: View {
    @StateObject private var vm: LegalDocumentsViewModel
    @State private var openType: LegalDocumentType?

    init(client: PartnerProfileClient, snackbar: SnackbarController) {
        _vm = StateObject(wrappedValue: LegalDocumentsViewModel(client: client, snackbar: snackbar))
    }

    var body: some View {
        SectionScaffold(
            title: L10n.LegalDocuments.title,
            isLoading: vm.state.isLoading,
            isError: isError,
            onRetry: { Task { await vm.load() } },
            form: {
                LegalDocumentsList(
                    documents: vm.state.loadedValue ?? [],
                    onOpen: { openType = $0.type }
                )
            }
        )
        .sheet(
            item: openDocument,
            onDismiss: { vm.onDocumentClosed() },
            content: { document in
                LegalDocumentSheet(
                    document: document,
                    notice: vm.notice,
                    actionState: vm.actionState,
                    onAccept: { Task { await vm.accept(document) } },
                    onClose: { openType = nil }
                )
            }
        )
        .task { await vm.load() }
        .onReceive(vm.accepted) { _ in openType = nil }
    }

    private var isError: Bool {
        if case .error = vm.state { return true }
        return false
    }

    private var openDocument: Binding<CleanerLegalDocument?> {
        Binding(
            get: { vm.document(ofType: openType) },
            set: { if $0 == nil { openType = nil } }
        )
    }
}

struct LegalDocumentsList: View {
    let documents: [CleanerLegalDocument]
    var onOpen: (CleanerLegalDocument) -> Void = { _ in }

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.m) {
            Text(documents.isEmpty ? L10n.LegalDocuments.empty : L10n.LegalDocuments.intro)
                .font(CleansiaTypography.bodyMedium)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
                .fixedSize(horizontal: false, vertical: true)
            VStack(spacing: Spacing.s) {
                ForEach(documents) { document in
                    LegalDocumentRow(document: document, onTap: { onOpen(document) })
                }
            }
        }
        .padding(.top, Spacing.s)
    }
}

private struct LegalDocumentRow: View {
    let document: CleanerLegalDocument
    let onTap: () -> Void

    var body: some View {
        Button(action: onTap) {
            HStack(spacing: Spacing.s) {
                Image(systemName: "doc.text")
                    .font(.system(size: 22))
                    .foregroundColor(CleansiaColors.primary)
                VStack(alignment: .leading, spacing: 2) {
                    Text(document.title)
                        .font(CleansiaTypography.titleMedium)
                        .foregroundColor(CleansiaColors.onSurface)
                    Text(L10n.LegalDocuments.version(document.version))
                        .font(CleansiaTypography.labelSmall)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                    AcceptanceLine(document: document)
                }
                Spacer(minLength: 0)
                Image(systemName: "chevron.right")
                    .font(.system(size: 13, weight: .semibold))
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
            }
            .padding(Spacing.s)
            .frame(maxWidth: .infinity, alignment: .leading)
            .background(CleansiaColors.surface, in: RoundedRectangle(cornerRadius: CornerRadius.large))
            .overlay {
                RoundedRectangle(cornerRadius: CornerRadius.large)
                    .stroke(CleansiaColors.outlineVariant, lineWidth: 1)
            }
            .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
    }
}

private struct AcceptanceLine: View {
    @Environment(\.locale) private var locale
    let document: CleanerLegalDocument

    var body: some View {
        HStack(spacing: Spacing.xxs) {
            Image(systemName: document.isAccepted ? "checkmark.circle" : "exclamationmark.circle")
                .font(.system(size: 12))
            Text(document.acceptanceLine(locale: locale))
                .font(CleansiaTypography.labelMedium)
        }
        .foregroundColor(document.isAccepted ? CleansiaColors.primary : CleansiaColors.error)
    }
}

extension CleanerLegalDocument {
    func acceptanceLine(locale: Locale) -> String {
        if isAccepted {
            let date = acceptedAt.map { OrdersFormat.dateTime($0, locale: locale) } ?? "—"
            return L10n.LegalDocuments.acceptedOn(date, acceptedVersion ?? version)
        }
        if let acceptedVersion {
            return L10n.LegalDocuments.newVersion(acceptedVersion)
        }
        return L10n.LegalDocuments.awaiting
    }
}

private struct LegalDocumentSheet: View {
    let document: CleanerLegalDocument
    let notice: LegalDocumentNotice?
    let actionState: ActionState
    let onAccept: () -> Void
    let onClose: () -> Void

    private var submitting: Bool {
        actionState.isSubmitting
    }

    var body: some View {
        NavigationStack {
            LegalDocumentSheetContent(
                document: document,
                notice: notice,
                error: actionState.errorMessage,
                submitting: submitting,
                onAccept: onAccept,
                onClose: onClose
            )
            .navigationTitle(document.title)
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .cancellationAction) {
                    Button(L10n.close, action: onClose).disabled(submitting)
                }
            }
            .background(CleansiaColors.surface.ignoresSafeArea())
        }
        .interactiveDismissDisabled(submitting)
    }
}

struct LegalDocumentSheetContent: View {
    let document: CleanerLegalDocument
    var notice: LegalDocumentNotice?
    var error: String?
    var submitting = false
    var onAccept: () -> Void = {}
    var onClose: () -> Void = {}

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.s) {
            Text(L10n.LegalDocuments.version(document.version))
                .font(CleansiaTypography.labelMedium)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
            if notice == .textUpdated {
                NoticeRow(text: L10n.LegalDocuments.textUpdated)
            }
            Divider().overlay(CleansiaColors.outlineVariant)
            HtmlContentView(html: document.contentHtml)
                .frame(maxWidth: .infinity, maxHeight: .infinity)
            Divider().overlay(CleansiaColors.outlineVariant)
            if let error {
                Text(error)
                    .font(CleansiaTypography.bodyMedium)
                    .foregroundColor(CleansiaColors.error)
            }
            if document.isAccepted {
                AcceptanceLine(document: document)
                CleansiaPrimaryButton(L10n.close, action: onClose)
            } else {
                CleansiaPrimaryButton(
                    L10n.LegalDocuments.accept,
                    loading: submitting,
                    enabled: !submitting,
                    action: onAccept
                )
            }
        }
        .padding(.horizontal, Spacing.m)
        .padding(.top, Spacing.s)
        .padding(.bottom, Spacing.m)
    }
}

#if DEBUG
    extension CleanerLegalDocument {
        static let preview = CleanerLegalDocument(
            type: ._3,
            legalDocumentTextId: "text-1",
            version: "2026-12-01",
            title: "Framework contract",
            contentHtml: "<h2>Parties</h2><p>This framework contract is concluded with the partner.</p>",
            isAccepted: false,
            acceptedVersion: "2026-10-01",
            acceptedAt: Date(timeIntervalSince1970: 1_791_000_000)
        )
    }

    struct LegalDocumentsView_Previews: PreviewProvider {
        static var previews: some View {
            Group {
                ScrollView {
                    LegalDocumentsList(documents: [
                        .preview,
                        CleanerLegalDocument(
                            type: ._5,
                            legalDocumentTextId: "text-2",
                            version: "2026-12-01",
                            title: "Data processing agreement",
                            contentHtml: "<p>Processing.</p>",
                            isAccepted: true,
                            acceptedVersion: "2026-12-01",
                            acceptedAt: Date(timeIntervalSince1970: 1_791_000_000)
                        )
                    ])
                    .padding(.horizontal, Spacing.m)
                }
                .previewDisplayName("List")
                LegalDocumentsList(documents: [])
                    .padding(.horizontal, Spacing.m)
                    .previewDisplayName("Empty")
                LegalDocumentSheetContent(document: .preview, notice: .textUpdated)
                    .previewDisplayName("Sheet · text updated")
            }
            .background(CleansiaColors.background)
        }
    }
#endif
