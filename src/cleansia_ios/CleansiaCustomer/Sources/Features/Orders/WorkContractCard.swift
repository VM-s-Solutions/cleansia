import CleansiaCore
import SwiftUI

/// One line per crew member who accepted the contract for work, each opening the accepted text.
/// Nothing before any acceptance: the crew card already says who is coming, and a "pending" line
/// would tell the customer about a state that is the platform's to chase.
struct WorkContractCard: View {
    @Environment(\.locale) private var locale
    let lines: [WorkContractAcceptanceLine]
    let onRead: (String) -> Void

    var body: some View {
        OrderCardSurface {
            OrderSectionHeaderRow(title: L10n.WorkContract.title)
            ForEach(Array(lines.enumerated()), id: \.element.id) { index, line in
                if index > 0 { Divider().background(CleansiaColors.outlineVariant) }
                AcceptanceRow(line: line, locale: locale, onRead: { onRead(line.id) })
            }
        }
        .id(locale.identifier)
    }
}

private struct AcceptanceRow: View {
    let line: WorkContractAcceptanceLine
    let locale: Locale
    let onRead: () -> Void

    var body: some View {
        HStack(alignment: .top, spacing: Spacing.xs) {
            Image(systemName: "checkmark.seal")
                .font(.system(size: 18))
                .foregroundColor(CleansiaColors.primary)
                .padding(.top, 2)
            VStack(alignment: .leading, spacing: Spacing.xxs) {
                Text(L10n.WorkContract.acceptedLine(
                    line.cleanerName ?? L10n.OrderDetail.cleanerFallback,
                    OrdersFormat.dateTime(line.acceptedOn, locale: locale),
                    line.documentVersion
                ))
                .font(CleansiaTypography.bodyMedium)
                .foregroundColor(CleansiaColors.onSurface)
                .fixedSize(horizontal: false, vertical: true)
                CleansiaTextLink(L10n.WorkContract.read, action: onRead)
            }
        }
    }
}

#if DEBUG
    struct WorkContractCard_Previews: PreviewProvider {
        static var previews: some View {
            WorkContractCard(
                lines: [
                    WorkContractAcceptanceLine(
                        id: "acc-1",
                        cleanerName: "Jana N.",
                        acceptedOn: Date(timeIntervalSince1970: 1_786_200_000),
                        documentVersion: "2026-09-20"
                    ),
                    WorkContractAcceptanceLine(
                        id: "acc-2",
                        cleanerName: nil,
                        acceptedOn: Date(timeIntervalSince1970: 1_786_300_000),
                        documentVersion: "2026-09-20"
                    )
                ],
                onRead: { _ in }
            )
            .padding()
            .background(CleansiaColors.surface)
        }
    }
#endif
