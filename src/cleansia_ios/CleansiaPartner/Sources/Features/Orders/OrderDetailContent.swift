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
    var onConfirm: (OrderPrimaryAction) -> Void = { _ in }
    var onDeclineOffer: () -> Void = {}
    var onDismissRefusal: () -> Void = {}
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

    /// Before photos upload once OnTheWay/InProgress; after photos only once
    /// InProgress (OrderDetailScreen.kt:530-532 parity).
    private var canUploadBefore: Bool {
        order.status == ._3 || order.status == ._4
    }

    private var canUploadAfter: Bool {
        order.status == ._4
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
        ZStack {
            detail
            if confirmingCash {
                CleansiaDialog(
                    title: L10n.Orders.markCashCollectedConfirmTitle,
                    confirmLabel: L10n.Orders.markCashCollectedConfirmAction,
                    onConfirm: {
                        confirmingCash = false
                        onConfirm(.collectCash)
                    },
                    onDismiss: { confirmingCash = false },
                    message: cashConfirmMessage,
                    dismissLabel: L10n.cancel,
                    icon: "banknote",
                    confirmEnabled: inFlightAction != .markCashCollected
                )
            }
            if decliningOffer {
                OfferDeclineDialog(
                    onConfirm: {
                        decliningOffer = false
                        onDeclineOffer()
                    },
                    onDismiss: { decliningOffer = false }
                )
            }
            if let refusal, inFlightAction == nil {
                OfferRefusalDialog(refusal: refusal, onDismiss: onDismissRefusal)
            }
        }
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
                            canUploadBefore: canUploadBefore,
                            canUploadAfter: canUploadAfter
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
            }
            Spacer()
            if let pay = order.pay, pay > 0 {
                Text(OrdersFormat.money(pay, symbol: order.currencySymbol))
                    .font(CleansiaTypography.titleLarge)
                    .foregroundColor(CleansiaColors.primary)
            }
        }
        .padding(.horizontal, Spacing.m)
        .padding(.bottom, Spacing.s)
    }
}

struct OrderStatusPill: View {
    @Environment(\.locale) private var locale
    let status: OrderStatus?

    var body: some View {
        Text(L10n.Orders.statusLabel(status))
            .font(CleansiaTypography.labelSmall)
            .foregroundColor(tint)
            .padding(.horizontal, Spacing.xs)
            .padding(.vertical, 2)
            .background(tint.opacity(0.14), in: Capsule())
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
            crew: .spotsOpen(crewSize: 2, openSpots: 1),
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
