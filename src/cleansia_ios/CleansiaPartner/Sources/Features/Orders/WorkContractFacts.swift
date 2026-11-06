import CleansiaCore
import CleansiaPartnerApi
import SwiftUI

/// Date, place and what, then the reward. From the board the services carry the row's translated names, and a
/// reward that moved since the card was drawn says so.
struct TakeFactsCard: View {
    @Environment(\.locale) private var locale
    let contract: WorkContract
    let snapshot: OrderListItem?
    let orderLine: String?
    let now: Date

    private var facts: WorkContractJobFacts {
        contract.facts
    }

    var body: some View {
        FactsCardLayout(
            orderLine: orderLine,
            date: facts.cleaningDateTimeUtc,
            window: OrdersFormat.timeWindow(facts.cleaningDateTimeUtc, minutes: facts.estimatedMinutes, locale: locale),
            area: facts.locationApproximate,
            band: OrdersBoardRules.bandText(snapshot?.homeDistanceBand),
            scope: scope,
            pay: OrdersFormat.money(facts.reward, symbol: EarningsFormat.currencySymbol(facts.currencyCode)),
            previousPay: previousPay,
            live: snapshot?.status == ._3 || snapshot?.status == ._4,
            now: now
        )
    }

    private var previousPay: String? {
        guard let shown = snapshot?.estimatedCleanerPay, abs(shown - facts.reward) >= 0.005 else { return nil }
        return OrdersFormat.money(shown, symbol: EarningsFormat.currencySymbol(facts.currencyCode))
    }

    private var scope: String? {
        let lang = locale.language.languageCode?.identifier
        let packages = translated(facts.packages, ids: facts.packageIds, lang: lang) { id in
            snapshot?.selectedPackages?.first { $0.id == id }?.translations
        }
        let services = translated(facts.services, ids: facts.serviceIds, lang: lang) { id in
            snapshot?.selectedServices?.first { $0.id == id }?.translations
        }
        let names = (packages + services).joined(separator: ", ")
        let parts = [
            names.isEmpty ? nil : names,
            facts.rooms > 0 ? OrdersFormat.rooms(facts.rooms) : nil,
            facts.bathrooms > 0 ? OrdersFormat.baths(facts.bathrooms) : nil,
            OrdersBoardRules.extrasLine(facts.extraSlugs)
        ].compactMap { $0 }
        return parts.isEmpty ? nil : parts.joined(separator: OrdersBoardRules.dot)
    }

    private func translated(
        _ names: [String],
        ids: [String],
        lang: String?,
        translations: (String) -> [String: Translation]?
    ) -> [String] {
        guard ids.count == names.count else { return names }
        return zip(ids, names).map { id, name in
            ScopeTranslation.resolveTranslatedName(translations(id), lang: lang, fallback: name) ?? name
        }
    }
}

/// The facts in a well, in the head's slots: how far off the day is, the calendar line, the area and how far from
/// home; then what, and the reward as the block's one total row.
struct FactsCardLayout: View {
    var orderLine: String?
    let date: Date?
    let window: String?
    let area: String?
    var band: String?
    let scope: String?
    let pay: String?
    var previousPay: String?
    var live = false
    var now = Date()

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            if let orderLine {
                Text(orderLine)
                    .font(CleansiaTypography.labelLarge)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
                    .padding(.bottom, Spacing.xs)
            }
            TakeFacts(date: date, window: window, place: area, detail: band, live: live, now: now) {
                if let scope {
                    OrdersDotFlow(
                        items: scope.components(separatedBy: OrdersBoardRules.dot).map { Text($0) },
                        tiesLast: true
                    )
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
                    .padding(.top, Spacing.s)
                }
                Divider().overlay(CleansiaColors.outlineVariant).padding(.top, Spacing.s)
                HStack(alignment: .firstTextBaseline) {
                    Text(L10n.WorkContract.reward)
                        .font(CleansiaTypography.bodyLarge)
                        .foregroundColor(CleansiaColors.onSurface)
                    Spacer(minLength: Spacing.xs)
                    if let pay {
                        Text(pay)
                            .font(CleansiaTypography.titleMedium)
                            .monospacedDigit()
                            .foregroundColor(CleansiaColors.onSurface)
                            .lineLimit(1)
                            .fixedSize()
                    }
                }
                .padding(.top, Spacing.s)
                if let previousPay {
                    RuleText(text: L10n.WorkContract.payUpdated(previousPay))
                }
            }
        }
    }
}

/// The four plain lines (ruling 4), true to versions 2026-10-05 and 2026-10-27 only — the caller gates them. Each
/// bullet sits on its line's x-height band.
struct ContractSummary: View {
    var body: some View {
        VStack(alignment: .leading, spacing: 6) {
            Text(L10n.WorkContract.summaryLead)
                .font(CleansiaTypography.titleMedium)
                .foregroundColor(CleansiaColors.onSurface)
                .fixedSize(horizontal: false, vertical: true)
            ForEach(lines, id: \.self) { line in
                HStack(alignment: .firstTextBaseline, spacing: 7) {
                    Circle()
                        .fill(CleansiaColors.primaryText)
                        .frame(width: 6, height: 6)
                        .xHeightAligned(of: CleansiaTypography.scale.bodyMedium)
                        .padding(.leading, 3)
                    Text(L10n.tie(line))
                        .font(CleansiaTypography.bodyMedium)
                        .fixedSize(horizontal: false, vertical: true)
                }
                .foregroundColor(CleansiaColors.onSurface)
            }
        }
        .accessibilityElement(children: .combine)
    }

    private var lines: [String] {
        [
            L10n.WorkContract.summaryLockout,
            L10n.WorkContract.summaryLateCancel,
            L10n.WorkContract.summaryComplaint,
            L10n.WorkContract.summaryEquipment
        ]
    }
}

/// The board's rules for this job, from the card's row: when it can be cancelled, that a team does not split
/// the work, and whose the cash is.
struct BoardRules: View {
    @Environment(\.locale) private var locale
    let snapshot: OrderListItem
    let contract: WorkContract

    var body: some View {
        let cancel = OrdersBoardRules.cancelLine(
            status: snapshot.status,
            start: snapshot.cleaningDateTime,
            now: Date(),
            version: contract.version
        )
        if let text = OrdersBoardRules.cancelText(cancel, phase: .beforeTake, locale: locale) {
            RuleText(text: text)
        }
        if let required = snapshot.requiredEmployees, required > 1 {
            RuleText(text: L10n.WorkContract.teamLine(L10n.Orders.crewSize(required)))
        }
        if WorkContract.releaseVersions.contains(contract.version), let cash = OrdersBoardRules.cashAmount(snapshot) {
            let reward = OrdersFormat.money(
                contract.facts.reward,
                symbol: EarningsFormat.currencySymbol(contract.facts.currencyCode)
            )
            RuleText(text: L10n.WorkContract.cashLine(cash, reward))
        }
    }
}

struct RuleText: View {
    let text: String

    var body: some View {
        Text(L10n.tie(text))
            .font(CleansiaTypography.bodyMedium)
            .foregroundColor(CleansiaColors.onSurface)
            .fixedSize(horizontal: false, vertical: true)
            .frame(maxWidth: .infinity, alignment: .leading)
            .padding(.top, Spacing.s)
    }
}
