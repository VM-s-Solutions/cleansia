import CleansiaCore
import CleansiaPartnerApi
import SwiftUI

/// The job detail for a job it can show, on the band page: the head; for her own job the run sheet and the reference
/// sections, each only with something in it; for a job not taken what it is and what else that day holds; for a
/// cancelled one what it pays and what her day still holds; Withdraw at the sheet's end; and the state's one move
/// pinned under the thumb. While the job loads, and when the rest of it fails, the same page is drawn from the board's
/// row.
struct OrderDetailContent: View {
    /// The part of the job only `GetById` brings: floor, flat and the customer's words.
    enum Rest {
        case loaded
        case loading
        case failed
    }

    @Environment(\.locale) private var locale
    @Environment(\.accessibilityReduceMotion) private var reduceMotion
    let order: OrderDetail
    var now = Date()
    var photos: [OrderPhoto] = []
    var myEmployeeId: String?
    var band: HomeDistanceBand?
    var ownRows: [OrderListItem]?
    var openRows: [OrderListItem]?
    var openTotal: Int?
    var hasBoardRow = true
    var clash: OrderListItem?
    var rest = Rest.loaded
    var primaryAction: OrderPrimaryAction = .none
    /// Drawn from the board's row while the job loads, with a move the row cannot tell: its slot is held.
    var holdsMoveSlot = false
    var offersCashNotPaid = false
    var offersStartFromConfirmed = false
    var inFlightAction: OrderAction?
    var preferredOffer: PendingOfferItem?
    var refusal: OfferRefusal?
    var contractStanding: WorkContractStanding = .none
    var canDropOrder = false
    var onConfirm: (OrderPrimaryAction) -> Void = { _ in }
    var onOpenContract: (WorkContractRequest) -> Void = { _ in }
    var onDeclineOffer: () -> Void = {}
    var onDismissRefusal: () -> Void = {}
    var onReportLockout: (String) -> Void = { _ in }
    var onReportCashNotPaid: () -> Void = {}
    var onDropOrder: () -> Void = {}
    var onRetry: () -> Void = {}
    var onOpenJob: ((String) -> Void)?
    var onBrowseJobs: (() -> Void)?
    var onOpenEarnings: (() -> Void)?
    @ObservedObject var checklistVM: CleaningChecklistViewModel
    @ObservedObject var notesVM: OrderNotesViewModel
    @ObservedObject var photosVM: OrderPhotosViewModel

    @State private var confirmingCash = false
    @State private var confirmingCashNotPaid = false
    @State private var decliningOffer = false
    @State private var confirmingDrop = false
    @State private var afterPhotoRequested = false

    private var isMine: Bool {
        order.isAssignedToCurrentUser && order.status != ._0 && order.status != ._1
    }

    private var facts: JobHeadFacts {
        order.headFacts(myEmployeeId: myEmployeeId, photos: photos, band: band, now: now, locale: locale)
    }

    private var cashConfirmMessage: String {
        guard let cashAmount = order.cashDueLabel else {
            return L10n.Orders.markCashCollectedConfirmMessageNoAmount
        }
        return L10n.Orders.markCashCollectedConfirmMessage(cashAmount)
    }

    private var cashNotPaidConfirmMessage: String {
        guard let owed = order.cashNotPaidOwedLabel else {
            return L10n.Orders.cashNotPaidConfirmMessageNoAmount
        }
        return L10n.Orders.cashNotPaidConfirmMessage(owed)
    }

    private var mascot: MascotArt? {
        OrderDetailMascotArt.art(
            for: order.status,
            isMine: isMine,
            offered: preferredOffer != nil,
            reported: order.lockoutReportedAt != nil,
            cash: order.payment.isCash,
            failed: rest == .failed,
            cleaning: isMine && order.runFacts(myEmployeeId: myEmployeeId, photos: photos)
                .flatMap(OrdersBoardRules.runStep) == .clean,
            reduceMotion: reduceMotion
        )
    }

    var body: some View {
        JobDetailFrame(
            mascot: mascot,
            endsWithMove: primaryAction != .none || holdsMoveSlot,
            map: JobBandMap.mode(location: order.location, area: order.area, taken: isMine)
        ) {
            JobHead(facts: facts)
        } sheet: {
            if let dayLine {
                dayLine.padding(.top, Spacing.m)
            }
            if let findings {
                findings.padding(.top, Spacing.s)
            }
            if rest == .failed {
                DetailFailureCard(title: L10n.Orders.errorRest, message: L10n.Orders.errorRestBody, onRetry: onRetry)
                    .padding(.top, Spacing.l)
            }
            sections
            end
        } pinned: {
            footer
        }
        // The button that raises it is busy while the collection is in flight, and the view
        // model refuses a second mutation, so the confirm needs no busy state of its own.
        .alert(L10n.Orders.markCashCollectedConfirmTitle, isPresented: $confirmingCash) {
            Button(L10n.Orders.markCashCollectedConfirmAction) { onConfirm(.collectCash) }
            Button(L10n.cancel, role: .cancel) {}
        } message: {
            Text(cashConfirmMessage)
        }
        .alert(L10n.Orders.cashNotPaidConfirmTitle, isPresented: $confirmingCashNotPaid) {
            Button(L10n.Orders.cashNotPaidConfirmAction, role: .destructive, action: onReportCashNotPaid)
            Button(L10n.cancel, role: .cancel) {}
        } message: {
            Text(cashNotPaidConfirmMessage)
        }
        // Its way out is "Keep the job", never "Cancel", inside a dialog asking whether to leave the job.
        .alert(L10n.Orders.dropConfirmTitle, isPresented: $confirmingDrop) {
            Button(L10n.Orders.dropConfirmAction, role: .destructive, action: onDropOrder)
            Button(L10n.Orders.dropKeep, role: .cancel) {}
        } message: {
            Text(L10n.Orders.dropConfirmMessage)
        }
        .offerDeclineAlert(
            decliningOffer ? preferredOffer : nil,
            onDismiss: { decliningOffer = false },
            onConfirm: { _ in onDeclineOffer() }
        )
        .offerRefusalAlert(inFlightAction == nil ? refusal : nil, onDismiss: onDismissRefusal)
    }

    // MARK: - The head

    private var dayLine: AnyView? {
        guard OrderDetailTimeline.shows(order, isMine: isMine) else { return nil }
        return AnyView(OrderDetailTimeline(order: order, now: now, ownRows: ownRows, clash: clash, isMine: isMine))
    }

    /// A job not hers draws one finding: her job it would overlap, else the hold, else the late join's flag. Her own
    /// job: the cash to collect and the contract still to accept.
    private var findings: AnyView? {
        if !isMine, order.status != ._6 {
            if let clash { return AnyView(clashLink(clash)) }
            if let preferredOffer { return AnyView(hold(preferredOffer)) }
            if order.status == ._3 || order.status == ._4, let end = order.bookedEnd {
                return AnyView(TintedFlag(
                    icon: "clock",
                    text: [
                        L10n.Orders.flagTakeUntil(OrdersFormat.timeOnly(end, locale: locale)), order.onItLine
                    ].compactMap { $0 }.joined(separator: OrdersBoardRules.dot)
                ))
            }
            return nil
        }
        guard isMine, order.cashFlag != nil || contractStanding == .pending else { return nil }
        return AnyView(VStack(alignment: .leading, spacing: Spacing.xs) {
            if let cash = order.cashFlag {
                TintedFlag(icon: "banknote", text: cash, tone: .amber)
            }
            if contractStanding == .pending {
                WorkContractCard(standing: .pending, onAccept: { onOpenContract(.accept(orderId: order.id)) })
            }
        })
    }

    private func hold(_ offer: PendingOfferItem) -> some View {
        VStack(alignment: .leading, spacing: Spacing.xxs) {
            Text(L10n.Offers.cardTitle)
                .font(CleansiaTypography.titleMedium)
                .foregroundColor(CleansiaColors.onSurface)
            OrdersDotFlow(
                items: [
                    Text(OfferLabels.reservedUntil(offer.respondByUtc, now: now))
                        .foregroundColor(CleansiaColors.primaryText),
                    Text(L10n.Orders.reservedThen).foregroundColor(CleansiaColors.onSurfaceVariant)
                ],
                style: CleansiaTypography.scale.bodyMedium
            )
        }
        .padding(.top, Spacing.s)
        .frame(maxWidth: .infinity, alignment: .leading)
        .overlay(alignment: .top) { Rectangle().fill(CleansiaColors.outlineVariant).frame(height: 1) }
    }

    /// *Your job that day*: the job of hers it would overlap, one disclosure row under a rule.
    private func clashLink(_ mine: OrderListItem) -> some View {
        Button { if let id = mine.id { onOpenJob?(id) } } label: {
            VStack(alignment: .leading, spacing: Spacing.hair) {
                Text(L10n.Orders.yourJobThatDay)
                    .font(CleansiaTypography.labelMedium)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
                HStack(alignment: .lastTextBaseline, spacing: Spacing.xs) {
                    OrdersDotFlow(
                        items: [
                            OrdersBoardRules.streetLine(mine.customerAddress).map {
                                Text($0).font(CleansiaTypography.bodyLarge)
                            },
                            OrdersBoardRules.window(of: mine, locale: locale).map { Text($0).monospacedDigit() },
                            OrdersBoardRules.heroTeam(mine).map { Text($0) }
                        ].compactMap { $0 },
                        style: CleansiaTypography.scale.bodyMedium
                    )
                    .foregroundColor(CleansiaColors.onSurface)
                    .frame(maxWidth: .infinity, alignment: .leading)
                    Image(systemName: "chevron.right")
                        .font(CleansiaTypography.labelLarge)
                        .foregroundColor(CleansiaColors.primaryText)
                        .capBandAligned(.lastTextBaseline, of: CleansiaTypography.scale.bodyMedium)
                        .accessibilityHidden(true)
                }
            }
            .padding(.top, Spacing.xs)
            .frame(maxWidth: .infinity, minHeight: 44, alignment: .leading)
            .overlay(alignment: .top) { Rectangle().fill(CleansiaColors.outlineVariant).frame(height: 1) }
            .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
        .disabled(onOpenJob == nil)
    }

    // MARK: - What follows the head

    @ViewBuilder
    private var sections: some View {
        if order.status == ._6 {
            CancelledSections(
                order: order,
                now: now,
                ownRows: ownRows,
                openRows: openRows,
                openTotal: openTotal,
                onOpenJob: onOpenJob,
                onBrowseJobs: onBrowseJobs,
                onOpenEarnings: onOpenEarnings
            )
        } else if isMine {
            ownSections
        } else if rest != .failed {
            NotTakenSections(
                order: order,
                now: now,
                ownRows: ownRows,
                openRows: openRows,
                clash: clash,
                hasBoardRow: hasBoardRow,
                onOpenJob: onOpenJob
            )
        }
    }

    @ViewBuilder
    private var ownSections: some View {
        let steps = order.runSheet(myEmployeeId: myEmployeeId, photos: photos, now: now, locale: locale)
        if !steps.isEmpty {
            JobRunSheet(steps: steps) { step in
                RunStepContent(
                    step: step,
                    order: order,
                    now: now,
                    photos: photos,
                    myEmployeeId: myEmployeeId,
                    rest: rest,
                    isReporting: inFlightAction == .reportLockout,
                    actionsEnabled: inFlightAction == nil,
                    afterPhotoRequested: $afterPhotoRequested,
                    onReportLockout: onReportLockout,
                    checklistVM: checklistVM,
                    notesVM: notesVM,
                    photosVM: photosVM
                )
            }
        }
        switch rest {
        case .loaded: referenceSections
        case .loading: loadingSection
        case .failed: EmptyView()
        }
    }

    @ViewBuilder
    private var referenceSections: some View {
        let plan = order.sectionPlan(myEmployeeId: myEmployeeId, photos: photos)
        if let customer = plan.customer {
            CustomerSection(order: order, part: customer, now: now)
        }
        if let scope = plan.scope {
            ScopeSection(order: order, rows: scope.rows)
        }
        if let payment = plan.payment {
            PaymentSection(payment: payment, onOpenEarnings: onOpenEarnings)
        }
        if case .accepted = contractStanding {
            WorkContractCard(standing: contractStanding, onRead: { onOpenContract(.read(acceptanceId: $0)) })
        }
    }

    /// The customer's words and the way in come with the job: bars at their real size until it does.
    private var loadingSection: some View {
        DetailSection(title: L10n.Orders.customerSectionTitle, icon: "person") {
            VStack(alignment: .leading, spacing: 6) {
                OrdersBar(width: 70, height: 14)
                OrdersBar(width: 290, height: 16)
                OrdersBar(width: 250, height: 16)
            }
        }
    }

    // MARK: - The end of the sheet and the move

    @ViewBuilder
    private var end: some View {
        if canDropOrder {
            VStack(spacing: 6) {
                CleansiaDangerButton(
                    L10n.Orders.dropAction,
                    leadingIcon: "xmark.circle",
                    enabled: inFlightAction == nil
                ) { confirmingDrop = true }
                if let until = order.dropUntil(now: now, locale: locale) {
                    Text(L10n.tie(until.prefix(1).uppercased(with: locale) + until.dropFirst()))
                        .font(CleansiaTypography.bodyMedium)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                        .multilineTextAlignment(.center)
                        .fixedSize(horizontal: false, vertical: true)
                }
            }
            .frame(maxWidth: .infinity)
            .padding(.top, Spacing.l)
        }
    }

    @ViewBuilder
    private var footer: some View {
        if holdsMoveSlot {
            MoveSlotBar()
        } else {
            StickyActionFooter(
                action: primaryAction,
                inFlightAction: inFlightAction,
                onConfirm: onConfirm,
                onCashConfirmRequested: { confirmingCash = true },
                offersCashNotPaid: offersCashNotPaid,
                onCashNotPaidRequested: { confirmingCashNotPaid = true },
                preferredOffer: preferredOffer,
                onDeclineOffer: { decliningOffer = true },
                cashAmount: order.cashDueLabel,
                waitingFor: order.openMate(myEmployeeId),
                offersStartFromConfirmed: offersStartFromConfirmed,
                onTakeAfterPhoto: { afterPhotoRequested = true },
                now: now
            )
        }
    }
}

// MARK: - What the page says, built from the job

extension OrderDetail {
    func headFacts(
        myEmployeeId: String?,
        photos: [OrderPhoto],
        band: HomeDistanceBand?,
        now: Date,
        locale: Locale
    ) -> JobHeadFacts {
        JobHeadFacts(self, myEmployeeId: myEmployeeId, photos: photos, band: band, now: now, locale: locale)
    }

    func sectionPlan(myEmployeeId: String?, photos: [OrderPhoto]) -> DetailSectionPlan {
        DetailSectionPlan(self, myEmployeeId: myEmployeeId, photos: photos)
    }
}

extension JobHeadFacts {
    /// A job she has not taken wears her relation to it ("New") and says how far off its day is; her own job counts
    /// its run in four steps. The customer's name is the sub line's only where no customer row is drawn: Setting off
    /// and Getting in hold one, and so does a completed job's Customer section.
    init(
        _ order: OrderDetail,
        myEmployeeId: String?,
        photos: [OrderPhoto],
        band: HomeDistanceBand?,
        now: Date,
        locale: Locale
    ) {
        let isMine = order.isAssignedToCurrentUser && order.status != ._0 && order.status != ._1
        let isPast = order.customerDetailsClosedNote != nil && order.status == ._5
        let shown: OrderStatus? = isMine || order.status == ._6 ? order.status : ._0
        let runFacts = order.runFacts(myEmployeeId: myEmployeeId, photos: photos)
        let named = order.customerName.flatMap { $0.isBlank ? nil : $0 }
        let team = (order.crew?.crewSize ?? 1) > 1
        number = order.orderNumber.isEmpty ? "" : "#\(order.orderNumber)"
        statusWire = shown?.rawValue
        statusLabel = L10n.Orders.statusLabel(shown)
        day = order.cleaningDateTime.map { OrdersFormat.shortDay($0, locale: locale) }
        window = OrdersBoardRules.window(start: order.cleaningDateTime, minutes: order.estimatedMinutes, locale: locale)
        status = Self.statusItems(order, runFacts: runFacts, isMine: isMine, now: now, locale: locale)
        progress = Self.progress(runFacts: runFacts, isMine: isMine, now: now)
        street = isMine && !isPast
            ? OrdersBoardRules.streetLine(order.location.navigationTarget?.line) ?? order.location.line
            : order.area ?? order.location.line
        sub = if order.status == ._6 {
            order.scopeItems(locale: locale)
        } else if isPast {
            []
        } else if isMine {
            [order.area, order.status == ._4 ? named : nil].compactMap { $0 }
        } else {
            [OrdersBoardRules.bandText(band)].compactMap { $0 }
        }
        pay = order.status == ._6 ? nil : order.pay.map { OrdersFormat.money($0, symbol: order.currencySymbol) }
        caption = team ? L10n.Orders.yourShare : isMine ? L10n.WorkContract.reward : nil
    }

    private static func statusItems(
        _ order: OrderDetail,
        runFacts: OrdersBoardRules.RunFacts?,
        isMine: Bool,
        now: Date,
        locale: Locale
    ) -> [String] {
        if let runFacts, isMine || order.status == ._6,
           let line = OrdersBoardRules.runStatus(runFacts, now: now, locale: locale)?.items
        {
            return line
        }
        guard let start = order.cleaningDateTime else { return [] }
        let live = order.status == ._3 || order.status == ._4
        return OrdersBoardRules.howFarOff(start, live: live, now: now, locale: locale)
    }

    /// Setting off fills from the hour it opens to the start; Cleaning and Finishing with the booked window gone.
    static func progress(runFacts: OrdersBoardRules.RunFacts?, isMine: Bool, now: Date) -> Progress {
        guard let runFacts, let step = OrdersBoardRules.runStep(runFacts) else { return .none }
        if step == .cancelled { return .cancelled }
        guard isMine else { return .none }
        let share = { (from: Date, to: Date) -> Double? in
            let span = to.timeIntervalSince(from)
            guard span > 0, now >= from else { return nil }
            return min(max(now.timeIntervalSince(from) / span, 0), 1)
        }
        let opens = runFacts.start.addingTimeInterval(-TimeInterval(OrdersBoardRules.startGraceWindowMinutes) * 60)
        switch step {
        case .setOff: return .steps(step: 1, fraction: share(opens, runFacts.start), done: false)
        case .getIn: return .steps(step: 2, fraction: nil, done: false)
        case .clean: return .steps(step: 3, fraction: share(runFacts.start, runFacts.end), done: false)
        case .finish: return .steps(step: 4, fraction: share(runFacts.start, runFacts.end), done: false)
        case .done, .cancelled: return .steps(step: 4, fraction: nil, done: true)
        }
    }
}

extension DetailSectionPlan {
    /// The reference sections under her own job's run sheet, each fact said once. While the checklist is open it is
    /// the scope, so Property & scope keeps only the dirt and the crew. The customer's words go on the crew's and the
    /// cash finishing steps, whose step holds what is needed; the way in once Getting in is behind her.
    init(_ order: OrderDetail, myEmployeeId: String?, photos: [OrderPhoto]) {
        let step = order.runFacts(myEmployeeId: myEmployeeId, photos: photos).flatMap(OrdersBoardRules.runStep)
        let crewStep = order.isCrewJob && order.status == ._4 && order.mateMarked(myEmployeeId)
        let checklistOpen = (step == .clean && !crewStep)
            || (step == .finish && !order.isCrewJob && !order.payment.isCash)
        let who: Who? = if order.status == ._5 {
            order.customerDetailsClosedNote.map { .removed($0) } ?? .contact
        } else {
            nil
        }
        let finishingHeld = step == .finish && (order.isCrewJob || order.payment.isCash)
        let notes = [._2, ._3, ._4].contains(order.status) && !crewStep && !finishingHeld
            && FromCustomerNotes.shows(order)
        let access = order.status == ._4 && (order.showsAccessCard || !order.accessFacts.isEmpty)
        let dirt = L10n.Orders.dirtinessLevel(order.dirtinessLevel)
        let team = order.crew.flatMap { $0.crewSize > 1 ? L10n.Orders.heroTeam($0.crewSize) : nil }
        let reward = order.pay.map { OrdersFormat.money($0, symbol: order.currencySymbol) }
        let cashRule = order.payment.isCash
            ? reward.map { L10n.Orders.cashRule(OrdersBoardRules.nonBreaking($0)) }
            : nil
        customer = who != nil || notes || access ? Customer(who: who, notes: notes, access: access) : nil
        scope = !checklistOpen || dirt != nil || team != nil ? Scope(rows: !checklistOpen, dirt: dirt, team: team) : nil
        payment = cashRule != nil || order.status == ._5
            ? Payment(cashRule: cashRule, payouts: order.status == ._5)
            : nil
    }
}

/// What her own job's reference sections hold: the customer (who, what they wrote, the way in), the property and
/// scope, and the money the head does not say.
struct DetailSectionPlan: Equatable {
    enum Who: Equatable {
        /// Name and phone, when the details close, Call and SMS.
        case contact
        /// The details are gone: the sentence that says so.
        case removed(String)
    }

    struct Customer: Equatable {
        var who: Who?
        var notes: Bool
        var access: Bool
    }

    struct Scope: Equatable {
        var rows: Bool
        var dirt: String?
        var team: String?
    }

    struct Payment: Equatable {
        var cashRule: String?
        var payouts: Bool
    }

    var customer: Customer?
    var scope: Scope?
    var payment: Payment?
}

#if DEBUG
    extension OrderDetail {
        static let preview = OrderDetail(
            id: "order-1",
            orderNumber: "ORD-7F3A9C21",
            status: ._2,
            cleaningDateTime: Date(timeIntervalSinceNow: 4 * 3600),
            completedAt: nil,
            pay: 779,
            currencyCode: "CZK",
            currencySymbol: "Kč",
            location: .precise(.init(line: "Vinohradská 46, Praha, 12000", coordinate: nil)),
            area: "Praha 2",
            customerName: "Jana Nováková",
            customerPhone: "+420 604 218 377",
            rooms: 2,
            bathrooms: 1,
            dirtinessLevel: ._0,
            crew: nil,
            seats: [OrderSeat(id: "seat-1", employeeId: "emp-1")],
            workContractAcceptances: [],
            services: [OrderDetailService(id: "svc-standard", name: "Standard clean")],
            packages: [],
            extras: ["inside-fridge"],
            customerNotes: "Máme kočku Micku, je přátelská.",
            specialInstructions: nil,
            accessInstructions: "Budu doma. Zvonek NOVÁKOVÁ.",
            floor: "3",
            flat: "12",
            accessMode: "at_home",
            payment: OrderDetailPayment(
                subtotal: nil,
                total: nil,
                tierDiscount: nil,
                membershipDiscount: nil,
                promoDiscount: nil,
                creditApplied: nil,
                amountDue: nil,
                typeCode: PaymentTypeCode.card.rawValue,
                statusCode: PaymentStatusCode.paid.rawValue
            ),
            isAssignedToCurrentUser: true,
            hasAfterPhotos: false,
            lockoutReportedAt: nil,
            lockoutCallAttempts: nil,
            orderNotes: [],
            orderIssues: [],
            statusHistory: [],
            estimatedMinutes: 120
        )
    }

    struct OrderDetailContent_Previews: PreviewProvider {
        static var previews: some View {
            OrderDetailContent(order: .preview, checklistVM: .preview, notesVM: .preview, photosVM: .preview)
        }
    }
#endif
