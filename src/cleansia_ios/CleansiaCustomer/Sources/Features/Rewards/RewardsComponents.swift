import CleansiaCore
import SwiftUI

struct RewardsActivityRow: View {
    let item: LoyaltyActivityItem

    private var isPositive: Bool {
        item.points >= 0
    }

    private var description: String {
        switch LoyaltyPresentation.transactionKind(item) {
        case let .earnOrder(points, order): L10n.Rewards.txEarnOrder(points, order)
        case let .revokeOrder(points, order): L10n.Rewards.txRevokeOrder(points, order)
        case let .referral(points): L10n.Rewards.txReferral(points)
        case let .manual(points): L10n.Rewards.txManual(points)
        }
    }

    var body: some View {
        HStack(alignment: .center) {
            VStack(alignment: .leading, spacing: Spacing.hair) {
                Text(description)
                    .font(CleansiaTypography.bodyMedium)
                    .foregroundColor(CleansiaColors.onSurface)
                if let occurredOn = item.occurredOn {
                    Text(RewardsFormat.dateTime(occurredOn))
                        .font(CleansiaTypography.labelSmall)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                }
            }
            Spacer()
            Text(verbatim: isPositive ? "+\(item.points)" : "\(item.points)")
                .font(CleansiaTypography.titleMedium)
                .foregroundColor(isPositive ? CleansiaColors.successText : CleansiaColors.error)
        }
        .frame(maxWidth: .infinity, alignment: .leading)
    }
}

struct RewardsStateMessage: View {
    let systemImage: String
    let message: String
    var retry: (() -> Void)?

    var body: some View {
        VStack(spacing: Spacing.s) {
            Image(systemName: systemImage)
                .font(.system(size: 40))
                .foregroundColor(CleansiaColors.onSurfaceVariant)
            Text(message)
                .font(CleansiaTypography.bodyMedium)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
                .multilineTextAlignment(.center)
            if let retry {
                CleansiaOutlinedButton(L10n.Rewards.retry, size: .medium, action: retry)
                    .fixedSize()
            }
        }
        .padding(Spacing.xl)
    }
}

enum RewardsFormat {
    static func dateTime(_ date: Date) -> String {
        formatter.string(from: date)
    }

    private static let formatter: DateFormatter = {
        let formatter = DateFormatter()
        formatter.locale = .current
        formatter.setLocalizedDateFormatFromTemplate("d MMM yyyy HH:mm")
        return formatter
    }()
}
