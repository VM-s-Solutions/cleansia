import CleansiaCore
import SwiftUI

struct FullscreenImageURL: Identifiable {
    let url: URL
    var id: String {
        url.absoluteString
    }
}

struct EvidencePdfPreview: Identifiable {
    let url: URL
    var id: String {
        url.path
    }
}

struct DisputeDetailView: View {
    @StateObject private var vm: DisputeDetailViewModel
    @Environment(\.snackbarController) private var snackbar

    @State private var draft = ""
    @State private var showEvidencePicker = false
    @State private var fullscreenImage: FullscreenImageURL?
    @State private var pdfPreview: EvidencePdfPreview?

    init(disputeId: String, repository: DisputeRepository, snackbar: SnackbarController) {
        _vm = StateObject(wrappedValue: DisputeDetailViewModel(
            disputeId: disputeId,
            repository: repository,
            snackbar: snackbar
        ))
    }

    var body: some View {
        VStack(spacing: 0) {
            content
            replyBar
        }
        .navigationTitle(L10n.Disputes.detailTitle)
        .navigationBarTitleDisplayMode(.inline)
        .background(CleansiaColors.background.ignoresSafeArea())
        .task { await vm.load() }
        .evidencePicker(isPresented: $showEvidencePicker) { source, _ in
            Task { await vm.uploadEvidence([source]) }
        }
        .fullScreenCover(item: $fullscreenImage) { item in
            FullscreenSingleImage(url: item.url) { fullscreenImage = nil }
        }
        .sheet(item: $pdfPreview) { item in
            pdfPreviewView(item.url)
        }
    }

    @ViewBuilder
    private var content: some View {
        switch vm.state {
        case .loading:
            ProgressView()
                .tint(CleansiaColors.primary)
                .frame(maxWidth: .infinity, maxHeight: .infinity)
        case .error:
            DisputeDetailErrorView { Task { await vm.load() } }
        case let .loaded(detail):
            DisputeThread(
                detail: detail,
                uploading: vm.uploadState.isSubmitting,
                onAddEvidence: { showEvidencePicker = true },
                onImageTap: openImage,
                onPdfTap: openPdf,
                onUnknownTap: { _ in snackbar.showError(L10n.Disputes.evidenceOpenError) }
            )
        }
    }

    @ViewBuilder
    private var replyBar: some View {
        if case let .loaded(detail) = vm.state {
            if detail.allowsMessages {
                ReplyInputBar(
                    draft: $draft,
                    sending: vm.sendState.isSubmitting,
                    onSend: {
                        let body = draft
                        draft = ""
                        Task { await vm.sendMessage(body) }
                    }
                )
            } else {
                Text(L10n.Disputes.detailClosedNote)
                    .font(CleansiaTypography.bodyMedium)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
                    .frame(maxWidth: .infinity)
                    .padding(Spacing.m)
                    .background(CleansiaColors.surface.ignoresSafeArea(edges: .bottom))
            }
        }
    }

    private func openImage(_ evidence: DisputeEvidence) {
        guard let url = evidence.blobURL.flatMap(URL.init(string:)) else {
            snackbar.showError(L10n.Disputes.evidenceOpenError)
            return
        }
        fullscreenImage = FullscreenImageURL(url: url)
    }

    private func openPdf(_ evidence: DisputeEvidence) {
        guard let url = evidence.blobURL.flatMap(URL.init(string:)) else {
            snackbar.showError(L10n.Disputes.evidenceOpenError)
            return
        }
        pdfPreview = EvidencePdfPreview(url: url)
    }

    @ViewBuilder
    private func pdfPreviewView(_ url: URL) -> some View {
        #if canImport(UIKit)
            QuickLookPreview(url: url, deleteOnDismiss: false) { pdfPreview = nil }
                .ignoresSafeArea()
        #else
            EmptyView()
        #endif
    }
}
