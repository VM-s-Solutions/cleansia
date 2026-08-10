import CleansiaCore
import CleansiaPartnerApi
import SwiftUI

/// A refusal the cleaner is owed an explanation for, paired with the offer it belongs to.
struct OfferRefusal: Equatable {
    let displayOrderNumber: String?
    let reason: String
}

enum OfferLabels {
    /// The deadline is stated, never counted down: the hold's real expiry lives on the server, so a
    /// remaining-time label on a screen left open drifts into a promise the client cannot keep.
    static func reservedUntil(_ respondByUtc: Date?, now: Date = Date()) -> String {
        guard let deadline = PendingOfferPresentation.respondBy(respondByUtc, now: now) else {
            return L10n.Offers.reservedEnded
        }
        switch deadline.day {
        case .today: return L10n.Offers.reservedUntilToday(deadline.time)
        case .tomorrow: return L10n.Offers.reservedUntilTomorrow(deadline.time)
        case .later: return L10n.Offers.reservedUntilDate(deadline.date, deadline.time)
        case .ended: return L10n.Offers.reservedEnded
        }
    }
}

/// The disclosure that turns a priority into an assignment: this job is held for you, and until when.
struct ReservedForYouRow: View {
    let respondByUtc: Date?
    var now: Date = .init()

    var body: some View {
        HStack(spacing: Spacing.xxs) {
            Image(systemName: "clock")
                .font(.system(size: 14))
                .foregroundColor(CleansiaColors.primary)
            Text(OfferLabels.reservedUntil(respondByUtc, now: now))
                .font(CleansiaTypography.labelLarge)
                .foregroundColor(CleansiaColors.primary)
            Spacer(minLength: 0)
        }
    }
}

/// The platform owns this failure and says so. A reservation spends no capacity, so the take gate can
/// refuse a job the cleaner was told was theirs — the server's own reason is quoted verbatim, wrapped
/// in the sentence that puts the mistake where it belongs. Shared with the order detail so the broken
/// promise reads identically wherever it is met.
struct OfferRefusalDialog: View {
    let refusal: OfferRefusal
    let onDismiss: () -> Void

    private var title: String {
        guard let number = refusal.displayOrderNumber, !number.isBlank else { return L10n.Offers.blockedTitle }
        return "\(L10n.Offers.blockedTitle) · \(number)"
    }

    var body: some View {
        CleansiaDialog(
            title: title,
            confirmLabel: L10n.Offers.blockedDismiss,
            onConfirm: onDismiss,
            onDismiss: onDismiss,
            message: L10n.Offers.blockedBody(refusal.reason),
            icon: "exclamationmark.triangle"
        )
    }
}

/// Refusing the reservation is destructive and irreversible for this cleaner, so it asks first. The
/// copy says what happens to the JOB and never what the customer will be told — one sentence covers a
/// refusal and a silence on their side, and naming them here would make a claim the platform does not.
struct OfferDeclineDialog: View {
    let onConfirm: () -> Void
    let onDismiss: () -> Void

    var body: some View {
        CleansiaDialog(
            title: L10n.Offers.declineTitle,
            confirmLabel: L10n.Offers.declineCta,
            onConfirm: onConfirm,
            onDismiss: onDismiss,
            message: L10n.Offers.declineBody,
            dismissLabel: L10n.cancel,
            destructive: true
        )
    }
}

#if DEBUG
    struct PendingOfferComponents_Previews: PreviewProvider {
        static var previews: some View {
            VStack(alignment: .leading, spacing: Spacing.m) {
                ReservedForYouRow(
                    respondByUtc: Date(timeIntervalSinceNow: 3600),
                    now: Date()
                )
                ReservedForYouRow(
                    respondByUtc: Date(timeIntervalSinceNow: -3600),
                    now: Date()
                )
            }
            .padding()
            .background(CleansiaColors.surface)
            .previewLayout(.sizeThatFits)
        }
    }
#endif
