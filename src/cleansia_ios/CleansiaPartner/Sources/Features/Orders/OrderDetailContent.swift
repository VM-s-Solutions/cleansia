import CleansiaCore
import CleansiaPartnerApi
import SwiftUI

struct OrderDetailContent: View {
    @Environment(\.locale) private var locale
    let order: OrderDetail
    var primaryAction: OrderPrimaryAction = .none
    var inFlightAction: OrderAction?
    var preferredOffer: PendingOfferItem?
    var refusal: OfferRefusal?
    var contractStanding: WorkContractStanding = .none
    var onConfirm: (OrderPrimaryAction) -> Void = { _ in }
    var onOpenContract: (WorkContractRequest) -> Void = { _ in }
    var onDeclineOffer: () -> Void = {}
    var onDismissRefusal: () -> Void = {}
    var onReportLockout: (String) -> Void = { _ in }
    @ObservedObject var checklistVM: CleaningChecklistViewModel
    @ObservedObject var notesVM: OrderNotesViewModel
    @ObservedObject var photosVM: OrderPhotosViewModel

    private var showFromCustomerCard: Bool {
        !(order.customerNotes ?? "").trimmingCharacters(in: .whitespaces).isEmpty
            || !(order.specialInstructions ?? "").trimmingCharacters(in: .whitespaces).isEmpty
    }

    private var checklistInteractive: Bool {
        order.status == ._4
    }

    private var isTerminal: Bool {
        order.status == ._5 || order.status == ._6
    }

    @State private var confirmingCash = false
    @State private var decliningOffer = false

    private var cashConfirmMessage: String {
        guard let cashAmount = order.cashDueLabel else {
            return L10n.Orders.markCashCollectedConfirmMessageNoAmount
        }
        return L10n.Orders.markCashCollectedConfirmMessage(cashAmount)
    }

    var body: some View {
        detail
            // The button that raises it is busy while the collection is in flight, and the view
            // model refuses a second mutation, so the confirm needs no busy state of its own.
            .alert(L10n.Orders.markCashCollectedConfirmTitle, isPresented: $confirmingCash) {
                Button(L10n.Orders.markCashCollectedConfirmAction) { onConfirm(.collectCash) }
                Button(L10n.cancel, role: .cancel) {}
            } message: {
                Text(cashConfirmMessage)
            }
            .offerDeclineAlert(
                decliningOffer ? preferredOffer : nil,
                onDismiss: { decliningOffer = false },
                onConfirm: { _ in onDeclineOffer() }
            )
            .offerRefusalAlert(inFlightAction == nil ? refusal : nil, onDismiss: onDismissRefusal)
    }

    private var detail: some View {
        VStack(spacing: 0) {
            OrderDetailCompactHeader(order: order, locale: locale)
            ScrollView {
                VStack(spacing: Spacing.m) {
                    // Zero spacing: the timer text and the segmented bar are one
                    // hero block, not two stacked sections.
                    VStack(alignment: .leading, spacing: 0) {
                        OrderTimerCard(order: order, locale: locale)
                        OrderTrackerHero(status: order.status)
                    }
                    OrderMetadataRow(order: order, locale: locale)
                    if order.showsAccessCard, let access = order.accessInstructions {
                        AccessCard(instructions: access)
                    }
                    CustomerCard(order: order)
                    WorkContractCard(
                        standing: contractStanding,
                        onAccept: { onOpenContract(.accept(orderId: order.id)) },
                        onRead: { onOpenContract(.read(acceptanceId: $0)) }
                    )
                    LockoutCard(
                        order: order,
                        isReporting: inFlightAction == .reportLockout,
                        actionsEnabled: inFlightAction == nil,
                        photosVM: photosVM,
                        onReport: onReportLockout
                    )
                    ScopeCard(order: order)
                    if showFromCustomerCard {
                        FromCustomerNotesCard(order: order)
                    }
                    if order.showsWorkSections {
                        CleaningChecklistView(
                            order: order,
                            checkedIds: checklistVM.checkedIds,
                            interactive: checklistInteractive,
                            onToggle: checklistVM.setChecked
                        )
                    }
                    if order.showsNotesAndIssues {
                        NotesAndIssuesSection(
                            notes: order.orderNotes,
                            issues: order.orderIssues,
                            canAdd: order.canAddNotes,
                            isReadOnly: isTerminal,
                            vm: notesVM
                        )
                    }
                    if order.showsWorkSections {
                        PhotosSection(
                            vm: photosVM,
                            canUploadBefore: order.photoWindowOpen(for: ._1),
                            canUploadAfter: order.photoWindowOpen(for: ._2)
                        )
                    }
                    PaymentCard(order: order)
                    StatusTimelineView(history: order.statusHistory, locale: locale)
                }
                .padding(.horizontal, Spacing.m)
                .padding(.vertical, Spacing.m)
            }
            StickyActionFooter(
                action: primaryAction,
                inFlightAction: inFlightAction,
                onConfirm: onConfirm,
                onCashConfirmRequested: { confirmingCash = true },
                preferredOffer: preferredOffer,
                onDeclineOffer: { decliningOffer = true }
            )
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .top)
    }
}

/// Always-visible header at the top of the sheet (never scrolls) — order #,
/// status pill, date, pay (the compact-header parity).
///
/// **All three lines left-aligned, and the trailing half deliberately empty.** The mascot puck rides
/// the sheet's top edge at `.trailing` (see `SnapSheet`'s ornament), so its lower half lands on exactly
/// this row — and while the pay sat in the trailing slot, the character painted over the one number the
/// cleaner opens this screen for. The customer sheet moved its date off the right half for the same
/// reason.
private struct OrderDetailCompactHeader: View {
    let order: OrderDetail
    let locale: Locale

    var body: some View {
        HStack(alignment: .top) {
            VStack(alignment: .leading, spacing: 2) {
                HStack(spacing: Spacing.xs) {
                    Text("#\(order.orderNumber)")
                        .font(CleansiaTypography.titleMedium)
                        .foregroundColor(CleansiaColors.onSurface)
                    OrderStatusPill(status: order.status)
                }
                Text(OrdersFormat.relativeDateTime(order.cleaningDateTime, locale: locale))
                    .font(CleansiaTypography.bodyMedium)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
                    .lineLimit(1)
                    .minimumScaleFactor(0.8)
                if let pay = order.pay, pay > 0 {
                    Text(OrdersFormat.money(pay, symbol: order.currencySymbol))
                        .font(CleansiaTypography.titleLarge)
                        .foregroundColor(CleansiaColors.primaryText)
                        .lineLimit(1)
                        .minimumScaleFactor(0.8)
                }
            }
            Spacer(minLength: mascotClearance)
        }
        .padding(.horizontal, Spacing.m)
        .padding(.bottom, Spacing.s)
    }
}

/// How much of the header's trailing edge the mascot puck occupies: half its 128pt width plus the
/// trailing inset `SnapSheet` gives it. Reserved rather than merely avoided, so a long cs/uk date
/// shrinks instead of sliding under the character.
private let mascotClearance: CGFloat = 80

struct OrderStatusPill: View {
    @Environment(\.locale) private var locale
    let status: OrderStatus?

    var body: some View {
        OrderStatusBadge(label: L10n.Orders.statusLabel(status), tint: tint, tone: tone)
            .id(locale.identifier)
    }

    private var tint: Color {
        switch status {
        case ._0, ._1: CleansiaColors.warningStar
        case ._2: CleansiaColors.primary
        case ._3, ._4: CleansiaColors.secondary
        case ._5: CleansiaColors.successText
        case ._6: CleansiaColors.error
        case .none: CleansiaColors.onSurfaceVariant
        }
    }

    /// The group the mark draws: hue agrees with it, but shape is what carries it to a reader who cannot
    /// separate the hues — see `OrderStatusBadge`.
    private var tone: OrderStatusTone {
        switch status {
        case ._0, ._1, ._2, ._3, ._4: .live
        case ._5: .done
        case ._6: .failed
        case .none: .unknown
        }
    }
}

/// The partner's five-phase tracker. The rule and the bar both live in `CleansiaCore`
/// (`OrderTrackerRule` / `OrderTrackerBar`); this maps the generated `OrderStatus` onto a step index and
/// supplies the translated labels.
///
/// The customer app draws the same bar off its own status enum — that split is why the shared rule takes
/// an index rather than a status.
enum OrderTrackerProgress {
    static let stepCount = OrderTrackerRule.stepCount

    static func state(for status: OrderStatus?) -> OrderTrackerState {
        OrderTrackerRule.state(
            step: stepIndex(for: status),
            cancelled: status == ._6,
            completed: status == ._5
        )
    }

    private static func stepIndex(for status: OrderStatus?) -> Int {
        switch status {
        case ._2: 1
        case ._3: 2
        case ._4: 3
        case ._5: 4
        default: 0
        }
    }
}

struct OrderTrackerHero: View {
    @Environment(\.locale) private var locale
    let status: OrderStatus?

    var body: some View {
        OrderTrackerBar(
            state: OrderTrackerProgress.state(for: status),
            cancelledLabel: L10n.Orders.statusLabel(._6),
            stepCounterLabel: { step, total in L10n.Orders.trackerStepCounter(step, total) }
        )
        .id(locale.identifier)
    }
}

private struct OrderMetadataRow: View {
    let order: OrderDetail
    let locale: Locale

    var body: some View {
        HStack {
            Label(OrdersFormat.relativeDateTime(order.cleaningDateTime, locale: locale), systemImage: "calendar")
                .font(CleansiaTypography.bodyMedium)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
            Spacer()
        }
    }
}

#if DEBUG
    extension OrderDetail {
        static let preview = OrderDetail(
            id: "order-1",
            orderNumber: "ORD-2026-001",
            status: ._4,
            cleaningDateTime: Date(timeIntervalSinceNow: 3600),
            completedAt: nil,
            pay: 1200,
            currencyCode: "CZK",
            currencySymbol: "Kč",
            location: .precise(.init(
                line: "Vinohradská 12, Praha, 120 00",
                coordinate: Coordinate(latitude: 50.0755, longitude: 14.4378)
            )),
            customerName: "Jana Nováková",
            customerPhone: "+420 777 123 456",
            rooms: 3,
            bathrooms: 2,
            dirtinessLevel: ._1,
            crew: .spotsOpen(crewSize: 2, openSpots: 1),
            seats: [OrderSeat(id: "seat-1", employeeId: "emp-1")],
            workContractAcceptances: [
                WorkContractAcceptance(
                    id: "acc-1",
                    orderEmployeeId: "seat-1",
                    acceptedOn: Date(timeIntervalSinceNow: -7200),
                    documentVersion: "2026-09-20"
                )
            ],
            services: [
                OrderDetailService(id: "svc-standard", name: "Standard clean"),
                OrderDetailService(id: "svc-window", name: "Window clean")
            ],
            packages: [OrderDetailPackage(id: "pkg-deep", name: "Deep clean", price: 800)],
            extras: ["inside-oven", "interior-windows"],
            customerNotes: "Cat is friendly.",
            specialInstructions: "Use the eco products under the sink.",
            accessInstructions: "Code 1234 at the gate.",
            payment: OrderDetailPayment(
                subtotal: 1400,
                total: 1200,
                tierDiscount: 200,
                membershipDiscount: nil,
                promoDiscount: nil,
                typeCode: PaymentTypeCode.card.rawValue,
                statusCode: PaymentStatusCode.paid.rawValue
            ),
            isAssignedToCurrentUser: true,
            hasAfterPhotos: false,
            lockoutReportedAt: nil,
            lockoutCallAttempts: nil,
            orderNotes: [],
            orderIssues: [],
            // An hour-old InProgress stamp so the preview renders the live clock.
            statusHistory: [
                OrderStatusTrackDto(status: Code(value: 4), createdOn: Date(timeIntervalSinceNow: -3725))
            ]
        )
    }

    struct OrderDetailContent_Previews: PreviewProvider {
        static var previews: some View {
            OrderDetailContent(
                order: .preview,
                checklistVM: .preview,
                notesVM: .preview,
                photosVM: .preview
            )
            .background(CleansiaColors.surface)
        }
    }
#endif
