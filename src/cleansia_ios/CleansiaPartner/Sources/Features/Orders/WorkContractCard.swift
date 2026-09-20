import CleansiaCore
import SwiftUI

/// The cleaner's standing on the contract for work: the acceptance line with a way to read the
/// accepted text, or — for a seat an administrator placed — the prompt to accept before starting.
/// Nothing renders for a cleaner who is not on the crew; the take path carries its own contract.
struct WorkContractCard: View {
    @Environment(\.locale) private var locale
    let standing: WorkContractStanding
    var onAccept: () -> Void = {}
    var onRead: (String) -> Void = { _ in }

    var body: some View {
        switch standing {
        case .none:
            EmptyView()
        case .pending:
            PendingBanner(onAccept: onAccept)
                .id(locale.identifier)
        case let .accepted(acceptanceId, acceptedOn, documentVersion):
            AcceptedLine(
                acceptedOn: OrdersFormat.dateTime(acceptedOn, locale: locale),
                documentVersion: documentVersion,
                onRead: { onRead(acceptanceId) }
            )
            .id(locale.identifier)
        }
    }
}

/// The attention treatment the access card uses: tinted, inset, rounded — this is the one thing on
/// the detail the cleaner has to act on before the work.
private struct PendingBanner: View {
    let onAccept: () -> Void

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.xs) {
            ContractTitle(ink: CleansiaColors.warningStar)
            Text(L10n.WorkContract.pendingBanner)
                .font(CleansiaTypography.bodyMedium)
                .foregroundColor(CleansiaColors.onSurface)
                .fixedSize(horizontal: false, vertical: true)
            CleansiaPrimaryButton(L10n.WorkContract.acceptCta, size: .medium, action: onAccept)
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .padding(Spacing.m)
        .background(CleansiaColors.warningStar.opacity(0.12), in: RoundedRectangle(cornerRadius: CornerRadius.medium))
    }
}

/// Flat like every other section on the sheet (`OrderSectionCard`): a fact, not a prompt.
private struct AcceptedLine: View {
    let acceptedOn: String
    let documentVersion: String
    let onRead: () -> Void

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.xs) {
            ContractTitle(ink: CleansiaColors.onSurface)
            Text(L10n.WorkContract.acceptedLine(acceptedOn, documentVersion))
                .font(CleansiaTypography.bodyMedium)
                .foregroundColor(CleansiaColors.onSurface)
                .fixedSize(horizontal: false, vertical: true)
            CleansiaTextLink(L10n.WorkContract.read, action: onRead)
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .padding(.vertical, Spacing.m)
        .background(CleansiaColors.surface)
    }
}

private struct ContractTitle: View {
    let ink: Color

    var body: some View {
        Label(L10n.WorkContract.title, systemImage: "doc.text")
            .font(CleansiaTypography.titleMedium)
            .foregroundColor(ink)
    }
}

#if DEBUG
    struct WorkContractCard_Previews: PreviewProvider {
        static var previews: some View {
            VStack(spacing: Spacing.m) {
                WorkContractCard(standing: .pending)
                WorkContractCard(standing: .accepted(
                    acceptanceId: "acc-1",
                    acceptedOn: Date(timeIntervalSince1970: 1_786_200_000),
                    documentVersion: "2026-09-20"
                ))
            }
            .padding()
            .background(CleansiaColors.surface)
        }
    }
#endif
