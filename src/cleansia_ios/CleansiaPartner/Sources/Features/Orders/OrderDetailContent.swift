import CleansiaCore
import CleansiaPartnerApi
import SwiftUI

struct OrderDetailContent: View {
    @Environment(\.locale) private var locale
    let order: OrderDetail
    var primaryAction: OrderPrimaryAction = .none
    var inFlightAction: OrderAction?
    var onConfirm: (OrderPrimaryAction) -> Void = { _ in }
    @ObservedObject var checklistVM: CleaningChecklistViewModel
    @ObservedObject var notesVM: OrderNotesViewModel
    @ObservedObject var photosVM: OrderPhotosViewModel

    private var showAccessCard: Bool {
        order.isAssignedToCurrentUser
            && !(order.accessInstructions ?? "").trimmingCharacters(in: .whitespaces).isEmpty
            && (order.status == ._3 || order.status == ._4)
    }

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

    /// Adds for notes/issues are allowed once the cleaner is OnTheWay/InProgress.
    private var canAddNotes: Bool {
        order.status == ._3 || order.status == ._4
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
                    if showAccessCard, let access = order.accessInstructions {
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
                    if order.isAssignedToCurrentUser {
                        NotesAndIssuesSection(
                            notes: order.orderNotes,
                            issues: order.orderIssues,
                            canAdd: canAddNotes,
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
                onCashConfirmRequested: { confirmingCash = true }
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

enum OrderTrackerSegment: Equatable {
    case past
    case current
    case future
}

enum OrderTrackerState: Equatable {
    /// The workflow never progressed, so segmenting it would claim phases that
    /// never happened — Android replaces the whole tracker for this one status.
    case cancelled
    case steps(segments: [OrderTrackerSegment], stepNumber: Int)
}

/// The in-sheet job tracker's rule, ported from Android's `ContinuousProgressBar`:
/// five phases with the current one sweeping, Completed filling every segment
/// rather than sitting on the last, and Cancelled dropping the segmentation.
///
/// One sealed answer rather than a segment list plus a cancelled flag, so a
/// renderer cannot forget the cancelled branch.
enum OrderTrackerProgress {
    static let stepCount = 5

    static func state(for status: OrderStatus?) -> OrderTrackerState {
        guard status != ._6 else { return .cancelled }
        let index = stepIndex(for: status)
        let isCompleted = status == ._5
        let segments = (0 ..< stepCount).map { position -> OrderTrackerSegment in
            if isCompleted || position < index { return .past }
            return position == index ? .current : .future
        }
        return .steps(segments: segments, stepNumber: index + 1)
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

private enum TrackerMetrics {
    static let barHeight: CGFloat = 4
    static let corner: CGFloat = 2
    static let gap: CGFloat = 6
    static let counterGap: CGFloat = 4
    static let bandWidthFraction: CGFloat = 0.5
    /// The band enters from off-screen left and finishes well past the right
    /// edge, so half of each cycle is a rest beat on the armed track.
    static let travelStart: CGFloat = -0.5
    static let travelEnd: CGFloat = 2.5
    static let cycleSeconds: Double = 1.3
}

struct OrderTrackerHero: View {
    @Environment(\.locale) private var locale
    let status: OrderStatus?

    var body: some View {
        Group {
            switch OrderTrackerProgress.state(for: status) {
            case .cancelled:
                CancelledTrack()
            case let .steps(segments, stepNumber):
                VStack(alignment: .trailing, spacing: TrackerMetrics.counterGap) {
                    Text(L10n.Orders.trackerStepCounter(stepNumber, OrderTrackerProgress.stepCount))
                        .font(CleansiaTypography.labelSmall)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                    HStack(spacing: TrackerMetrics.gap) {
                        ForEach(Array(segments.enumerated()), id: \.offset) { _, segment in
                            TrackerSegment(state: segment)
                        }
                    }
                }
                .frame(maxWidth: .infinity, alignment: .trailing)
            }
        }
        .id(locale.identifier)
    }
}

private struct TrackerSegment: View {
    let state: OrderTrackerSegment

    var body: some View {
        switch state {
        case .past:
            flatBar(CleansiaColors.primary)
        case .future:
            flatBar(CleansiaColors.outlineVariant)
        case .current:
            SweepingTrackerSegment()
        }
    }

    private func flatBar(_ color: Color) -> some View {
        RoundedRectangle(cornerRadius: TrackerMetrics.corner)
            .fill(color)
            .frame(height: TrackerMetrics.barHeight)
    }
}

/// The current phase: an armed track under a soft band gliding left to right on
/// a linear loop. Quiet by design — it sits on screen for a multi-hour job.
private struct SweepingTrackerSegment: View {
    @Environment(\.accessibilityReduceMotion) private var reduceMotion
    @State private var sweptToEnd = false

    var body: some View {
        GeometryReader { geometry in
            let width = geometry.size.width
            Rectangle()
                .fill(
                    LinearGradient(
                        colors: [
                            CleansiaColors.primary.opacity(0),
                            CleansiaColors.primary.opacity(0.18),
                            CleansiaColors.primary.opacity(0.30)
                        ],
                        startPoint: .leading,
                        endPoint: .trailing
                    )
                )
                .frame(width: width * TrackerMetrics.bandWidthFraction)
                .offset(x: width * (sweptToEnd ? TrackerMetrics.travelEnd : TrackerMetrics.travelStart))
        }
        .frame(height: TrackerMetrics.barHeight)
        .background(CleansiaColors.primary.opacity(0.22))
        .clipShape(RoundedRectangle(cornerRadius: TrackerMetrics.corner))
        .onAppear {
            guard !reduceMotion else { return }
            withAnimation(
                .linear(duration: TrackerMetrics.cycleSeconds).repeatForever(autoreverses: false)
            ) {
                sweptToEnd = true
            }
        }
    }
}

private struct CancelledTrack: View {
    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.xs) {
            Text(L10n.Orders.statusLabel(._6))
                .font(CleansiaTypography.labelLarge)
                .foregroundColor(CleansiaColors.error)
            RoundedRectangle(cornerRadius: TrackerMetrics.corner)
                .fill(CleansiaColors.error)
                .frame(height: TrackerMetrics.barHeight)
        }
        .frame(maxWidth: .infinity, alignment: .leading)
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
