import CleansiaCore
import CleansiaPartnerApi
import SwiftUI

/// The job that matters now as the hero, the rest under "Later". With one job the tab couples to the board: the
/// open jobs of the next seven days and the soonest one to take. With none, the standing head, the offer waiting
/// for her, and the week.
struct ActivePane: View {
    @ObservedObject var vm: OrdersListViewModel
    @Environment(\.locale) private var locale
    @Environment(\.dynamicTypeSize) private var typeSize

    var body: some View {
        content
            .padding(.horizontal, Spacing.m)
            .padding(.top, Spacing.xs)
            .padding(.bottom, Spacing.l)
            .ordersScroll(onRefresh: vm.userRefresh)
    }

    @ViewBuilder
    private var content: some View {
        switch vm.state(of: .active) {
        case .loading:
            ActiveSkeleton()
        case .error:
            failed
        case let .loaded(rows):
            if rows.isEmpty {
                empty
            } else {
                populated
            }
        }
    }

    private var populated: some View {
        let now = vm.now()
        let later = vm.laterRows
        return VStack(alignment: .leading, spacing: Spacing.s) {
            if let stale = staleText {
                OrdersStaleStrip(text: stale) { Task { await vm.userRefresh() } }
            }
            if let hero = vm.hero {
                ActiveHero(
                    row: hero,
                    now: now,
                    run: vm.heroRun,
                    isBusy: vm.inFlightActionOrderId == hero.id,
                    onOpen: { open(hero) },
                    onAdvance: { Task { await vm.advance(hero) } }
                )
            }
            if later.isEmpty {
                week(title: L10n.Orders.nothingElse, subtitle: L10n.Orders.openNext7Days, checkAgain: false)
                if let soonest = vm.soonestOpen {
                    OrdersSectionHeader(title: L10n.Orders.soonestOpen)
                    AvailableJobCard(row: soonest, now: now, onOpen: { open(soonest) })
                        .ordersCard()
                }
            } else {
                OrdersSectionHeader(title: L10n.Orders.dayLater, trailing: L10n.Orders.jobCount(later.count))
                OrdersRowList(rows: later, now: now, onOpen: open)
            }
        }
    }

    /// Active failed with no rows held: the error with Retry, the open jobs of the next seven days the board still
    /// has (Retry is the way on, so no Check again), then the cash she holds, as the compact notice over the cap.
    private var failed: some View {
        VStack(alignment: .leading, spacing: Spacing.s) {
            OrdersShortPanel(vm: vm, failed: true)
            if case .loaded = vm.state(of: .available) {
                week(title: L10n.Orders.openNext7Days, subtitle: nil, checkAgain: false)
            }
            if let cash = vm.cashPanel {
                if cash.over, let limit = cash.limit {
                    OrdersCashNotice(held: cash.held, limit: limit, ratio: cash.ratio)
                } else {
                    OrdersCashPanel(held: cash.held, limit: cash.limit, ratio: cash.ratio)
                }
            }
        }
    }

    /// No own job: the standing head with its mascot, the offer waiting for her, and the week to take one from.
    private var empty: some View {
        VStack(alignment: .leading, spacing: Spacing.s) {
            HStack(alignment: .bottom, spacing: Spacing.s) {
                VStack(alignment: .leading, spacing: Spacing.xxs) {
                    Text(L10n.Orders.noActiveOrders)
                        .font(CleansiaTypography.titleMedium)
                        .foregroundColor(CleansiaColors.onSurface)
                        .accessibilityAddTraits(.isHeader)
                    Text(L10n.tie(L10n.Orders.activeEmptyBody))
                        .font(CleansiaTypography.bodyMedium)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                        .fixedSize(horizontal: false, vertical: true)
                }
                .padding(.bottom, Spacing.l)
                .frame(maxWidth: .infinity, alignment: .leading)
                if !typeSize.isAccessibilitySize {
                    Mascot.ready.image
                        .resizable()
                        .scaledToFit()
                        .frame(width: 156, height: 156)
                        .accessibilityHidden(true)
                }
            }
            .padding(.bottom, -Spacing.m)
            .zIndex(1)
            if let offer = vm.reserved.first {
                offerLink(offer.offer, row: offer.row)
            }
            week(title: L10n.Orders.openNext7Days, subtitle: nil, checkAgain: true)
        }
    }

    private func offerLink(_ offer: PendingOfferItem, row: OrderListItem) -> some View {
        Button { Task { await vm.selectTab(.available) } } label: {
            VStack(alignment: .leading, spacing: 0) {
                offerHead(offer, row: row)
                offerJob(row).padding(.top, Spacing.s)
            }
            .padding(Spacing.m)
            .ordersCard(edge: CleansiaColors.primary, width: 1.5)
            .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
        .accessibilityElement(children: .combine)
    }

    private func offerHead(_ offer: PendingOfferItem, row: OrderListItem) -> some View {
        let now = vm.now()
        return HStack(alignment: .top, spacing: Spacing.s) {
            if let start = row.cleaningDateTime {
                OrdersDateTile(date: start, size: .header, now: now)
            }
            VStack(alignment: .leading, spacing: Spacing.hair) {
                Text(L10n.Offers.cardTitle)
                    .font(CleansiaTypography.titleMedium)
                    .foregroundColor(CleansiaColors.onSurface)
                    .fixedSize(horizontal: false, vertical: true)
                OrdersDotFlow(
                    items: [
                        Text(OfferLabels.reservedUntil(offer.respondByUtc, now: now))
                            .foregroundColor(CleansiaColors.primaryText),
                        Text(L10n.Orders.reservedThen).foregroundColor(CleansiaColors.onSurfaceVariant)
                    ]
                )
            }
        }
    }

    /// The offered job as the board's row draws it, with See offer on the tile beside it at the tile's one-line width
    /// while the job keeps room for each of its runs whole; else, and at every accessibility size, a full-width bar
    /// under it.
    @ViewBuilder
    private func offerJob(_ row: OrderListItem) -> some View {
        let pay = OrdersBoardRules.pay(row)
        let under = VStack(alignment: .leading, spacing: Spacing.s) {
            offerWhen(row)
            offerBar(pay)
        }
        if typeSize.isAccessibilitySize {
            under
        } else {
            ViewThatFits(in: .horizontal) {
                HStack(alignment: .top, spacing: Spacing.s) {
                    ZStack(alignment: .topLeading) {
                        offerRuns(row)
                        offerWhen(row).frame(minWidth: 0, idealWidth: 0, maxWidth: .infinity, alignment: .leading)
                    }
                    offerTile(pay)
                }
                under
            }
        }
    }

    /// Each run of the job at its whole width, unseen: the narrowest the job's column may be.
    private func offerRuns(_ row: OrderListItem) -> some View {
        let wide = CleansiaTypography.scale.bodyLarge
        let narrow = CleansiaTypography.scale.bodyMedium
        return ZStack(alignment: .topLeading) {
            if let start = row.cleaningDateTime {
                Text(OrdersFormat.wideWeekday(start, locale: locale)).cleansiaFont(wide).fixedSize()
            }
            if let window = OrdersBoardRules.window(of: row, locale: locale) {
                Text(window).cleansiaFont(wide).monospacedDigit().fixedSize()
            }
            if let area = OrdersBoardRules.areaLine(row) {
                Text(area).cleansiaFont(narrow).fixedSize()
            }
        }
        .frame(height: 0, alignment: .top)
        .hidden()
        .accessibilityHidden(true)
    }

    private func offerWhen(_ row: OrderListItem) -> some View {
        VStack(alignment: .leading, spacing: Spacing.hair) {
            OrdersDotFlow(
                items: [
                    row.cleaningDateTime.map { Text(OrdersFormat.wideWeekday($0, locale: locale)) },
                    OrdersBoardRules.window(of: row, locale: locale).map {
                        Text(OrdersBoardRules.unbreakable($0)).monospacedDigit()
                    }
                ].compactMap { $0 },
                style: CleansiaTypography.scale.bodyLarge
            )
            .foregroundColor(CleansiaColors.onSurface)
            OrdersDotFlow(
                items: [
                    OrdersBoardRules.areaLine(row).map { Text($0).foregroundColor(CleansiaColors.onSurface) },
                    OrdersBoardRules.bandText(row.homeDistanceBand).map {
                        Text(OrdersBoardRules.unbreakable($0)).foregroundColor(CleansiaColors.onSurfaceVariant)
                    }
                ].compactMap { $0 }
            )
        }
        .frame(maxWidth: .infinity, alignment: .leading)
    }

    private func offerTile(_ pay: String?) -> some View {
        VStack(spacing: 0) {
            if let pay { offerPay(pay) }
            OrdersTileMove(label: L10n.Orders.seeOffer)
        }
        .padding(.vertical, Spacing.xs)
        .padding(.horizontal, Spacing.s)
        .frame(minWidth: 108)
        .background(CleansiaColors.primary.opacity(0.12), in: RoundedRectangle(cornerRadius: CornerRadius.small))
        .fixedSize(horizontal: true, vertical: false)
    }

    /// The pay leads and See offer trails, on one line while they fit; else See offer takes the next line, wrapping
    /// there only if it must.
    private func offerBar(_ pay: String?) -> some View {
        ViewThatFits(in: .horizontal) {
            HStack(alignment: .center, spacing: Spacing.xs) {
                if let pay { offerPay(pay) }
                Spacer(minLength: Spacing.xs)
                OrdersTileMove(label: L10n.Orders.seeOffer)
            }
            VStack(alignment: .leading, spacing: 0) {
                if let pay { offerPay(pay) }
                HStack(spacing: 0) {
                    Spacer(minLength: 0)
                    OrdersTileMove(label: L10n.Orders.seeOffer, wraps: true)
                }
            }
        }
        .padding(.vertical, 10)
        .padding(.horizontal, Spacing.m)
        .frame(maxWidth: .infinity, alignment: .leading)
        .background(CleansiaColors.primary.opacity(0.12), in: RoundedRectangle(cornerRadius: CornerRadius.small))
    }

    private func offerPay(_ pay: String) -> some View {
        Text(pay)
            .font(CleansiaTypography.titleMedium)
            .monospacedDigit()
            .foregroundColor(CleansiaColors.onSurface)
            .lineLimit(1)
            .fixedSize()
    }

    private func week(title: String, subtitle: String?, checkAgain: Bool) -> some View {
        VStack(alignment: .leading, spacing: 0) {
            Text(title)
                .font(CleansiaTypography.titleMedium)
                .foregroundColor(CleansiaColors.onSurface)
                .accessibilityAddTraits(.isHeader)
            if let subtitle {
                Text(subtitle)
                    .font(CleansiaTypography.bodyMedium)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
                    .padding(.top, Spacing.hair)
            }
            OrdersWeekStrip(cells: vm.weekStrip)
                .padding(.top, Spacing.s)
                .accessibilityElement(children: .contain)
                .accessibilityLabel(L10n.Orders.openNext7Days)
            if let open = vm.badgeCount(.available), open > 0 {
                CleansiaOutlinedButton(
                    L10n.Orders.seeOpen(open),
                    size: .medium,
                    contentColor: CleansiaColors.primaryText,
                    borderColor: CleansiaColors.primary,
                    maxLines: 4
                ) { Task { await vm.selectTab(.available) } }
                    .padding(.top, Spacing.s)
            }
            if checkAgain {
                CleansiaTextLink(L10n.Orders.checkAgain, leadingIcon: "arrow.clockwise", maxLines: 3) {
                    Task { await vm.userRefresh() }
                }
                .frame(maxWidth: .infinity)
                .padding(.top, Spacing.xxs)
            }
        }
        .padding(Spacing.m)
        .frame(maxWidth: .infinity, alignment: .leading)
        .ordersCard()
    }

    private var staleText: String? {
        let state = vm.pane(.active)
        guard state.error != nil, let loadedAt = state.loadedAt else { return nil }
        return L10n.Orders.staleSince(OrdersBoardRules.staleWhen(loadedAt, now: vm.now(), locale: locale))
    }

    private func open(_ row: OrderListItem) {
        guard let id = row.id else { return }
        vm.openDetail(id)
    }
}

/// Active while its first load runs: the hero's head, day line, scope and slide, then the Later rows, as flat bars at
/// their real size, and none of Available's controls. VoiceOver hears only "Loading…".
private struct ActiveSkeleton: View {
    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.s) {
            VStack(alignment: .leading, spacing: 0) {
                OrdersBar(width: 170, height: 20)
                OrdersBar(width: 190, height: 16).padding(.top, Spacing.xs)
                OrdersBar(width: 220, height: 22).padding(.top, Spacing.s)
                Capsule().fill(CleansiaColors.surfaceVariant).frame(height: 4).padding(.top, Spacing.xs)
                OrdersBar(width: 160, height: 22).padding(.top, Spacing.m)
                Capsule().fill(CleansiaColors.surfaceVariant).frame(height: 10).padding(.top, Spacing.m)
                OrdersBar(width: 220, height: 16).padding(.top, Spacing.m)
                Capsule().fill(CleansiaColors.surfaceVariant)
                    .frame(height: 56)
                    .padding(.top, Spacing.s)
            }
            .padding(Spacing.m)
            .frame(maxWidth: .infinity, alignment: .leading)
            .ordersCard()
            OrdersBar(width: 90, height: 20, fill: CleansiaColors.outline)
                .padding(.top, Spacing.xs)
            VStack(spacing: 0) {
                ForEach(Array([150, 120, 170].enumerated()), id: \.offset) { index, street in
                    if index > 0 { Divider().overlay(CleansiaColors.outlineVariant) }
                    HStack(alignment: .top, spacing: Spacing.s) {
                        RoundedRectangle(cornerRadius: CornerRadius.small).fill(CleansiaColors.surfaceVariant)
                            .frame(width: 64, height: 64)
                        VStack(alignment: .leading, spacing: 6) {
                            OrdersBar(width: 118, height: 22)
                            OrdersBar(width: CGFloat(street), height: 16)
                        }
                        .frame(maxWidth: .infinity, alignment: .leading)
                        OrdersBar(width: 56, height: 20)
                    }
                    .padding(.horizontal, Spacing.m)
                    .padding(.vertical, Spacing.s)
                }
            }
            .ordersCard()
        }
        .accessibilityElement(children: .ignore)
        .accessibilityLabel(L10n.WorkContract.loading)
    }
}
