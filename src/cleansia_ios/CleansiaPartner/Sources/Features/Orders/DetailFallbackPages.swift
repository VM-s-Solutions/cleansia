import CleansiaCore
import CleansiaPartnerApi
import SwiftUI

/// A read that failed in place of what it would show, as a card under the head: what failed, why, Retry.
struct DetailFailureCard: View {
    let title: String
    let message: String?
    var onRetry: (() -> Void)?

    var body: some View {
        VStack(alignment: .leading, spacing: 0) {
            Text(L10n.tie(title))
                .font(CleansiaTypography.titleMedium)
                .foregroundColor(CleansiaColors.onSurface)
                .fixedSize(horizontal: false, vertical: true)
                .accessibilityAddTraits(.isHeader)
            if let message {
                Text(L10n.tie(message))
                    .font(CleansiaTypography.bodyMedium)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
                    .fixedSize(horizontal: false, vertical: true)
                    .padding(.top, Spacing.xxs)
            }
            if let onRetry {
                RetryButton(action: onRetry).padding(.top, Spacing.s)
            }
        }
        .padding(Spacing.m)
        .frame(maxWidth: .infinity, alignment: .leading)
        .background(CleansiaColors.surface, in: RoundedRectangle(cornerRadius: CornerRadius.medium))
        .overlay(
            RoundedRectangle(cornerRadius: CornerRadius.medium).stroke(CleansiaColors.outlineVariant, lineWidth: 1)
        )
        .onAppear { OrdersAccessibility.layoutChanged(title) }
    }
}

/// Opened with no board row and nothing loaded yet: not even whether the job is hers is known, so every value is a
/// flat bar at its real size, the band has no figure and the move's slot holds its height. VoiceOver hears only
/// "Loading…".
struct NoRowSkeleton: View {
    var body: some View {
        JobDetailFrame(mascot: nil) {
            VStack(alignment: .leading, spacing: 0) {
                HStack(spacing: Spacing.xs) {
                    OrdersBar(width: 130, height: 20)
                    Capsule().fill(CleansiaColors.surfaceVariant).frame(width: 84, height: 24)
                }
                OrdersBar(width: 170, height: 18).padding(.top, Spacing.hair)
                HStack {
                    OrdersBar(width: 200, height: 22)
                    Spacer(minLength: 0)
                    OrdersBar(width: 56, height: 14)
                }
                .padding(.top, Spacing.s)
                HStack(spacing: 6) {
                    ForEach(0 ..< JobHeadFacts.stepCount, id: \.self) { _ in
                        Capsule().fill(CleansiaColors.outlineVariant).frame(height: Spacing.xxs)
                    }
                }
                .padding(.top, Spacing.xs)
                HStack(alignment: .top) {
                    VStack(alignment: .leading, spacing: 6) {
                        OrdersBar(width: 150, height: 20)
                        OrdersBar(width: 190, height: 16)
                    }
                    Spacer(minLength: 0)
                    Capsule().fill(CleansiaColors.surfaceVariant).frame(width: 96, height: 34)
                }
                .padding(.top, Spacing.m)
                DetailSection(title: L10n.Orders.runSpoken, icon: "list.bullet") {
                    VStack(alignment: .leading, spacing: Spacing.ml) {
                        ForEach(0 ..< 4, id: \.self) { index in
                            HStack(alignment: .top, spacing: Spacing.s) {
                                Circle().fill(CleansiaColors.surfaceVariant).frame(width: 20, height: 20)
                                VStack(alignment: .leading, spacing: 6) {
                                    HStack {
                                        OrdersBar(width: index == 0 ? 120 : 96, height: 20)
                                        Spacer(minLength: 0)
                                        OrdersBar(width: 56, height: 16)
                                    }
                                    OrdersBar(width: index == 0 ? 230 : 170, height: 16)
                                    if index == 0 { OrdersBar(width: 190, height: 16) }
                                }
                            }
                        }
                    }
                }
            }
            .accessibilityElement(children: .ignore)
            .accessibilityLabel(L10n.Orders.loading)
            .accessibilityAddTraits(.isHeader)
        } pinned: {
            MoveSlotBar()
        }
        .onAppear { OrdersAccessibility.screenChanged(L10n.Orders.loading) }
    }
}

/// The move's slot while nothing says yet what the move is: one flat bar at the slide's height in the pinned area, so
/// the move takes its place without the page shifting. Silent to VoiceOver.
struct MoveSlotBar: View {
    var body: some View {
        Capsule().fill(CleansiaColors.surfaceVariant)
            .frame(height: 56)
            .padding(.horizontal, Spacing.m)
            .padding(.vertical, Spacing.s)
            .background(CleansiaColors.surface.ignoresSafeArea(edges: .bottom))
            .overlay(alignment: .top) { Divider().overlay(CleansiaColors.outlineVariant) }
            .accessibilityHidden(true)
    }
}

/// Opened with no board row and the job failed: the failure is the sheet's head, flat as the removal page's, then what
/// the app still holds — her next jobs and the open jobs of the next seven days, from the board only (a read now
/// would fail too). A job the server no longer has is gone: it says only that, since neither the connection nor Retry
/// could change it.
struct NoRowFailure: View {
    let isGone: Bool
    let now: Date
    let ownRows: [OrderListItem]?
    let openRows: [OrderListItem]?
    var openTotal: Int?
    let onRetry: () -> Void
    var onOpenJob: ((String) -> Void)?
    var onBrowseJobs: (() -> Void)?

    var body: some View {
        JobDetailFrame(mascot: .still(.resting)) {
            DetailFailureHead(
                title: isGone ? L10n.Orders.gone : L10n.Orders.errorJob,
                message: isGone ? nil : L10n.Orders.errorMessage,
                onRetry: isGone ? nil : onRetry
            )
            if let ownRows {
                DetailNextJobs(rows: ownRows.upcoming(now: now, except: nil), now: now, onOpenJob: onOpenJob)
            }
            if let openRows {
                OpenWeek(now: now, open: openRows, own: ownRows ?? [], total: openTotal, onBrowseJobs: onBrowseJobs)
            }
        }
    }
}

/// A failed read with nothing drawn above it: its title and why, wrapping clear of the band's figure, then Retry.
struct DetailFailureHead: View {
    let title: String
    let message: String?
    var onRetry: (() -> Void)?
    @Environment(\.mascotClearance) private var clearance

    var body: some View {
        ClearanceStack(clearance: clearance) {
            Text(title)
                .font(CleansiaTypography.titleMedium)
                .foregroundColor(CleansiaColors.onSurface)
                .fixedSize(horizontal: false, vertical: true)
                .frame(maxWidth: .infinity, alignment: .leading)
                .accessibilityAddTraits(.isHeader)
            if let message {
                Text(L10n.tie(message))
                    .font(CleansiaTypography.bodyMedium)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
                    .fixedSize(horizontal: false, vertical: true)
                    .frame(maxWidth: .infinity, alignment: .leading)
                    .layoutValue(key: RowGap.self, value: Spacing.xxs)
            }
            if let onRetry {
                RetryButton(action: onRetry).layoutValue(key: RowGap.self, value: Spacing.s)
            }
        }
        .onAppear { OrdersAccessibility.layoutChanged(title) }
    }
}

/// Retry, outlined in the primary: the arrow is the word's, so VoiceOver hears the word alone.
struct RetryButton: View {
    let action: () -> Void

    var body: some View {
        CleansiaOutlinedButton(
            L10n.retry,
            size: .medium,
            leadingIcon: "arrow.clockwise",
            contentColor: CleansiaColors.primaryText,
            borderColor: CleansiaColors.primary,
            action: action
        )
        .accessibilityLabel(L10n.retry)
    }
}

/// *You were taken off this job*: the job as the board last held it, the administrator's reason as written (or, with
/// no removal on record, that the job left her schedule), then her next jobs and the open jobs of the next seven
/// days, each from the board or read by the page. A list still being read holds its place as bars; one that failed
/// is left out; with both failed the head ends in *Browse jobs* and *Check again*.
struct RemovalPage: View {
    let page: OrderDetailViewModel.RemovalPage
    let now: Date
    let onCheckAgain: () -> Void
    var onOpenJob: ((String) -> Void)?
    var onBrowseJobs: (() -> Void)?

    private var bothFailed: Bool {
        if case .error = page.nextJobs, case .error = page.openJobs { return true }
        return false
    }

    var body: some View {
        JobDetailFrame(mascot: .still(.resting)) {
            RemovalHead(
                page: page,
                now: now,
                bothFailed: bothFailed,
                onCheckAgain: onCheckAgain,
                onBrowseJobs: onBrowseJobs
            )
            switch page.nextJobs {
            case .loading: OrdersBar(width: nil, height: 140).padding(.top, Spacing.l)
            case let .loaded(rows): DetailNextJobs(
                    rows: rows.upcoming(now: now, except: nil),
                    now: now,
                    onOpenJob: onOpenJob
                )
            case .error: EmptyView()
            }
            switch page.openJobs {
            case .loading: OrdersBar(width: nil, height: 180).padding(.top, Spacing.l)
            case let .loaded(rows):
                OpenWeek(
                    now: now,
                    open: rows,
                    own: page.nextJobs.loadedValue ?? [],
                    total: page.openTotal,
                    onBrowseJobs: onBrowseJobs,
                    onCheckAgain: onCheckAgain
                )
            case .error: EmptyView()
            }
        }
        .onAppear { OrdersAccessibility.screenChanged(RemovalHead.title(page)) }
    }
}

/// The removal page's head: the title and the job line beside the band's figure, then why.
private struct RemovalHead: View {
    @Environment(\.locale) private var locale
    @Environment(\.mascotClearance) private var clearance
    let page: OrderDetailViewModel.RemovalPage
    let now: Date
    let bothFailed: Bool
    let onCheckAgain: () -> Void
    var onBrowseJobs: (() -> Void)?

    static func title(_ page: OrderDetailViewModel.RemovalPage) -> String {
        page.reason == nil ? L10n.Orders.reassignedTitle : L10n.Orders.removalTitle
    }

    var body: some View {
        ClearanceStack(clearance: clearance) {
            Text(Self.title(page))
                .font(CleansiaTypography.titleMedium)
                .foregroundColor(CleansiaColors.onSurface)
                .fixedSize(horizontal: false, vertical: true)
                .frame(maxWidth: .infinity, alignment: .leading)
                .accessibilityAddTraits(.isHeader)
            if let job = page.job, let line = jobLine(job) {
                OrdersDotFlow(items: line.map { Text($0) }, style: CleansiaTypography.scale.bodyLarge)
                    .foregroundColor(CleansiaColors.onSurface)
                    .layoutValue(key: RowGap.self, value: Spacing.xxs)
            }
            if let reason = page.reason {
                VStack(alignment: .leading, spacing: Spacing.xs) {
                    Text(L10n.tie(L10n.Orders.removalLead))
                        .font(CleansiaTypography.bodyMedium)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                        .fixedSize(horizontal: false, vertical: true)
                    CustomerText(text: reason)
                        .padding(.horizontal, Spacing.m)
                        .padding(.vertical, Spacing.s)
                        .frame(maxWidth: .infinity, alignment: .leading)
                        .background(
                            CleansiaColors.surfaceVariant,
                            in: RoundedRectangle(cornerRadius: CornerRadius.medium)
                        )
                }
                .layoutValue(key: RowGap.self, value: Spacing.s)
            } else {
                Text(L10n.tie(L10n.Orders.offSchedule))
                    .font(CleansiaTypography.bodyMedium)
                    .foregroundColor(CleansiaColors.onSurface)
                    .fixedSize(horizontal: false, vertical: true)
                    .frame(maxWidth: .infinity, alignment: .leading)
                    .layoutValue(key: RowGap.self, value: Spacing.s)
            }
            if bothFailed {
                VStack(spacing: Spacing.xs) {
                    if let onBrowseJobs {
                        CleansiaOutlinedButton(
                            L10n.Orders.browseJobs,
                            size: .medium,
                            contentColor: CleansiaColors.primaryText,
                            borderColor: CleansiaColors.primary,
                            action: onBrowseJobs
                        )
                    }
                    CleansiaTextLink(L10n.Orders.checkAgain, leadingIcon: "arrow.clockwise", action: onCheckAgain)
                }
                .layoutValue(key: RowGap.self, value: Spacing.m)
            }
        }
    }

    /// "Tomorrow · 15:00–18:00 · Seifertova 21"; another day by its weekday and date.
    private func jobLine(_ job: OrderListItem) -> [String]? {
        guard let start = job.cleaningDateTime else { return nil }
        let calendar = Calendar.current
        let day: String = if calendar.isDate(start, inSameDayAs: now) {
            L10n.Orders.dayToday
        } else if let tomorrow = calendar.date(byAdding: .day, value: 1, to: now),
                  calendar.isDate(start, inSameDayAs: tomorrow)
        {
            L10n.Orders.dayTomorrow
        } else {
            "\(OrdersFormat.wideWeekday(start, locale: locale)) \(OrdersFormat.dayMonth(start, locale: locale))"
        }
        return [day, OrdersBoardRules.window(of: job, locale: locale), OrdersBoardRules.streetLine(job.customerAddress)]
            .compactMap { $0 }
    }
}

/// *Open jobs, next 7 days*: the week strip with her days filled, the way to the board under the Available badge's
/// count (from the server's total, so the cells may add up to less), and when asked a way to read both lists again.
struct OpenWeek: View {
    let now: Date
    let open: [OrderListItem]
    let own: [OrderListItem]
    var total: Int?
    var onBrowseJobs: (() -> Void)?
    var onCheckAgain: (() -> Void)?

    var body: some View {
        let rows = open.filter { !OrdersBoardRules.isExpired($0, now: now) }
        DetailSection(title: L10n.Orders.openNext7Days, icon: "list.clipboard") {
            OrdersWeekStrip(cells: OrdersBoardRules.weekStrip(now: now, open: rows, own: own))
            if let onBrowseJobs, let total = OrdersBoardRules.openCount(total: total, rows: open, now: now), total > 0 {
                SeeOpenJobsButton(count: total, action: onBrowseJobs)
            }
            if let onCheckAgain {
                CleansiaTextLink(L10n.Orders.checkAgain, leadingIcon: "arrow.clockwise", action: onCheckAgain)
                    .frame(maxWidth: .infinity)
            }
        }
    }
}

/// *See 12 open jobs*: the way to the board, outlined in the primary.
struct SeeOpenJobsButton: View {
    let count: Int
    let action: () -> Void

    var body: some View {
        CleansiaOutlinedButton(
            L10n.Orders.seeOpen(count),
            size: .medium,
            contentColor: CleansiaColors.primaryText,
            borderColor: CleansiaColors.primary,
            action: action
        )
        .accessibilityLabel(L10n.Orders.seeOpen(count))
    }
}
