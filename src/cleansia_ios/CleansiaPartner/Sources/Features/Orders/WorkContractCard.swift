import CleansiaCore
import SwiftUI

/// The cleaner's standing on the contract for work: under the head, the amber notice to accept it before starting
/// (a seat an administrator placed); among the sections, *Contract for work*: when she accepted which version, and the
/// way to read it. Nothing for a cleaner who is not on the crew; the take path carries its own contract.
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
            HStack(alignment: .firstTextBaseline, spacing: 10) {
                Image(systemName: "exclamationmark.circle")
                    .font(CleansiaTypography.bodyLarge)
                    .capBandAligned(of: CleansiaTypography.scale.bodyMedium)
                    .accessibilityHidden(true)
                VStack(alignment: .leading, spacing: Spacing.xs) {
                    Text(L10n.tie(L10n.WorkContract.pendingBanner))
                        .font(CleansiaTypography.bodyMedium)
                        .fixedSize(horizontal: false, vertical: true)
                    CleansiaPrimaryButton(L10n.WorkContract.acceptCta, size: .small, action: onAccept).fixedSize()
                }
                .frame(maxWidth: .infinity, alignment: .leading)
            }
            .foregroundColor(CleansiaColors.pendingInk)
            .padding(.vertical, Spacing.s)
            .padding(.horizontal, Spacing.m)
            .background(
                CleansiaColors.pendingInk.opacity(0.12),
                in: RoundedRectangle(cornerRadius: CornerRadius.medium)
            )
            .id(locale.identifier)
        case let .accepted(acceptanceId, acceptedOn, documentVersion):
            DetailSection(title: L10n.WorkContract.title, icon: "doc.text") {
                VStack(alignment: .leading, spacing: 0) {
                    OrderInfoRow(
                        label: L10n.Orders.contractAcceptedLabel,
                        value: OrdersFormat.dayMonth(acceptedOn, locale: locale)
                    )
                    .padding(.vertical, 3)
                    OrderInfoRow(label: L10n.Orders.contractVersionLabel, value: documentVersion)
                        .padding(.vertical, 3)
                    CleansiaTextLink(L10n.WorkContract.read, trailingIcon: "chevron.right") { onRead(acceptanceId) }
                        .padding(.leading, -Spacing.xxs)
                        .padding(.bottom, -Spacing.s)
                }
            }
            .id(locale.identifier)
        }
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
