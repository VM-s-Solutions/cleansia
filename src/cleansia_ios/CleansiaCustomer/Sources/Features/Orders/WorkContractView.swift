import CleansiaCore
import SwiftUI

/// The contract for work a cleaner accepted for one seat of the customer's order: the job's facts as
/// they were frozen at the acceptance, the acceptance itself, and the accepted document's text in
/// the customer's language — in-app, because the page is authenticated and a deep link into it from
/// a signed-in app would be a second sign-in for a paragraph of text.
struct WorkContractView: View {
    @StateObject private var vm: WorkContractViewModel

    init(acceptanceId: String, client: OrderClient, snackbar: SnackbarController) {
        _vm = StateObject(wrappedValue: WorkContractViewModel(
            acceptanceId: acceptanceId,
            client: client,
            snackbar: snackbar
        ))
    }

    var body: some View {
        WorkContractContent(state: vm.state, onRetry: { Task { await vm.load() } })
            .navigationTitle(L10n.WorkContract.title)
            .navigationBarTitleDisplayMode(.inline)
            .background(CleansiaColors.background.ignoresSafeArea())
            .task { await vm.load() }
    }
}

struct WorkContractContent: View {
    let state: UiState<WorkContract>
    var onRetry: () -> Void = {}

    var body: some View {
        switch state {
        case .loading:
            ProgressView()
                .tint(CleansiaColors.primary)
                .frame(maxWidth: .infinity, maxHeight: .infinity)
        case .error:
            VStack(spacing: Spacing.m) {
                Image(systemName: "wifi.slash")
                    .font(.system(size: 40))
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
                Text(L10n.WorkContract.loadError)
                    .font(CleansiaTypography.bodyMedium)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
                    .multilineTextAlignment(.center)
                CleansiaOutlinedButton(L10n.Orders.errorRetry, size: .medium, action: onRetry)
                    .fixedSize()
            }
            .padding(Spacing.xl)
            .frame(maxWidth: .infinity, maxHeight: .infinity)
        case let .loaded(contract):
            LoadedContract(contract: contract)
        }
    }
}

private struct LoadedContract: View {
    @Environment(\.locale) private var locale
    let contract: WorkContract

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.xs) {
            Text(contract.title ?? L10n.WorkContract.title)
                .font(CleansiaTypography.titleLarge)
                .foregroundColor(CleansiaColors.onBackground)
            Text(L10n.WorkContract.version(contract.version))
                .font(CleansiaTypography.labelMedium)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
            JobFacts(facts: contract.facts, locale: locale)
                .padding(.top, Spacing.xxs)
            if let acceptance = contract.acceptance {
                AcceptanceFacts(acceptance: acceptance, renderedLanguage: contract.language, locale: locale)
            }
            Divider().overlay(CleansiaColors.outlineVariant)
            HtmlContentView(html: contract.contentHtml)
                .frame(maxWidth: .infinity, maxHeight: .infinity)
        }
        .padding(.horizontal, Spacing.m)
        .padding(.top, Spacing.s)
        .id(locale.identifier)
    }
}

/// The job as it was frozen on the acceptance row — never the live order, which moves on afterwards.
private struct JobFacts: View {
    let facts: WorkContractJobFacts
    let locale: Locale

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.xxs) {
            Text(L10n.WorkContract.factsTitle)
                .font(CleansiaTypography.labelLarge)
                .foregroundColor(CleansiaColors.onSurface)
            if let number = facts.orderNumber, !number.isBlank {
                FactRow(label: L10n.WorkContract.orderNumber, value: number)
            }
            FactRow(
                label: L10n.WorkContract.window,
                value: OrdersFormat.dateRange(
                    facts.cleaningDateTimeUtc,
                    estimatedMinutes: facts.estimatedMinutes,
                    locale: locale
                )
            )
            FactRow(
                label: L10n.WorkContract.price,
                value: OrdersFormat.price(facts.totalPrice, currencyCode: facts.currencyCode)
            )
            if let location = facts.locationApproximate, !location.isBlank {
                FactRow(label: L10n.WorkContract.location, value: location)
            }
            FactRow(
                label: L10n.OrderDetail.rooms,
                value: L10n.OrderDetail.roomsBathrooms(facts.rooms, facts.bathrooms)
            )
            if !facts.services.isEmpty {
                FactRow(label: L10n.OrderDetail.servicesHeader, value: facts.services.joined(separator: ", "))
            }
            if !facts.packages.isEmpty {
                FactRow(label: L10n.OrderDetail.packagesHeader, value: facts.packages.joined(separator: ", "))
            }
            if !facts.extraSlugs.isEmpty {
                FactRow(
                    label: L10n.OrderDetail.extras,
                    value: facts.extraSlugs.map(OrdersFormat.prettifyExtraKey).joined(separator: ", ")
                )
            }
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .padding(Spacing.s)
        .background(CleansiaColors.surfaceVariant.opacity(0.5), in: RoundedRectangle(cornerRadius: CornerRadius.medium))
    }
}

private struct FactRow: View {
    let label: String
    let value: String

    var body: some View {
        HStack(alignment: .top, spacing: Spacing.xs) {
            Text(label)
                .font(CleansiaTypography.bodyMedium)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
                .frame(maxWidth: .infinity, alignment: .leading)
            Text(value)
                .font(CleansiaTypography.labelLarge)
                .foregroundColor(CleansiaColors.onSurface)
                .frame(maxWidth: .infinity, alignment: .leading)
                .layoutPriority(1)
        }
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
            .font(CleansiaTypography.bodyMedium)
            .foregroundColor(CleansiaColors.onSurfaceVariant)
            if let acceptedLanguageName {
                Text(L10n.WorkContract.acceptedInLanguage(acceptedLanguageName))
                    .font(CleansiaTypography.bodyMedium)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
            }
        }
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
            acceptance: WorkContractAcceptanceFacts(
                acceptedOn: Date(timeIntervalSince1970: 1_786_000_000),
                documentVersion: "2026-09-20",
                acceptedLanguage: "cs"
            )
        )
    }

    struct WorkContractView_Previews: PreviewProvider {
        static var previews: some View {
            Group {
                WorkContractContent(state: .loaded(.preview))
                    .previewDisplayName("Loaded")
                WorkContractContent(state: .loading)
                    .previewDisplayName("Loading")
                WorkContractContent(state: .error(ApiError(code: "order.not_found")))
                    .previewDisplayName("Error")
            }
            .background(CleansiaColors.background)
        }
    }
#endif
