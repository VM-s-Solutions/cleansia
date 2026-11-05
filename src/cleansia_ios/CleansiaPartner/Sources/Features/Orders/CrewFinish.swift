import CleansiaCore
import SwiftUI

/// Where the cleaners on a job stand on finishing it (owner ruling 2026-10-26): with two or more on it, each
/// marks their own part done, and once every one still on it has, any of them completes it.
enum CrewFinish: Equatable {
    /// One cleaner on the job: their Complete closes it, as it always has.
    case solo
    /// The caller has not marked their part done.
    case myPartOpen
    /// The caller has; someone else on the job has not.
    case waitingForCrew
    /// Everyone on the job has marked their part done.
    case everyoneDone
}

extension OrderDetail {
    /// Read off the seats the server sends the crew. Until the caller's own id is known their seat cannot be
    /// found, and marking again keeps the first time, so an unfinished crew then reads as the caller's part
    /// still open.
    func crewFinish(myEmployeeId: String?) -> CrewFinish {
        guard seats.count >= 2 else { return .solo }
        if seats.allSatisfy({ $0.completionConfirmedAt != nil }) { return .everyoneDone }
        let mine = seats.first { $0.employeeId == myEmployeeId }
        return mine?.completionConfirmedAt == nil ? .myPartOpen : .waitingForCrew
    }

    /// Someone else on the job has marked their part done.
    func mateMarked(_ myEmployeeId: String?) -> Bool {
        mates(myEmployeeId).contains { $0.completionConfirmedAt != nil }
    }

    /// A crew-mate's phone, for Call alone: only on her own job in progress, and only while the mate's part is open.
    func callPhone(for mate: OrderSeat) -> String? {
        guard status == ._4, isAssignedToCurrentUser, mate.completionConfirmedAt == nil else { return nil }
        return mate.phone
    }
}

/// *Before you can complete*: each cleaner on the job by given name with their own mark, her crew-mate in green
/// and her in sky (identity only, never a status), then the after photo. The rows say who and where each stands; a
/// call to the crew-mate is the pinned move's. The line under it says what completing waits for. Once every part is
/// marked, a card Stripe has not charged says so under the after photo, where the locked payment slide is pinned.
struct CrewGate: View {
    let order: OrderDetail
    let myEmployeeId: String?
    let afterPhoto: Date?

    var body: some View {
        let allMarked = !order.seats.isEmpty && order.seats.allSatisfy { $0.completionConfirmedAt != nil }
        let unpaid = allMarked && order.payment.isCardNotCharged && (afterPhoto != nil || order.hasAfterPhotos)
        VStack(alignment: .leading, spacing: Spacing.xs) {
            DetailEyebrow(text: L10n.Orders.finishBeforeComplete)
            VStack(alignment: .leading, spacing: 0) {
                ForEach(order.mates(myEmployeeId), id: \.id) { mate in
                    GateRow(
                        title: mate.givenName ?? "—",
                        done: mate.completionConfirmedAt != nil,
                        stamp: mate.completionConfirmedAt,
                        avatar: GateRow.Avatar(initials: mate.initials, isMate: true)
                    )
                }
                GateRow(
                    title: L10n.Orders.crewYou,
                    done: order.ownSeat(myEmployeeId)?.completionConfirmedAt != nil,
                    stamp: order.ownSeat(myEmployeeId)?.completionConfirmedAt,
                    avatar: GateRow.Avatar(initials: order.ownSeat(myEmployeeId)?.initials ?? "", isMate: false)
                )
                if let afterPhoto {
                    GateRow(title: L10n.Orders.finishAfterPhoto, done: true, stamp: afterPhoto)
                }
                if unpaid {
                    OrdersCaption(text: L10n.Orders.paymentNotConfirmed)
                        .frame(maxWidth: .infinity, alignment: .leading)
                        .padding(.leading, 48)
                }
            }
            OrdersCaption(text: allMarked ? L10n.Orders.crewAllMarked : L10n.Orders.crewBothParts)
        }
    }
}

/// One row of a gate list: a person's avatar or a point, what it is, whether it is done and when.
struct GateRow: View {
    struct Avatar {
        let initials: String
        let isMate: Bool
    }

    @Environment(\.locale) private var locale
    let title: String
    let done: Bool
    var stamp: Date?
    var avatar: Avatar?

    var body: some View {
        HStack(alignment: .center, spacing: Spacing.s) {
            marker.frame(width: 36)
            VStack(alignment: .leading, spacing: 0) {
                Text(title)
                    .font(CleansiaTypography.bodyLarge)
                    .foregroundColor(CleansiaColors.onSurface)
                    .fixedSize(horizontal: false, vertical: true)
                sub
            }
            .frame(maxWidth: .infinity, alignment: .leading)
        }
        .frame(minHeight: 44)
        .padding(.vertical, Spacing.xxs)
        .accessibilityElement(children: .combine)
    }

    @ViewBuilder
    private var sub: some View {
        let time = stamp.map { OrdersFormat.timeOnly($0, locale: locale) }
        if avatar != nil {
            HStack(alignment: .firstTextBaseline, spacing: Spacing.xxs) {
                if done {
                    Image(systemName: "checkmark")
                        .font(CleansiaTypography.labelMedium)
                        .foregroundColor(CleansiaColors.primaryText)
                        .capBandAligned(of: CleansiaTypography.scale.bodyMedium)
                        .accessibilityHidden(true)
                }
                Text(done ? time.map(L10n.Orders.crewDoneAt) ?? L10n.Orders.crewDoneAt("") : L10n.Orders.crewNotYet)
                    .font(CleansiaTypography.bodyMedium)
                    .monospacedDigit()
            }
            .foregroundColor(CleansiaColors.onSurfaceVariant)
        } else if let time {
            Text(time)
                .font(CleansiaTypography.bodyMedium)
                .monospacedDigit()
                .foregroundColor(CleansiaColors.onSurfaceVariant)
        }
    }

    @ViewBuilder
    private var marker: some View {
        if let avatar {
            Text(avatar.initials)
                .font(CleansiaTypography.labelLarge)
                .foregroundColor(avatar.isMate ? CleansiaColors.successInk : CleansiaColors.onPrimaryContainer)
                .frame(width: 36, height: 36)
                .background(
                    avatar.isMate ? CleansiaColors.crewMateContainer : CleansiaColors.primaryContainer,
                    in: Circle()
                )
                .dynamicTypeSize(...DynamicTypeSize.xxxLarge)
                .accessibilityHidden(true)
        } else {
            OrderTimelinePoint(segment: done ? .past : .future)
        }
    }
}
