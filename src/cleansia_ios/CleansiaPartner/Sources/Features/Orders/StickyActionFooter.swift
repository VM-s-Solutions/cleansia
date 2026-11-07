import CleansiaCore
import CleansiaPartnerApi
import SwiftUI

/// The detail's one move, pinned under the thumb: the resolved `OrderPrimaryAction` (the shared machine) drawn as
/// the control the board uses for the same act — a slide to set off, start and complete, the locked slide while
/// the server would refuse, Take disabled with its reason, the after photo, the cash pair, the crew's wait. Renders
/// nothing when the state has no move.
struct StickyActionFooter: View {
    @Environment(\.locale) private var locale
    let action: OrderPrimaryAction
    let inFlightAction: OrderAction?
    let onConfirm: (OrderPrimaryAction) -> Void
    /// Raised instead of confirming inline: the screen root (`OrderDetailContent`) owns the cash-collected
    /// confirmation, a native `.alert`, so the footer only asks for it.
    var onCashConfirmRequested: () -> Void = {}
    /// Shown under the cash collection only; its confirmation is the screen root's too.
    var offersCashNotPaid = false
    var onCashNotPaidRequested: () -> Void = {}
    /// The reservation held for this cleaner on this order. Present only where a hold exists, which is
    /// why the screen degrades to an ordinary job in the short-lead band the push also reaches.
    var preferredOffer: PendingOfferItem?
    var onDeclineOffer: () -> Void = {}
    /// The cash to collect as the customer still owes it, for the button's own label.
    var cashAmount: String?
    /// The crew-mate whose part is still open while hers is marked.
    var waitingFor: OrderSeat?
    /// Late and not set off: the start offered under the set-off slide, asked for in a sheet over the page. The sheet
    /// closes as the start is sent, and the footer shows it under way in the set-off slide's place.
    var offersStartFromConfirmed = false
    var onTakeAfterPhoto: () -> Void = {}
    var now = Date()

    @State private var confirmingStart = false
    @State private var homeIndicator: CGFloat = 0

    private func isBusy(_ orderAction: OrderAction) -> Bool {
        inFlightAction == orderAction
    }

    var body: some View {
        switch action {
        case .take:
            footer { take(waiting: false) }
        case let .takeOverlaps(start, end):
            footer { refusedTake(start: start, end: end) }
        case .takeWaiting:
            footer { take(waiting: true) }
        case let .setOffLocked(opensAt):
            footer { SlideToConfirm(lockedReason: L10n.tie(setOffReason(opensAt))) }
        case .notifyOnTheWay:
            footer {
                VStack(spacing: Spacing.xxs) {
                    if isBusy(.start) {
                        slide(.start, idle: L10n.Orders.slideToStart, busy: L10n.Orders.startingOrder)
                    } else {
                        slide(
                            .notifyOnTheWay,
                            idle: L10n.Orders.swipeToNotifyOnTheWay,
                            busy: L10n.Orders.customerNotifiedOnTheWay
                        )
                        if offersStartFromConfirmed {
                            FooterTextLink(
                                L10n.Orders.lateAlreadyThere,
                                isBusy: false,
                                enabled: inFlightAction == nil
                            ) { confirmingStart = true }
                        }
                    }
                }
                .sheet(isPresented: $confirmingStart) {
                    StartConfirmation(
                        onCancel: { confirmingStart = false },
                        slide: {
                            slide(.start, idle: L10n.Orders.slideToStart, busy: L10n.Orders.startingOrder) {
                                confirmingStart = false
                            }
                        }
                    )
                }
            }
        case .start:
            footer { slide(.start, idle: L10n.Orders.slideToStart, busy: L10n.Orders.startingOrder) }
        case .collectCash:
            // The two irreversible money answers in the app: the server rejects a second
            // call and neither can be taken back from here, so each asks first.
            footer {
                VStack(spacing: Spacing.xxs) {
                    CleansiaPrimaryButton(
                        cashAmount.map(L10n.Orders.cashMarkAmount) ?? L10n.Orders.markCashCollected,
                        loading: isBusy(.markCashCollected),
                        enabled: inFlightAction == nil,
                        action: onCashConfirmRequested
                    )
                    if offersCashNotPaid {
                        FooterTextLink(
                            L10n.Orders.cashNotPaidAction,
                            isBusy: isBusy(.reportCashNotPaid),
                            enabled: inFlightAction == nil,
                            onTap: onCashNotPaidRequested
                        )
                    }
                }
            }
        case .markDone:
            footer {
                slide(
                    .markDone,
                    idle: L10n.Orders.markDone,
                    busy: L10n.Orders.markingDone,
                    detail: L10n.Orders.markDoneSub
                )
            }
        case .awaitingCrew:
            footer { CrewWait(mate: waitingFor) }
        case .complete:
            footer { slide(.complete, idle: L10n.Orders.slideToComplete, busy: L10n.Orders.completingOrder) }
        case .completeJob:
            footer { slide(.completeJob, idle: L10n.Orders.slideToComplete, busy: L10n.Orders.completingOrder) }
        case .completeBlocked:
            footer { CleansiaPrimaryButton(L10n.Orders.takeAfterPhoto, leadingIcon: "camera", action: onTakeAfterPhoto)
            }
        case .paymentNotConfirmed:
            footer { SlideToConfirm(lockedReason: L10n.tie(L10n.Orders.paymentNotConfirmed)) }
        case .none:
            EmptyView()
        }
    }

    // MARK: - Take

    /// While her own rows are read the take waits; on a job reserved for her only Confirm does, since declining it
    /// does not depend on her other jobs.
    @ViewBuilder
    private func take(waiting: Bool) -> some View {
        if preferredOffer != nil {
            // On a job reserved for her by name the sheet runs the same command with a different word:
            // confirming IS taking.
            HStack(spacing: Spacing.s) {
                CleansiaOutlinedButton(
                    L10n.Offers.decline,
                    contentColor: CleansiaColors.primaryText,
                    borderColor: CleansiaColors.primary,
                    horizontalPadding: Spacing.xs,
                    enabled: inFlightAction == nil,
                    action: onDeclineOffer
                )
                CleansiaPrimaryButton(
                    L10n.Offers.confirm,
                    loading: isBusy(.take),
                    enabled: !waiting && inFlightAction == nil
                ) { onConfirm(.take) }
            }
        } else {
            // Opens the contract sheet; the deliberate gesture sits under the text it accepts. Spins while the
            // host reconciles the order after the sheet's verdict, so it cannot reopen the sheet on a job taken.
            CleansiaPrimaryButton(
                L10n.Orders.takeOrder,
                loading: isBusy(.take),
                enabled: !waiting && inFlightAction == nil
            ) { onConfirm(.take) }
        }
    }

    /// The one red mark by the move: the reason, with a lock, over the Take the server would refuse.
    private func refusedTake(start: Date, end: Date) -> some View {
        let reason = L10n.Orders.flagOverlap(OrdersBoardRules.unbreakable(
            OrdersFormat.timeWindow(start, minutes: Int(end.timeIntervalSince(start) / 60), locale: locale)
        ))
        return VStack(spacing: 10) {
            HStack(alignment: .firstTextBaseline, spacing: 6) {
                Image(systemName: "lock")
                    .font(CleansiaTypography.bodyMedium)
                    .capBandAligned(of: CleansiaTypography.scale.bodyMedium)
                Text(L10n.tie(reason))
                    .font(CleansiaTypography.bodyMedium)
                    .multilineTextAlignment(.center)
                    .fixedSize(horizontal: false, vertical: true)
            }
            .foregroundColor(CleansiaColors.error)
            .accessibilityHidden(true)
            CleansiaPrimaryButton(
                L10n.Orders.takeOrder,
                enabled: false,
                disabledContainerColor: CleansiaColors.surfaceVariant,
                disabledContentColor: CleansiaColors.onSurfaceVariant
            ) {}
                .accessibilityHint(reason)
        }
    }

    // MARK: - Slides

    /// `first` runs before the move is sent: the start asked for in a sheet closes it.
    private func slide(
        _ move: OrderPrimaryAction,
        idle: String,
        busy: String,
        detail: String? = nil,
        first: @escaping () -> Void = {}
    ) -> some View {
        let busyNow = move.orderAction.map(isBusy) ?? false
        let commit = {
            first()
            onConfirm(move)
        }
        return SlideToConfirm(
            idleLabel: L10n.tie(idle),
            busyLabel: L10n.tie(busy),
            isBusy: busyNow,
            maxLabelLines: 2,
            thumbLanes: true,
            detail: detail,
            onConfirm: commit
        )
        .disabled(inFlightAction != nil)
    }

    private func setOffReason(_ opensAt: Date) -> String {
        L10n.Orders.setOffFrom(OrdersFormat.timeOnly(opensAt, locale: locale))
    }

    /// The home indicator's inset is the room under the move; a phone without one keeps 12.
    static func bottomPadding(homeIndicator: CGFloat) -> CGFloat {
        homeIndicator > 0 ? 0 : Spacing.s
    }

    private func footer(@ViewBuilder _ control: () -> some View) -> some View {
        VStack(spacing: 0) {
            control()
        }
        .padding(.horizontal, Spacing.m)
        .padding(.top, Spacing.s)
        .padding(.bottom, Self.bottomPadding(homeIndicator: homeIndicator))
        .readableWidth()
        .frame(maxWidth: .infinity)
        .background(CleansiaColors.surface.ignoresSafeArea(edges: .bottom))
        .background(GeometryReader { space in
            Color.clear
                .onAppear { homeIndicator = space.safeAreaInsets.bottom }
                .onChange(of: space.safeAreaInsets.bottom) { homeIndicator = $0 }
        })
        .overlay(alignment: .top) { Divider().overlay(CleansiaColors.outlineVariant) }
    }
}

/// While it is sent, a spinner of the link's own touch-target height stands in for it, so the footer does not
/// drop under the cleaner's thumb.
private struct FooterTextLink: View {
    let label: String
    let isBusy: Bool
    let enabled: Bool
    let onTap: () -> Void

    init(_ label: String, isBusy: Bool, enabled: Bool, onTap: @escaping () -> Void) {
        self.label = label
        self.isBusy = isBusy
        self.enabled = enabled
        self.onTap = onTap
    }

    var body: some View {
        if isBusy {
            ProgressView()
                .progressViewStyle(.circular)
                .padding(Spacing.xxs)
                .frame(minHeight: 44)
        } else {
            CleansiaTextLink(label, maxLines: 2, action: onTap)
                .frame(maxWidth: .infinity)
                .disabled(!enabled)
        }
    }
}

/// *Already there? Start cleaning*: the start slide and Cancel in a sheet sized to them, so the set-off slide stays
/// pinned behind it and a mistaken tap costs nothing.
private struct StartConfirmation<Slide: View>: View {
    let onCancel: () -> Void
    @ViewBuilder let slide: () -> Slide
    @State private var height: CGFloat = 240

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.m) {
            Text(L10n.tie(L10n.Orders.lateAlreadyThere))
                .cleansiaFont(CleansiaTypography.headlineSmall)
                .foregroundColor(CleansiaColors.onSurface)
                .fixedSize(horizontal: false, vertical: true)
                .accessibilityAddTraits(.isHeader)
            slide()
            CleansiaTextLink(L10n.cancel, action: onCancel)
                .frame(maxWidth: .infinity)
        }
        .padding(.horizontal, Spacing.m)
        .padding(.top, Spacing.l)
        .padding(.bottom, Spacing.s)
        .background(GeometryReader { space in
            Color.clear
                .onAppear { height = space.size.height }
                .onChange(of: space.size.height) { height = $0 }
        })
        .presentationDetents([.height(height)])
        .presentationDragIndicator(.visible)
    }
}

/// Her part is marked and her crew-mate's is not: Call her while she has a phone, else say who the job waits for.
private struct CrewWait: View {
    @Environment(\.locale) private var locale
    @Environment(\.openURL) private var openURL
    let mate: OrderSeat?

    var body: some View {
        let name = mate?.givenName ?? ""
        Group {
            if let call = ContactActions.callURL(phone: mate?.phone) {
                CleansiaOutlinedButton(
                    "\(L10n.Orders.actionCall)\(OrdersBoardRules.dot)\(name)",
                    leadingIcon: "phone",
                    contentColor: CleansiaColors.primaryText,
                    borderColor: CleansiaColors.primary
                ) { openURL(call) }
            } else {
                Text(L10n.tie(L10n.Orders.waitingFor(name)))
                    .font(CleansiaTypography.bodyMedium)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
                    .multilineTextAlignment(.center)
                    .frame(maxWidth: .infinity, minHeight: 56)
            }
        }
        .id(locale.identifier)
    }
}
