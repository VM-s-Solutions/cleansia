import CleansiaCore
import SwiftUI

/// The contract for work, on one screen with the gesture that accepts it: the job's facts, the text,
/// and the swipe beneath. A take and a standalone acceptance differ only in what the swipe calls; a
/// read shows the stored facts and the accepted version with no swipe at all.
///
/// Built per presentation, so every open re-loads — the facts are the server's word at that moment,
/// never the list row's.
struct WorkContractSheet: View {
    @StateObject private var vm: WorkContractSheetViewModel
    private let onDismiss: () -> Void
    private let onOutcome: (WorkContractOutcome) -> Void

    init(
        request: WorkContractRequest,
        client: PartnerOrderClient,
        onDismiss: @escaping () -> Void,
        onOutcome: @escaping (WorkContractOutcome) -> Void
    ) {
        _vm = StateObject(wrappedValue: WorkContractSheetViewModel(request: request, client: client))
        self.onDismiss = onDismiss
        self.onOutcome = onOutcome
    }

    private var submitting: Bool {
        vm.actionState.isSubmitting
    }

    var body: some View {
        NavigationStack {
            WorkContractSheetContent(
                request: vm.request,
                state: vm.state,
                notice: vm.notice,
                submitting: submitting,
                onAccept: { Task { await vm.accept() } },
                onRetry: { Task { await vm.load() } },
                onClose: onDismiss
            )
            .navigationTitle(vm.state.loadedContract?.title ?? L10n.WorkContract.title)
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .cancellationAction) {
                    Button(L10n.close, action: onDismiss).disabled(submitting)
                }
            }
            .background(CleansiaColors.surface.ignoresSafeArea())
        }
        .interactiveDismissDisabled(submitting)
        .task { await vm.load() }
        .onReceive(vm.outcome) { onOutcome($0) }
    }
}

struct WorkContractSheetContent: View {
    let request: WorkContractRequest
    let state: WorkContractSheetState
    var notice: WorkContractNotice?
    var submitting = false
    var onAccept: () -> Void = {}
    var onRetry: () -> Void = {}
    var onClose: () -> Void = {}

    var body: some View {
        switch state {
        case .loading:
            ProgressView()
                .frame(maxWidth: .infinity, maxHeight: .infinity)
        case .error:
            SheetMessage(text: L10n.WorkContract.loadError, ctaLabel: L10n.retry, onCta: onRetry)
        case let .unavailable(error):
            SheetMessage(text: ApiErrorLocalizer().message(for: error), ctaLabel: L10n.close, onCta: onClose)
        case let .loaded(contract):
            LoadedContract(
                request: request,
                contract: contract,
                notice: notice,
                submitting: submitting,
                onAccept: onAccept,
                onClose: onClose
            )
        }
    }
}

private struct LoadedContract: View {
    @Environment(\.locale) private var locale
    let request: WorkContractRequest
    let contract: WorkContract
    let notice: WorkContractNotice?
    let submitting: Bool
    let onAccept: () -> Void
    let onClose: () -> Void

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.s) {
            Text(L10n.WorkContract.version(contract.version))
                .font(CleansiaTypography.labelMedium)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
            JobFactsCard(facts: contract.facts, locale: locale)
            if let acceptance = contract.acceptance {
                AcceptanceFacts(acceptance: acceptance, renderedLanguage: contract.language, locale: locale)
            }
            if let notice {
                NoticeRow(text: notice.message)
            }
            Divider().overlay(CleansiaColors.outlineVariant)
            HtmlContentView(html: contract.contentHtml)
                .frame(maxWidth: .infinity, maxHeight: .infinity)
            Divider().overlay(CleansiaColors.outlineVariant)
            gesture
        }
        .padding(.horizontal, Spacing.m)
        .padding(.top, Spacing.s)
        .padding(.bottom, Spacing.m)
    }

    @ViewBuilder
    private var gesture: some View {
        switch request {
        case .take, .accept:
            SlideToConfirm(
                idleLabel: L10n.WorkContract.swipeToAccept,
                busyLabel: L10n.WorkContract.accepting,
                isBusy: submitting,
                onConfirm: onAccept
            )
        case .read:
            CleansiaPrimaryButton(L10n.close, action: onClose)
        }
    }
}

private struct JobFactsCard: View {
    let facts: WorkContractJobFacts
    let locale: Locale

    private var window: String {
        OrdersFormat.window(facts.cleaningDateTimeUtc, minutes: facts.estimatedMinutes, locale: locale)
    }

    private var orderNumber: String? {
        guard let number = facts.orderNumber, !number.isBlank else { return nil }
        return number
    }

    private var scopeLine: String? {
        let parts = [
            facts.rooms > 0 ? OrdersFormat.rooms(facts.rooms) : nil,
            facts.bathrooms > 0 ? OrdersFormat.baths(facts.bathrooms) : nil
        ].compactMap { $0 }
        return parts.isEmpty ? nil : parts.joined(separator: " · ")
    }

    private var contentsLine: String? {
        let names = facts.packages + facts.services + facts.extraSlugs.map(OrderExtras.name)
        return names.isEmpty ? nil : names.joined(separator: ", ")
    }

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.xxs) {
            HStack(alignment: .firstTextBaseline) {
                Text(orderNumber.map(L10n.WorkContract.orderNumber) ?? window)
                    .font(CleansiaTypography.titleMedium)
                    .foregroundColor(CleansiaColors.onSurface)
                    .frame(maxWidth: .infinity, alignment: .leading)
                Text(EarningsFormat.wholeMoney(facts.totalPrice, currencyCode: facts.currencyCode))
                    .font(CleansiaTypography.titleMedium)
                    .foregroundColor(CleansiaColors.primary)
            }
            if orderNumber != nil {
                FactLine(window)
            }
            if let location = facts.locationApproximate, !location.isBlank {
                FactLine(location)
            }
            if let scopeLine {
                FactLine(scopeLine)
            }
            if let contentsLine {
                FactLine(contentsLine)
            }
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .padding(Spacing.s)
        .background(CleansiaColors.surfaceVariant.opacity(0.5), in: RoundedRectangle(cornerRadius: CornerRadius.small))
    }
}

private struct FactLine: View {
    let text: String

    init(_ text: String) {
        self.text = text
    }

    var body: some View {
        Text(text)
            .font(CleansiaTypography.bodyMedium)
            .foregroundColor(CleansiaColors.onSurfaceVariant)
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
            FactLine(L10n.WorkContract.acceptedOn(
                OrdersFormat.dateTime(acceptance.acceptedOn, locale: locale),
                acceptance.documentVersion
            ))
            if let acceptedLanguageName {
                FactLine(L10n.WorkContract.acceptedInLanguage(acceptedLanguageName))
            }
        }
    }
}

private struct NoticeRow: View {
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

#if DEBUG
    extension WorkContract {
        static let preview = WorkContract(
            legalDocumentTextId: "text-1",
            version: "2026-09-20",
            language: "en",
            title: "Contract for work",
            contentHtml: "<p>This contract for work is concluded between the customer and the cleaner.</p>",
            facts: WorkContractJobFacts(
                orderNumber: "CL-2026-0042",
                cleaningDateTimeUtc: Date(timeIntervalSince1970: 1_786_200_000),
                estimatedMinutes: 180,
                totalPrice: 1850,
                currencyCode: "CZK",
                locationApproximate: "Praha 4 · 14000",
                rooms: 3,
                bathrooms: 1,
                services: ["Standard cleaning"],
                packages: [],
                extraSlugs: ["inside-oven"]
            ),
            acceptance: nil
        )
    }

    struct WorkContractSheet_Previews: PreviewProvider {
        static var previews: some View {
            Group {
                WorkContractSheetContent(request: .take(orderId: "order-1"), state: .loaded(.preview))
                    .previewDisplayName("Take")
                WorkContractSheetContent(
                    request: .accept(orderId: "order-1"),
                    state: .loaded(.preview),
                    notice: .textUpdated
                )
                .previewDisplayName("Accept · text updated")
                WorkContractSheetContent(request: .read(acceptanceId: "acc-1"), state: .loading)
                    .previewDisplayName("Loading")
                WorkContractSheetContent(
                    request: .take(orderId: "order-1"),
                    state: .unavailable(ApiError(code: WorkContractErrorKey.documentNotFound))
                )
                .previewDisplayName("Unavailable")
            }
            .background(CleansiaColors.surface)
        }
    }
#endif
