import CleansiaCore
import CleansiaPartnerApi
import SwiftUI

/// *Customer*: who (on a finished job: name and phone, when the details close, Call and SMS; or that they are
/// gone), what they wrote, and how to get in once Getting in is behind her. Each part only with content.
struct CustomerSection: View {
    @Environment(\.locale) private var locale
    let order: OrderDetail
    let part: DetailSectionPlan.Customer
    let now: Date

    var body: some View {
        DetailSection(title: L10n.Orders.customerSectionTitle, icon: "person") {
            VStack(alignment: .leading, spacing: Spacing.m) {
                switch part.who {
                case .contact?:
                    VStack(alignment: .leading, spacing: 0) {
                        CustomerLine(order: order, icon: false)
                        if let close = order.detailsCloseLine(now: now, locale: locale) {
                            Text(L10n.tie(close))
                                .font(CleansiaTypography.labelMedium)
                                .foregroundColor(CleansiaColors.onSurfaceVariant)
                                .fixedSize(horizontal: false, vertical: true)
                                .padding(.top, Spacing.xs)
                        }
                        ContactPills(address: nil, coordinate: nil, phone: order.customerPhone, navigates: false)
                            .padding(.top, Spacing.s)
                    }
                case let .removed(note)?:
                    OrdersCaption(text: note)
                case nil:
                    EmptyView()
                }
                if part.notes {
                    FromCustomerNotes(order: order)
                }
                if part.access {
                    VStack(alignment: .leading, spacing: 6) {
                        DetailEyebrow(text: L10n.Orders.accessSectionTitle)
                        AccessBlock(order: order)
                    }
                }
            }
        }
    }
}

/// *Payment*: only what the head does not say. The cash rule on a cash job, as shipped; where the payouts go once the
/// job is done.
struct PaymentSection: View {
    let payment: DetailSectionPlan.Payment
    var onOpenEarnings: (() -> Void)?

    var body: some View {
        if payment.cashRule != nil || (payment.payouts && onOpenEarnings != nil) {
            DetailSection(title: L10n.Orders.paymentSectionTitle, icon: "creditcard") {
                if let rule = payment.cashRule {
                    Text(L10n.tie(rule))
                        .font(CleansiaTypography.bodyMedium)
                        .foregroundColor(CleansiaColors.onSurface)
                        .fixedSize(horizontal: false, vertical: true)
                }
                if payment.payouts, let onOpenEarnings {
                    PayoutsLink(action: onOpenEarnings).padding(.top, payment.cashRule == nil ? -10 : -Spacing.s)
                }
            }
        }
    }
}

/// *Payouts are in Pay & Earnings ›*, flush with the section's words.
struct PayoutsLink: View {
    let action: () -> Void

    var body: some View {
        CleansiaTextLink(
            L10n.Orders.payoutsLink(L10n.Earnings.title),
            trailingIcon: "chevron.right",
            maxLines: 2,
            action: action
        )
        .padding(.leading, -Spacing.xxs)
        .padding(.bottom, -Spacing.s)
    }
}

/// *Shown after you take it*: what the job keeps until then.
struct ShownAfterTake: View {
    var body: some View {
        DetailSection(title: L10n.Orders.shownAfterTake, icon: "lock") {
            OrdersDotFlow(
                items: [
                    L10n.Orders.hiddenAddress, L10n.Orders.hiddenContact, L10n.Orders.accessSectionTitle,
                    L10n.Orders.hiddenNotes
                ].map { Text($0) },
                style: CleansiaTypography.scale.bodyMedium
            )
            .foregroundColor(CleansiaColors.onSurfaceVariant)
        }
    }
}

/// A job not taken, under its head: what it is and what taking it shows; then what else that day holds — her own
/// jobs and the board's other open rows — or, with no board row for it (a push), her next jobs: the board's rows of
/// that day are older than the push.
struct NotTakenSections: View {
    let order: OrderDetail
    let now: Date
    let ownRows: [OrderListItem]?
    let openRows: [OrderListItem]?
    let clash: OrderListItem?
    var hasBoardRow = true
    var onOpenJob: ((String) -> Void)?

    private var day: Date? {
        order.cleaningDateTime.map { Calendar.current.startOfDay(for: $0) }
    }

    private var isToday: Bool {
        day.map { Calendar.current.isDate($0, inSameDayAs: now) } ?? false
    }

    var body: some View {
        ScopeSection(order: order, board: true, lateJoin: order.status == ._3 || order.status == ._4)
        ShownAfterTake()
        if !hasBoardRow {
            DetailNextJobs(rows: (ownRows ?? []).upcoming(now: now, except: order.id), now: now, onOpenJob: onOpenJob)
        } else {
            let mine = (ownRows ?? []).onDay(day).filter { $0.id != clash?.id }
            if !mine.isEmpty {
                DetailSection(title: isToday ? L10n.Orders.laterToday : L10n.Orders.yourJobThatDay, icon: "calendar") {
                    DetailJobList(rows: mine, now: now, onOpenJob: onOpenJob)
                }
            }
            let also = (openRows ?? []).onDay(day).filter {
                $0.id != order.id && !OrdersBoardRules.isExpired($0, now: now)
            }
            if !also.isEmpty {
                DetailSection(
                    title: isToday ? L10n.Orders.openToday : L10n.Orders.alsoOpenThatDay,
                    icon: "list.clipboard"
                ) {
                    DetailOpenRows(rows: also, own: ownRows ?? [], now: now, onOpenJob: onOpenJob)
                }
            }
        }
    }
}

/// A cancelled job, under its head: what it pays by why it was cancelled and where payouts go, that the customer's
/// details are gone, then what her day still holds and what is open to fill it. *See N open jobs* is the Available
/// badge's count, once the open list has answered.
struct CancelledSections: View {
    let order: OrderDetail
    let now: Date
    let ownRows: [OrderListItem]?
    let openRows: [OrderListItem]?
    var openTotal: Int?
    var onOpenJob: ((String) -> Void)?
    var onBrowseJobs: (() -> Void)?
    var onOpenEarnings: (() -> Void)?

    var body: some View {
        DetailSection(title: L10n.Orders.paymentSectionTitle, icon: "creditcard") {
            Text(L10n.tie(order.cancelledPayLine.text))
                .font(CleansiaTypography.bodyMedium)
                .foregroundColor(CleansiaColors.onSurface)
                .fixedSize(horizontal: false, vertical: true)
            if let onOpenEarnings {
                PayoutsLink(action: onOpenEarnings).padding(.top, -Spacing.s)
            }
        }
        DetailSection(title: L10n.Orders.customerSectionTitle, icon: "person") {
            OrdersCaption(text: L10n.Orders.customerDetailsClosedCancelled)
        }
        let day = Calendar.current.startOfDay(for: now)
        let later = (ownRows ?? []).onDay(day).filter {
            $0.id != order.id && (OrdersBoardRules.bookedEnd($0) ?? .distantPast) > now
        }
        if !later.isEmpty {
            DetailSection(title: L10n.Orders.laterToday, icon: "calendar") {
                DetailJobList(rows: later, now: now, onOpenJob: onOpenJob)
            }
        } else {
            DetailNextJobs(rows: (ownRows ?? []).upcoming(now: now, except: order.id), now: now, onOpenJob: onOpenJob)
        }
        let open = (openRows ?? []).filter { !OrdersBoardRules.isExpired($0, now: now) }
        let today = open.onDay(day)
        if !today.isEmpty {
            DetailSection(title: L10n.Orders.openToday, icon: "list.clipboard") {
                DetailOpenRows(rows: today, own: ownRows ?? [], now: now, onOpenJob: onOpenJob)
            }
        }
        if let openRows, let onBrowseJobs,
           let openTotal = OrdersBoardRules.openCount(total: openTotal, rows: openRows, now: now), openTotal > 0
        {
            SeeOpenJobsButton(count: openTotal, action: onBrowseJobs).padding(.top, Spacing.m)
        }
    }
}

/// "Your next jobs · 3 jobs": her own rows still ahead.
struct DetailNextJobs: View {
    let rows: [OrderListItem]
    let now: Date
    var onOpenJob: ((String) -> Void)?

    var body: some View {
        if !rows.isEmpty {
            DetailSection(title: L10n.Orders.yourNextJobs, icon: "calendar", count: L10n.Orders.jobCount(rows.count)) {
                DetailJobList(rows: rows, now: now, onOpenJob: onOpenJob)
            }
        }
    }
}

/// Her own rows flat in their section, split by rules, as the board draws each one.
struct DetailJobList: View {
    let rows: [OrderListItem]
    let now: Date
    var onOpenJob: ((String) -> Void)?

    var body: some View {
        VStack(spacing: 0) {
            ForEach(Array(rows.enumerated()), id: \.offset) { index, row in
                if index > 0 { Divider().overlay(CleansiaColors.outlineVariant) }
                OrdersJobRow(row: row, now: now, showsFlags: false) { if let id = row.id { onOpenJob?(id) } }
                    .padding(.horizontal, -Spacing.m)
            }
        }
        .padding(.vertical, -Spacing.s)
    }
}

/// The board's own open rows of a day, flat in their section as the board draws them: each opens its job.
struct DetailOpenRows: View {
    let rows: [OrderListItem]
    let own: [OrderListItem]
    let now: Date
    var onOpenJob: ((String) -> Void)?

    var body: some View {
        VStack(spacing: 0) {
            ForEach(Array(rows.enumerated()), id: \.offset) { index, row in
                if index > 0 { Divider().overlay(CleansiaColors.outlineVariant) }
                AvailableJobCard(
                    row: row,
                    now: now,
                    clash: OrdersBoardRules.clashes(row, own: own),
                    onOpen: { if let id = row.id { onOpenJob?(id) } }
                )
                .padding(.horizontal, -Spacing.m)
            }
        }
        .padding(.vertical, -Spacing.s)
    }
}
