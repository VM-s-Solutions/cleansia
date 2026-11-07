import CleansiaCore
import CleansiaPartnerApi
import SwiftUI

/// The contract for work, on one screen with the gesture that accepts it: the job's facts, a plain summary of
/// what the contract means for the cleaner, the text, and the swipe beneath. A take and a standalone acceptance
/// differ only in what the swipe calls; a read shows the stored facts and the accepted version with no swipe.
///
/// Built per presentation, so every open re-loads — the facts are the server's word at that moment. Opened from
/// the board it also knows the row the card showed: its facts appear at once while the contract loads, the
/// board's rules for the job sit above the text, and a refusal keeps the sheet open. A take from the board or the
/// detail ends on the taken job.
struct WorkContractSheet: View {
    @StateObject private var vm: WorkContractSheetViewModel
    @State private var takenHeight: CGFloat = 0
    private let board: BoardTake?
    private let taken: TakenJob?
    private let onDismiss: () -> Void
    private let onOutcome: (WorkContractOutcome) -> Void
    private let onOpenLegalDocuments: () -> Void
    private let onOpenJob: () -> Void
    private let onBackToJobs: (() -> Void)?

    /// `taken` is the panel's facts from a host other than the board, which also asks for the panel with
    /// `endsOnTakenPanel`; the board's come from its row.
    init(
        request: WorkContractRequest,
        client: PartnerOrderClient,
        board: BoardTake? = nil,
        taken: TakenJob? = nil,
        endsOnTakenPanel: Bool = false,
        onDismiss: @escaping () -> Void,
        onOutcome: @escaping (WorkContractOutcome) -> Void,
        onOpenLegalDocuments: @escaping () -> Void,
        onOpenJob: @escaping () -> Void = {},
        onBackToJobs: (() -> Void)? = nil
    ) {
        _vm = StateObject(wrappedValue: WorkContractSheetViewModel(
            request: request,
            client: client,
            hasBoardContext: board != nil,
            endsOnTakenPanel: endsOnTakenPanel
        ))
        self.board = board
        self.taken = taken
        self.onDismiss = onDismiss
        self.onOutcome = onOutcome
        self.onOpenLegalDocuments = onOpenLegalDocuments
        self.onOpenJob = onOpenJob
        self.onBackToJobs = onBackToJobs
    }

    private var submitting: Bool {
        vm.actionState.isSubmitting
    }

    /// The taken sheet hugs what it says; every other state takes the whole height.
    private var detents: Set<PresentationDetent> {
        guard case .taken = vm.state, takenHeight > 0 else { return [.large] }
        return [.height(takenHeight)]
    }

    var body: some View {
        WorkContractSheetContent(
            request: vm.request,
            state: vm.state,
            board: board,
            taken: taken,
            notice: vm.notice,
            submitting: submitting,
            onAccept: { Task { await vm.accept() } },
            onRetry: { Task { await vm.load() } },
            onClose: onDismiss,
            onOpenLegalDocuments: onOpenLegalDocuments,
            onOpenJob: onOpenJob,
            onBackToJobs: onBackToJobs,
            onTakenHeight: { takenHeight = $0 }
        )
        .presentationDetents(detents)
        .presentationDragIndicator(.visible)
        .interactiveDismissDisabled(submitting)
        .task { await vm.load() }
        .onReceive(vm.outcome) { onOutcome($0) }
        .onChange(of: submitting) { busy in
            if busy { OrdersAccessibility.announce(L10n.WorkContract.accepting) }
        }
    }
}

struct WorkContractSheetContent: View {
    let request: WorkContractRequest
    let state: WorkContractSheetState
    var board: BoardTake?
    var taken: TakenJob?
    var notice: WorkContractNotice?
    var submitting = false
    var onAccept: () -> Void = {}
    var onRetry: () -> Void = {}
    var onClose: () -> Void = {}
    var onOpenLegalDocuments: () -> Void = {}
    var onOpenJob: () -> Void = {}
    var onBackToJobs: (() -> Void)?
    var onTakenHeight: (CGFloat) -> Void = { _ in }
    var now = Date()

    @State private var scrolled = false

    /// A read names the stored contract's order; a take or an acceptance shows only the job's facts.
    static func orderLine(_ facts: WorkContractJobFacts, request: WorkContractRequest) -> String? {
        guard case .read = request, let number = facts.orderNumber, !number.isBlank else { return nil }
        return L10n.WorkContract.orderNumber(number)
    }

    /// "Take this job?" from the board, none once taken; the contract's own title everywhere else.
    static func title(request: WorkContractRequest, state: WorkContractSheetState, fromBoard: Bool) -> String {
        if case .taken = state { return "" }
        if fromBoard, case .take = request { return L10n.WorkContract.takeSheetTitle }
        return state.loadedContract?.title ?? L10n.WorkContract.title
    }

    /// The header stays put on the sheet's own surface and takes a rule once the body scrolls under it; the taken job
    /// has none, its own title under the grabber. Each state change crossfades over 200 ms; a refusal keeps the
    /// loaded screen, whose swipe area fades.
    var body: some View {
        VStack(spacing: 0) {
            if showsHeader {
                header
            }
            ZStack { content }
                .animation(.easeInOut(duration: 0.2), value: phase)
                .frame(maxHeight: .infinity)
        }
        .background(CleansiaColors.surface.ignoresSafeArea())
        .onPreferenceChange(OrdersScrolledKey.self) { scrolled = $0 }
    }

    private var header: some View {
        HStack(alignment: .center, spacing: Spacing.s) {
            Text(Self.title(request: request, state: state, fromBoard: board != nil))
                .cleansiaFont(CleansiaTypography.headlineSmall)
                .foregroundColor(CleansiaColors.onSurface)
                .fixedSize(horizontal: false, vertical: true)
                .accessibilityAddTraits(.isHeader)
            Spacer(minLength: 0)
            CleansiaTextLink(L10n.close, action: onClose)
                .disabled(submitting)
                .padding(.trailing, -Spacing.xxs)
        }
        .frame(minHeight: 48)
        .padding(.horizontal, Spacing.m)
        .padding(.top, Spacing.m)
        .padding(.bottom, Spacing.xxs)
        .background(CleansiaColors.surface)
        .overlay(alignment: .bottom) {
            Rectangle().fill(CleansiaColors.outlineVariant).frame(height: 1).opacity(scrolled ? 1 : 0)
        }
        .zIndex(1)
    }

    private var showsHeader: Bool {
        if case .taken = state { return false }
        return true
    }

    private var phase: Int {
        switch state {
        case .loading: 0
        case .error: 1
        case .unavailable: 2
        case .legalDocumentsNotAccepted: 3
        case .loaded, .refused: 4
        case .taken: 5
        }
    }

    private var refusal: ApiError? {
        guard case let .refused(_, error) = state else { return nil }
        return error
    }

    @ViewBuilder
    private var content: some View {
        switch state {
        case .loading:
            if let board {
                BoardLoadingContract(snapshot: board.snapshot, now: now)
            } else {
                ProgressView()
                    .frame(maxWidth: .infinity, maxHeight: .infinity)
            }
        case .error:
            SheetMessage(text: L10n.WorkContract.loadError, ctaLabel: L10n.retry, onCta: onRetry)
        case let .unavailable(error):
            SheetMessage(text: ApiErrorLocalizer().message(for: error), ctaLabel: L10n.close, onCta: onClose)
        case .legalDocumentsNotAccepted:
            SheetMessage(
                text: ApiErrorLocalizer().message(for: ApiError(code: WorkContractErrorKey.legalDocumentsNotAccepted)),
                ctaLabel: L10n.LegalDocuments.review,
                onCta: onOpenLegalDocuments
            )
        case let .loaded(contract), let .refused(contract, _):
            LoadedContract(
                request: request,
                contract: contract,
                board: board,
                notice: notice,
                submitting: submitting,
                refusal: refusal,
                now: now,
                onAccept: onAccept,
                onClose: onClose
            )
        case let .taken(contract):
            TakenContract(
                contract: contract,
                job: taken ?? board.map(TakenJob.init(board:)),
                onBack: onBackToJobs ?? onClose,
                onOpenJob: onOpenJob,
                onHeight: onTakenHeight,
                now: now
            )
        }
    }
}

private struct LoadedContract: View {
    @Environment(\.locale) private var locale
    let request: WorkContractRequest
    let contract: WorkContract
    let board: BoardTake?
    let notice: WorkContractNotice?
    let submitting: Bool
    let refusal: ApiError?
    let now: Date
    let onAccept: () -> Void
    let onClose: () -> Void
    @State private var contractHeight: CGFloat = 1

    /// One scroll for the facts, the four lines, the rules and the contract, with the swipe pinned under it. The
    /// contract is as tall as its text, and the page ends with the text's own bottom margin.
    var body: some View {
        VStack(spacing: 0) {
            ScrollView {
                VStack(alignment: .leading, spacing: 0) {
                    facts
                    HtmlContentView(html: contract.contentHtml) { contractHeight = $0 }
                        .frame(maxWidth: .infinity)
                        .frame(height: contractHeight)
                        .padding(.top, Spacing.xs)
                }
                .padding(.horizontal, Spacing.m)
                .padding(.top, Spacing.xxs)
                .background(GeometryReader { content in
                    Color.clear.preference(
                        key: OrdersScrolledKey.self,
                        value: content.frame(in: .named(OrdersScrolledKey.space)).minY < -0.5
                    )
                })
            }
            .coordinateSpace(name: OrdersScrolledKey.space)
            foot
                .padding(.horizontal, Spacing.m)
                .padding(.vertical, Spacing.s)
                .background(CleansiaColors.surface)
                .overlay(alignment: .top) { Divider().overlay(CleansiaColors.outlineVariant) }
        }
    }

    /// The facts card, the summary (it is the point of ruling 4, so it comes before the rules), the board's rules
    /// for this job, and the contract's heading.
    private var facts: some View {
        VStack(alignment: .leading, spacing: 0) {
            TakeFactsCard(
                contract: contract,
                snapshot: board?.snapshot,
                orderLine: WorkContractSheetContent.orderLine(contract.facts, request: request),
                now: now
            )
            if OrdersBoardRules.summaryVisible(request, version: contract.version) {
                ContractSummary().padding(.top, Spacing.m)
            }
            if let snapshot = board?.snapshot {
                BoardRules(snapshot: snapshot, contract: contract)
            }
            if let acceptance = contract.acceptance {
                AcceptanceFacts(acceptance: acceptance, renderedLanguage: contract.language, locale: locale)
                    .padding(.top, Spacing.s)
            }
            Divider().overlay(CleansiaColors.outlineVariant).padding(.top, Spacing.m).padding(.horizontal, -Spacing.m)
            contractHeading.padding(.top, Spacing.m)
        }
    }

    private var foot: some View {
        VStack(alignment: .leading, spacing: Spacing.xs) {
            if let notice { NoticeRow(text: notice.message) }
            gesture
        }
        .animation(.easeInOut(duration: 0.2), value: refusal == nil)
    }

    private var contractHeading: some View {
        VStack(alignment: .leading, spacing: Spacing.hair) {
            if board != nil {
                Text(L10n.WorkContract.title)
                    .font(CleansiaTypography.titleMedium)
                    .foregroundColor(CleansiaColors.onSurface)
                    .accessibilityAddTraits(.isHeader)
            }
            let terms = L10n.WorkContract.version(contract.version) + OrdersBoardRules.dot + L10n.WorkContract.parties
            Text(verbatim: terms)
                .font(CleansiaTypography.labelMedium)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
                .fixedSize(horizontal: false, vertical: true)
        }
    }

    @ViewBuilder
    private var gesture: some View {
        switch request {
        case .take, .accept:
            if let refusal {
                RefusedTake(reason: ApiErrorLocalizer().message(for: refusal), onBack: onClose)
            } else {
                SlideToConfirm(
                    idleLabel: L10n.tie(L10n.WorkContract.swipeToAccept),
                    busyLabel: L10n.tie(L10n.WorkContract.accepting),
                    isBusy: submitting,
                    maxLabelLines: 2,
                    thumbLanes: true,
                    onConfirm: onAccept
                )
            }
        case .read:
            CleansiaPrimaryButton(L10n.close, action: onClose)
        }
    }
}

/// From the board, while the contract loads: the card's own facts at once, the rest held as skeletons, and the
/// swipe waiting for the text it accepts.
private struct BoardLoadingContract: View {
    @Environment(\.locale) private var locale
    let snapshot: OrderListItem
    let now: Date

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            FactsCardLayout(
                date: snapshot.cleaningDateTime,
                window: OrdersBoardRules.window(of: snapshot, locale: locale),
                area: OrdersBoardRules.areaLine(snapshot),
                band: OrdersBoardRules.bandText(snapshot.homeDistanceBand),
                scope: OrdersBoardRules.scopeLine(snapshot, locale: locale),
                pay: OrdersBoardRules.pay(snapshot),
                live: snapshot.status == ._3 || snapshot.status == ._4,
                now: now
            )
            skeleton(widths: [1, 0.7]).padding(.top, Spacing.s)
            Divider().overlay(CleansiaColors.outlineVariant).padding(.top, Spacing.s)
            skeleton(widths: [1, 0.8, 0.9]).padding(.top, Spacing.s)
            Spacer(minLength: Spacing.s)
            SlideToConfirm(
                idleLabel: L10n.WorkContract.loading,
                busyLabel: L10n.WorkContract.accepting,
                isBusy: false,
                enabled: false,
                maxLabelLines: 2,
                thumbLanes: true,
                onConfirm: {}
            )
        }
        .padding(.horizontal, Spacing.m)
        .padding(.top, Spacing.s)
        .padding(.bottom, Spacing.m)
    }

    private func skeleton(widths: [CGFloat]) -> some View {
        GeometryReader { space in
            VStack(alignment: .leading, spacing: Spacing.s) {
                ForEach(widths.indices, id: \.self) { index in
                    RoundedRectangle(cornerRadius: CornerRadius.extraSmall)
                        .fill(CleansiaColors.surfaceVariant)
                        .frame(width: space.size.width * widths[index], height: 12)
                }
            }
        }
        .frame(height: CGFloat(widths.count) * 24 - 12)
        .accessibilityHidden(true)
    }
}

private struct AcceptanceFacts: View {
    let acceptance: WorkContractAcceptanceFacts
    let renderedLanguage: String?
    let locale: Locale

    /// Named only when the accepted text is not the one on screen, so the reader knows which string
    /// the acceptance binds.
    private var acceptedLanguageName: String? {
        guard let accepted = acceptance.acceptedLanguage, !accepted.isBlank,
              let rendered = renderedLanguage, accepted.caseInsensitiveCompare(rendered) != .orderedSame
        else { return nil }
        return locale.localizedString(forLanguageCode: accepted) ?? accepted
    }

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.xxs) {
            Text(L10n.WorkContract.acceptedOn(
                OrdersFormat.dateTime(acceptance.acceptedOn, locale: locale),
                acceptance.documentVersion
            ))
            if let acceptedLanguageName {
                Text(L10n.WorkContract.acceptedInLanguage(acceptedLanguageName))
            }
        }
        .font(CleansiaTypography.bodyMedium)
        .foregroundColor(CleansiaColors.onSurfaceVariant)
    }
}

struct NoticeRow: View {
    let text: String

    var body: some View {
        HStack(alignment: .top, spacing: Spacing.xs) {
            Image(systemName: "info.circle")
                .foregroundColor(CleansiaColors.warningStar)
            Text(text)
                .font(CleansiaTypography.labelMedium)
                .foregroundColor(CleansiaColors.onSurface)
                .fixedSize(horizontal: false, vertical: true)
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .padding(Spacing.s)
        .background(CleansiaColors.warningStar.opacity(0.12), in: RoundedRectangle(cornerRadius: CornerRadius.small))
    }
}

private struct SheetMessage: View {
    let text: String
    let ctaLabel: String
    let onCta: () -> Void

    var body: some View {
        VStack(spacing: Spacing.m) {
            Text(text)
                .font(CleansiaTypography.bodyLarge)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
                .multilineTextAlignment(.center)
            CleansiaPrimaryButton(ctaLabel, size: .medium, action: onCta)
                .fixedSize()
        }
        .padding(Spacing.xl)
        .frame(maxWidth: .infinity, maxHeight: .infinity)
    }
}
