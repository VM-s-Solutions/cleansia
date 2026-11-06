import CleansiaCore
import CleansiaPartnerApi
import SwiftUI

/// One step of an own job's run: Setting off, Getting in, Cleaning, Finishing. A done step carries its stamp, the
/// current one its time, one to come when it comes.
struct RunSheetStep: Equatable {
    enum Kind: CaseIterable {
        case setOff
        case getIn
        case clean
        case finish
    }

    enum Mark: Equatable {
        case done
        case current
        case toCome
    }

    let kind: Kind
    let mark: Mark
    var time: String?
    var spokenTime: String?
}

extension OrderDetail {
    /// The four steps of an own job from Confirmed to Completed; none for a job not hers or cancelled. The current
    /// step is the status line's (`OrdersBoardRules.runStep`): Cleaning closes at the first after photo on a solo job
    /// and at her own mark on a crew job.
    func runSheet(
        myEmployeeId: String?,
        photos: [OrderPhoto],
        now: Date,
        locale: Locale,
        calendar: Calendar = .current
    ) -> [RunSheetStep] {
        guard isAssignedToCurrentUser, let start = cleaningDateTime, let end = bookedEnd,
              let step = runFacts(myEmployeeId: myEmployeeId, photos: photos).flatMap(OrdersBoardRules.runStep),
              step != .cancelled
        else { return [] }
        let time = { OrdersFormat.timeOnly($0, locale: locale) }
        let order: [OrdersBoardRules.RunStep] = [.setOff, .getIn, .clean, .finish, .done]
        let reached = order.firstIndex(of: step) ?? 0
        let mark = { (index: Int) -> RunSheetStep.Mark in
            index < reached ? .done : index == reached ? .current : .toCome
        }
        let afterPhoto = photos.filter { $0.photoType == ._2 }.compactMap(\.capturedAt).min()
        let cleaningClosed = isCrewJob ? ownSeat(myEmployeeId)?.completionConfirmedAt ?? afterPhoto : afterPhoto
        let opensAt = start.addingTimeInterval(-TimeInterval(OrdersBoardRules.startGraceWindowMinutes) * 60)
        let setOffTime = if !calendar.isDate(start, inSameDayAs: now) {
            L10n.Orders.runDayFrom(OrdersFormat.pattern("EEE", start, locale: locale), time(opensAt))
        } else if now >= start {
            L10n.Orders.heroDue(time(start))
        } else {
            L10n.Orders.runFrom(time(opensAt))
        }
        return [
            stamped(.setOff, mark(0), firstStamp(._3), current: setOffTime, time: time),
            stamped(.getIn, mark(1), firstStamp(._4), current: L10n.Orders.runAt(time(start)), time: time),
            stamped(
                .clean,
                mark(2),
                cleaningClosed,
                current: OrdersBoardRules.window(start: start, minutes: estimatedMinutes, locale: locale) ?? "",
                time: time
            ),
            stamped(.finish, mark(3), finishedAt, current: L10n.Orders.runBy(time(end)), time: time)
        ]
    }

    /// A done step shows its stamp; the current one and one to come say when. On a crew job the on-the-way and
    /// start stamps are the order's, maybe a crew-mate's, so they read "job 14:12".
    private func stamped(
        _ kind: RunSheetStep.Kind,
        _ mark: RunSheetStep.Mark,
        _ stamp: Date?,
        current: String,
        time: (Date) -> String
    ) -> RunSheetStep {
        guard mark == .done else { return RunSheetStep(kind: kind, mark: mark, time: current) }
        guard let stamp else { return RunSheetStep(kind: kind, mark: mark) }
        return isCrewJob && (kind == .setOff || kind == .getIn)
            ? RunSheetStep(
                kind: kind,
                mark: mark,
                time: L10n.Orders.jobStamp(time(stamp)),
                spokenTime: L10n.Orders.jobStampSpoken(time(stamp))
            )
            : RunSheetStep(
                kind: kind,
                mark: mark,
                time: time(stamp),
                spokenTime: L10n.Orders.stepDoneAt(time(stamp))
            )
    }
}

/// The run sheet, "How the job goes": Setting off, Getting in, Cleaning, Finishing as a timetable, each step on its
/// point and its time, joined by a line; the current step open with what it needs and each step to come saying what
/// it will. VoiceOver reads it as one list, a heading a step.
struct JobRunSheet<StepContent: View>: View {
    let steps: [RunSheetStep]
    @ViewBuilder let content: (RunSheetStep) -> StepContent

    var body: some View {
        DetailSection(title: L10n.Orders.runSpoken, icon: "list.bullet") {
            VStack(alignment: .leading, spacing: 0) {
                ForEach(steps.indices, id: \.self) { index in
                    row(steps[index], last: index == steps.count - 1)
                }
            }
            .accessibilityElement(children: .contain)
            .accessibilityLabel(L10n.Orders.runSpoken)
        }
    }

    /// The point sits on the cap band of the name's first line, and the line under it runs to 4 above the next one.
    private func row(_ step: RunSheetStep, last: Bool) -> some View {
        let style = Self.nameStyle(step.mark)
        return HStack(alignment: .firstTextBaseline, spacing: Spacing.s) {
            VStack(spacing: Spacing.xxs) {
                OrderTimelinePoint(segment: Self.segment(step.mark)).capBandAligned(of: style)
                if !last {
                    Capsule()
                        .fill(CleansiaColors.outlineVariant)
                        .frame(width: 2)
                        .frame(maxHeight: .infinity)
                        .padding(.bottom, Spacing.xxs)
                }
            }
            .frame(width: 24)
            VStack(alignment: .leading, spacing: 0) {
                top(step, style: style)
                content(step)
            }
            .frame(maxWidth: .infinity, alignment: .leading)
            .padding(.bottom, last ? 0 : Spacing.ml)
        }
        .fixedSize(horizontal: false, vertical: true)
    }

    private func top(_ step: RunSheetStep, style: CleansiaTextStyle) -> some View {
        ViewThatFits(in: .horizontal) {
            HStack(alignment: .firstTextBaseline, spacing: Spacing.xs) {
                name(step, style: style)
                Spacer(minLength: Spacing.xs)
                time(step)
            }
            VStack(alignment: .trailing, spacing: Spacing.hair) {
                name(step, style: style).frame(maxWidth: .infinity, alignment: .leading)
                time(step)
            }
        }
        .accessibilityElement(children: .ignore)
        .accessibilityLabel([Self.name(step.kind), step.spokenTime ?? step.time].compactMap { $0 }
            .joined(separator: ", "))
        .accessibilityAddTraits(.isHeader)
    }

    private func name(_ step: RunSheetStep, style: CleansiaTextStyle) -> some View {
        Text(Self.name(step.kind))
            .cleansiaFont(style)
            .foregroundColor(step.mark == .done ? CleansiaColors.onSurfaceVariant : CleansiaColors.onSurface)
            .fixedSize(horizontal: false, vertical: true)
    }

    @ViewBuilder
    private func time(_ step: RunSheetStep) -> some View {
        if let time = step.time {
            Text(time)
                .font(CleansiaTypography.bodyMedium)
                .monospacedDigit()
                .foregroundColor(step.mark == .current ? CleansiaColors.primaryText : CleansiaColors.onSurfaceVariant)
                .multilineTextAlignment(.trailing)
                .fixedSize(horizontal: false, vertical: true)
        }
    }

    /// The current step's name is the sheet's one emphasised run; the same 16, so nothing reflows when the step moves.
    static func nameStyle(_ mark: RunSheetStep.Mark) -> CleansiaTextStyle {
        mark == .current ? CleansiaTypography.scale.titleLarge : CleansiaTypography.scale.bodyLarge
    }

    static func segment(_ mark: RunSheetStep.Mark) -> OrderTrackerSegment {
        switch mark {
        case .done: .past
        case .current: .current
        case .toCome: .future
        }
    }

    static func name(_ kind: RunSheetStep.Kind) -> String {
        switch kind {
        case .setOff: L10n.Orders.runStepSetOff
        case .getIn: L10n.Orders.runStepGetIn
        case .clean: L10n.Orders.runStepClean
        case .finish: L10n.Orders.runStepFinish
        }
    }
}

/// What a step says under its name: what a step to come will need, 2 under it; the current step opened with what it
/// needs now, 12 under it, its blocks 20 apart. A done step says nothing more than its stamp, but for the Cleaning a
/// finished job keeps: her photos, and while the customer's details are open her notes and issues, read-only.
struct RunStepContent: View {
    @Environment(\.locale) private var locale
    let step: RunSheetStep
    let order: OrderDetail
    let now: Date
    let photos: [OrderPhoto]
    let myEmployeeId: String?
    var rest = OrderDetailContent.Rest.loaded
    let isReporting: Bool
    let actionsEnabled: Bool
    @Binding var afterPhotoRequested: Bool
    let onReportLockout: (String) -> Void
    @ObservedObject var checklistVM: CleaningChecklistViewModel
    @ObservedObject var notesVM: OrderNotesViewModel
    @ObservedObject var photosVM: OrderPhotosViewModel

    private var firstAfterPhoto: Date? {
        photos.filter { $0.photoType == ._2 }.compactMap(\.capturedAt).min()
    }

    var body: some View {
        switch (step.kind, step.mark) {
        case (.setOff, .current): open { reachCustomer }
        case (.getIn, .toCome): gettingInLater.padding(.top, Spacing.hair)
        case (.getIn, .current): open { gettingInNow }
        case (.clean, .current): open { cleaning }
        case (.clean, .done) where order.status == ._5: open { record }
        case (.finish, .toCome): quiet([finishLine]).padding(.top, Spacing.hair)
        case (.finish, .current): open { finishing }
        default: EmptyView()
        }
    }

    private func open(@ViewBuilder _ blocks: () -> some View) -> some View {
        VStack(alignment: .leading, spacing: Spacing.ml) {
            blocks()
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .padding(.top, Spacing.s)
    }

    private func quiet(_ items: [String], tiesLast: Bool = false) -> some View {
        OrdersDotFlow(items: items.map { Text($0) }, style: CleansiaTypography.scale.bodyMedium, tiesLast: tiesLast)
            .foregroundColor(CleansiaColors.onSurfaceVariant)
    }

    @ViewBuilder
    private var reachCustomer: some View {
        CustomerLine(order: order)
        ContactPills(
            address: order.location.navigationTarget?.line,
            coordinate: order.location.navigationTarget?.coordinate,
            phone: order.customerPhone,
            navigates: order.location.navigationTarget != nil
        )
    }

    @ViewBuilder
    private var gettingInLater: some View {
        switch rest {
        case .loading:
            VStack(alignment: .leading, spacing: 6) {
                OrdersBar(width: 230, height: 16)
                OrdersBar(width: 250, height: 16)
            }
        case .failed:
            Text(verbatim: "–")
                .font(CleansiaTypography.bodyMedium)
                .foregroundColor(CleansiaColors.quiet)
        case .loaded:
            VStack(alignment: .leading, spacing: Spacing.hair) {
                if order.showsAccessFacts, !order.accessFacts.isEmpty { quiet(order.accessFacts) }
                OrdersCaption(text: L10n.Orders.accessShownOnSetOff)
                lockout(showsLine: order.showsLockoutLine(now: now))
            }
        }
    }

    @ViewBuilder
    private var gettingInNow: some View {
        let standing = order.lockoutStanding(now: now)
        let blockFirst = standing == .open || {
            if case .reported = standing { return true }
            return false
        }()
        if blockFirst { lockout(showsLine: true) }
        AccessBlock(order: order)
        reachCustomer
        if !blockFirst { lockout(showsLine: order.showsLockoutLine(now: now)) }
    }

    private func lockout(showsLine: Bool) -> some View {
        LockoutCard(
            order: order,
            now: now,
            isReporting: isReporting,
            actionsEnabled: actionsEnabled,
            photosVM: photosVM,
            onReport: onReportLockout,
            showsLine: showsLine
        )
    }

    private var finishLine: String {
        if order.isCrewJob { return L10n.Orders.finishNeedsCrew }
        if order.payment.isCash { return L10n.Orders.completeBlockedCashSequence }
        return L10n.Orders.finishNeedsPhoto
    }

    @ViewBuilder
    private var cleaning: some View {
        if order.isCrewJob, order.mateMarked(myEmployeeId) {
            CrewGate(order: order, myEmployeeId: myEmployeeId, afterPhoto: firstAfterPhoto)
            photosBlock(slot: false)
        } else {
            CleaningChecklistView(
                order: order,
                checkedIds: checklistVM.checkedIds,
                interactive: order.status == ._4,
                onToggle: checklistVM.setChecked
            )
            photosBlock(slot: true)
            if order.showsNotesAndIssues {
                NotesAndIssuesSection(
                    notes: order.orderNotes,
                    issues: order.orderIssues,
                    canAdd: order.canAddNotes,
                    isReadOnly: false,
                    vm: notesVM
                )
            }
        }
    }

    @ViewBuilder
    private var finishing: some View {
        if order.isCrewJob {
            CrewGate(order: order, myEmployeeId: myEmployeeId, afterPhoto: firstAfterPhoto)
            photosBlock(slot: false)
        } else if order.payment.isCash {
            CashBlock(order: order, afterPhoto: firstAfterPhoto)
        } else {
            VStack(alignment: .leading, spacing: 0) {
                GateRow(title: L10n.Orders.finishAfterPhoto, done: true, stamp: firstAfterPhoto)
                if order.payment.isCardNotCharged {
                    OrdersCaption(text: L10n.Orders.paymentNotConfirmed).padding(.leading, 48)
                }
            }
            CleaningChecklistView(
                order: order,
                checkedIds: checklistVM.checkedIds,
                interactive: order.status == ._4,
                onToggle: checklistVM.setChecked
            )
            photosBlock(slot: false)
        }
    }

    /// The work's record on a finished job, under the step that made it.
    @ViewBuilder
    private var record: some View {
        photosBlock(slot: false)
        if order.customerDetailsClosedNote == nil, !order.orderNotes.isEmpty || !order.orderIssues.isEmpty {
            NotesAndIssuesSection(
                notes: order.orderNotes,
                issues: order.orderIssues,
                canAdd: false,
                isReadOnly: true,
                vm: notesVM
            )
        }
    }

    private func photosBlock(slot: Bool) -> some View {
        DetailPhotos(
            photos: photos,
            canUploadBefore: order.photoWindowOpen(for: ._1),
            canUploadAfter: order.photoWindowOpen(for: ._2),
            afterSlotNote: slot,
            afterPhotoRequested: $afterPhotoRequested,
            photosVM: photosVM
        )
    }
}
