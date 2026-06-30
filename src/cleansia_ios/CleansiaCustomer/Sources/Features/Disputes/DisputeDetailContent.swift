import CleansiaCore
import SwiftUI
#if canImport(UIKit)
    import UniformTypeIdentifiers
#endif

/// The Add-evidence affordance: a `.confirmationDialog` (Take Photo / Choose
/// image / Choose PDF) over the camera/library picker + a native PDF importer.
/// Take Photo / Choose image route through the Core `CameraOrLibraryPicker`;
/// Choose PDF uses `.fileImporter` (the partner Documents pattern).
struct EvidencePickers: ViewModifier {
    @Binding var showSourceDialog: Bool
    @Binding var showImporter: Bool
    let onTakePhoto: () -> Void
    let onChooseImage: () -> Void
    let onChoosePdf: () -> Void
    let onImportPdf: (Result<[URL], Error>) -> Void

    func body(content: Content) -> some View {
        content
            .confirmationDialog(
                L10n.Disputes.evidenceAddButton,
                isPresented: $showSourceDialog,
                titleVisibility: .visible
            ) {
                Button(L10n.Disputes.addEvidenceTakePhoto, action: onTakePhoto)
                Button(L10n.Disputes.addEvidenceChooseImage, action: onChooseImage)
                Button(L10n.Disputes.addEvidenceChoosePdf, action: onChoosePdf)
                Button(L10n.cancel, role: .cancel) {}
            }
            .fileImporter(
                isPresented: $showImporter,
                allowedContentTypes: pdfTypes,
                allowsMultipleSelection: false,
                onCompletion: onImportPdf
            )
    }

    private var pdfTypes: [UTType] {
        #if canImport(UIKit)
            [.pdf]
        #else
            []
        #endif
    }
}

struct DisputeThread: View {
    let detail: DisputeDetail
    let uploading: Bool
    let onAddEvidence: () -> Void
    let onImageTap: (DisputeEvidence) -> Void
    let onPdfTap: (DisputeEvidence) -> Void
    let onUnknownTap: (DisputeEvidence) -> Void

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: Spacing.s) {
                DisputeHeaderCard(detail: detail)

                if let description = detail.description, !description.isBlank {
                    DisputeMessageBubble(message: DisputeMessage(
                        id: "original",
                        body: description,
                        isStaffMessage: false,
                        createdOn: detail.createdOn
                    ))
                }

                ForEach(detail.messages) { message in
                    DisputeMessageBubble(message: message)
                }

                evidenceSection
                Color.clear.frame(height: Spacing.l)
            }
            .padding(.horizontal, Spacing.ml)
            .padding(.top, Spacing.s)
        }
    }

    @ViewBuilder
    private var evidenceSection: some View {
        if !detail.evidence.isEmpty || detail.allowsMessages {
            Text(L10n.Disputes.evidenceSectionTitle)
                .font(CleansiaTypography.titleMedium)
                .foregroundColor(CleansiaColors.onBackground)
                .padding(.top, Spacing.xs)

            ForEach(detail.evidence) { evidence in
                EvidenceRow(evidence: evidence) {
                    switch evidence.kind {
                    case .image: onImageTap(evidence)
                    case .pdf: onPdfTap(evidence)
                    case .other: onUnknownTap(evidence)
                    }
                }
            }

            if detail.allowsMessages {
                CleansiaOutlinedButton(
                    uploading ? L10n.Disputes.evidenceUploading : L10n.Disputes.evidenceAddButton,
                    leadingIcon: "paperclip",
                    enabled: !uploading,
                    action: onAddEvidence
                )
                .padding(.top, Spacing.xxs)
            }
        }
    }
}

private struct DisputeHeaderCard: View {
    let detail: DisputeDetail

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.xxs) {
            HStack {
                Text(verbatim: detail.reasonName ?? "—")
                    .font(CleansiaTypography.titleMedium)
                    .foregroundColor(CleansiaColors.onSurface)
                Spacer()
                DisputeStatusPill(
                    label: DisputeStatusPresentation.label(detail.statusName),
                    color: DisputeStatusPresentation.color(detail.statusValue)
                )
            }
            Text(OrdersFormat.dateTime(detail.createdOn))
                .font(CleansiaTypography.labelSmall)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .padding(Spacing.m)
        .background(CleansiaColors.surface, in: RoundedRectangle(cornerRadius: CornerRadius.large))
        .overlay(
            RoundedRectangle(cornerRadius: CornerRadius.large)
                .stroke(CleansiaColors.outlineVariant, lineWidth: 1)
        )
    }
}

private struct DisputeMessageBubble: View {
    let message: DisputeMessage

    private var isStaff: Bool {
        message.isStaffMessage
    }

    var body: some View {
        HStack {
            if !isStaff { Spacer(minLength: Spacing.xl) }
            VStack(alignment: isStaff ? .leading : .trailing, spacing: Spacing.xxs) {
                HStack(spacing: Spacing.xs) {
                    Text(isStaff ? L10n.Disputes.detailAuthorSupport : L10n.Disputes.detailAuthorYou)
                        .font(CleansiaTypography.labelSmall)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                    Text(OrdersFormat.dateTime(message.createdOn))
                        .font(CleansiaTypography.labelSmall)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                }
                Text(verbatim: message.body ?? "")
                    .font(CleansiaTypography.bodyMedium)
                    .foregroundColor(isStaff ? CleansiaColors.onSurface : CleansiaColors.onPrimary)
                    .padding(.horizontal, Spacing.m)
                    .padding(.vertical, 10)
                    .background(
                        isStaff ? CleansiaColors.surfaceVariant : CleansiaColors.primary,
                        in: RoundedRectangle(cornerRadius: CornerRadius.medium)
                    )
            }
            if isStaff { Spacer(minLength: Spacing.xl) }
        }
    }
}

private struct EvidenceRow: View {
    let evidence: DisputeEvidence
    let onTap: () -> Void

    var body: some View {
        Button(action: onTap) {
            HStack(spacing: Spacing.s) {
                thumbnail
                VStack(alignment: .leading, spacing: Spacing.xxs) {
                    Text(verbatim: evidence.fileName ?? fallbackLabel)
                        .font(CleansiaTypography.bodyMedium)
                        .foregroundColor(CleansiaColors.onSurface)
                        .lineLimit(1)
                    Text(L10n.Disputes.evidenceCaption(OrdersFormat.dateTime(evidence.uploadedOn)))
                        .font(CleansiaTypography.labelSmall)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                }
                Spacer()
            }
            .padding(Spacing.s)
            .background(CleansiaColors.surface, in: RoundedRectangle(cornerRadius: CornerRadius.medium))
            .overlay(
                RoundedRectangle(cornerRadius: CornerRadius.medium)
                    .stroke(CleansiaColors.outlineVariant, lineWidth: 1)
            )
        }
        .buttonStyle(.plain)
    }

    private var fallbackLabel: String {
        switch evidence.kind {
        case .image: L10n.Disputes.evidenceImageLabel
        case .pdf: L10n.Disputes.evidencePdfLabel
        case .other: "—"
        }
    }

    @ViewBuilder
    private var thumbnail: some View {
        if evidence.kind == .image, let url = evidence.blobURL.flatMap(URL.init(string:)) {
            AsyncImage(url: url) { phase in
                switch phase {
                case let .success(image):
                    image.resizable().scaledToFill()
                case .failure:
                    iconTile("photo")
                default:
                    ZStack { CleansiaColors.surfaceVariant
                        ProgressView()
                    }
                }
            }
            .frame(width: 56, height: 56)
            .clipShape(RoundedRectangle(cornerRadius: CornerRadius.small))
        } else {
            iconTile(evidence.kind == .pdf ? "doc.richtext" : "paperclip")
        }
    }

    private func iconTile(_ systemName: String) -> some View {
        ZStack {
            CleansiaColors.surfaceVariant
            Image(systemName: systemName)
                .font(.system(size: 24))
                .foregroundColor(CleansiaColors.onSurfaceVariant)
        }
        .frame(width: 56, height: 56)
        .clipShape(RoundedRectangle(cornerRadius: CornerRadius.small))
    }
}

struct ReplyInputBar: View {
    @Binding var draft: String
    let sending: Bool
    let onSend: () -> Void

    private var canSend: Bool {
        !sending && !draft.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
    }

    var body: some View {
        HStack(spacing: Spacing.xs) {
            TextField(L10n.Disputes.detailMessagePlaceholder, text: $draft, axis: .vertical)
                .font(CleansiaTypography.bodyLarge)
                .lineLimit(1 ... 4)
                .padding(.horizontal, Spacing.m)
                .padding(.vertical, Spacing.xs)
                .background(CleansiaColors.surfaceVariant, in: Capsule())
                .disabled(sending)
                .onChange(of: draft) { next in
                    if next.count > DisputeFormConstants.messageMaxLength {
                        draft = String(next.prefix(DisputeFormConstants.messageMaxLength))
                    }
                }

            Button(action: onSend) {
                ZStack {
                    Circle().fill(CleansiaColors.primary.opacity(canSend ? 1 : 0.5))
                        .frame(width: 40, height: 40)
                    if sending {
                        ProgressView().tint(CleansiaColors.onPrimary)
                    } else {
                        Image(systemName: "paperplane.fill")
                            .foregroundColor(CleansiaColors.onPrimary)
                    }
                }
            }
            .disabled(!canSend)
            .accessibilityLabel(L10n.Disputes.detailMessageSend)
        }
        .padding(.horizontal, Spacing.s)
        .padding(.vertical, Spacing.xs)
        .background(CleansiaColors.surface.ignoresSafeArea(edges: .bottom))
    }
}

struct DisputeDetailErrorView: View {
    let onRetry: () -> Void

    var body: some View {
        VStack(spacing: Spacing.s) {
            Image(systemName: "wifi.slash")
                .font(.system(size: 44))
                .foregroundColor(CleansiaColors.onSurfaceVariant)
            Text(L10n.Disputes.listErrorTitle)
                .font(CleansiaTypography.titleMedium)
                .foregroundColor(CleansiaColors.onBackground)
                .multilineTextAlignment(.center)
            CleansiaPrimaryButton(L10n.Disputes.listErrorRetry, action: onRetry)
                .fixedSize()
        }
        .padding(Spacing.xl)
        .frame(maxWidth: .infinity, maxHeight: .infinity)
    }
}

struct FullscreenSingleImage: View {
    let url: URL
    let onClose: () -> Void

    var body: some View {
        ZStack(alignment: .topLeading) {
            Color.black.ignoresSafeArea()
            AsyncImage(url: url) { phase in
                switch phase {
                case let .success(image):
                    image.resizable().scaledToFit()
                case .failure:
                    Image(systemName: "photo")
                        .font(.system(size: 48))
                        .foregroundColor(.white.opacity(0.6))
                default:
                    ProgressView().tint(.white)
                }
            }
            .frame(maxWidth: .infinity, maxHeight: .infinity)
            .onTapGesture(perform: onClose)

            Button(action: onClose) {
                Image(systemName: "xmark")
                    .font(.system(size: 18, weight: .semibold))
                    .foregroundColor(.white)
                    .padding(Spacing.s)
            }
        }
    }
}

#if DEBUG
    struct DisputeThread_Previews: PreviewProvider {
        static var previews: some View {
            DisputeThread(
                detail: DisputeDetail(
                    id: "1",
                    displayOrderNumber: "1042",
                    reasonName: "Quality issue",
                    description: "Cleaner skipped the kitchen entirely and left early.",
                    statusName: "Pending",
                    statusValue: 1,
                    createdOn: Date(),
                    messages: [
                        DisputeMessage(
                            id: "m1",
                            body: "Thanks, we're looking into it.",
                            isStaffMessage: true,
                            createdOn: Date()
                        )
                    ],
                    evidence: [
                        DisputeEvidence(id: "e1", fileName: "kitchen.jpg", blobURL: nil, uploadedOn: Date()),
                        DisputeEvidence(id: "e2", fileName: "receipt.pdf", blobURL: nil, uploadedOn: Date())
                    ]
                ),
                uploading: false,
                onAddEvidence: {},
                onImageTap: { _ in },
                onPdfTap: { _ in },
                onUnknownTap: { _ in }
            )
            .background(CleansiaColors.background)
        }
    }
#endif
