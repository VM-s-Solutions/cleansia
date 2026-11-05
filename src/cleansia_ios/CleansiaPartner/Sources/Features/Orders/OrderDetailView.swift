import CleansiaCore
import CleansiaPartnerApi
import SwiftUI

/// The job detail. With the board's row for this id it draws the job at once and fills the rest when `GetById`
/// answers; with none (a push, the Dashboard) it holds every value as a flat bar until then. Opened from the removal
/// notice for a job she is off, it is the page that says so, in place of the job.
struct OrderDetailView: View {
    @Environment(\.dismiss) private var dismiss
    @StateObject private var vm: OrderDetailViewModel
    @StateObject private var checklistVM: CleaningChecklistViewModel
    @StateObject private var notesVM: OrderNotesViewModel
    @StateObject private var photosVM: OrderPhotosViewModel
    private let client: PartnerOrderClient
    private let onOpenLegalDocuments: () -> Void
    private let onOpenJob: ((String) -> Void)?
    private let onBrowseJobs: (() -> Void)?
    private let onOpenEarnings: (() -> Void)?

    init(
        orderId: String,
        client: PartnerOrderClient,
        staleness: OrdersStaleness,
        checklistStore: CleaningChecklistStore,
        snackbar: SnackbarController,
        pendingOffers: PendingOffersStore,
        boardStore: OrdersBoardStore? = nil,
        showRemovalReason: Bool = false,
        onOpenLegalDocuments: @escaping () -> Void,
        onOpenJob: ((String) -> Void)? = nil,
        onBrowseJobs: (() -> Void)? = nil,
        onOpenEarnings: (() -> Void)? = nil,
        now: @escaping () -> Date = { Date() }
    ) {
        _vm = StateObject(
            wrappedValue: OrderDetailViewModel(
                orderId: orderId,
                client: client,
                staleness: staleness,
                snackbar: snackbar,
                pendingOffers: pendingOffers,
                boardStore: boardStore,
                showRemovalReason: showRemovalReason,
                now: now
            )
        )
        _checklistVM = StateObject(
            wrappedValue: CleaningChecklistViewModel(orderId: orderId, store: checklistStore)
        )
        _notesVM = StateObject(
            wrappedValue: OrderNotesViewModel(orderId: orderId, client: client, snackbar: snackbar)
        )
        _photosVM = StateObject(
            wrappedValue: OrderPhotosViewModel(orderId: orderId, client: client, snackbar: snackbar)
        )
        self.client = client
        self.onOpenLegalDocuments = onOpenLegalDocuments
        self.onOpenJob = onOpenJob
        self.onBrowseJobs = onBrowseJobs
        self.onOpenEarnings = onOpenEarnings
    }

    var body: some View {
        TimelineView(.periodic(from: .now, by: 30)) { _ in
            content(now: vm.now())
        }
        // The page draws its own back control in the band. Not `.navigationBarBackButtonHidden`: the swipe-back
        // delegate refuses a screen that hides its back button.
        .toolbar(.hidden, for: .navigationBar)
        .toolbar(.hidden, for: .tabBar)
        .background(InteractivePopGestureEnabler())
        .sheet(item: contractRequest) { request in
            WorkContractSheet(
                request: request,
                client: client,
                taken: vm.takenJob,
                endsOnTakenPanel: true,
                onDismiss: vm.dismissContract,
                onOutcome: { outcome in Task { await vm.onWorkContractOutcome(outcome) } },
                onOpenLegalDocuments: {
                    vm.dismissContract()
                    onOpenLegalDocuments()
                },
                onOpenJob: vm.dismissContract,
                onBackToJobs: {
                    vm.dismissContract()
                    if let onBrowseJobs { onBrowseJobs() } else { dismiss() }
                }
            )
        }
        .task { await vm.load() }
        .task(id: vm.canReadPhotos) { await photosVM.load(isAuthorized: vm.canReadPhotos) }
        .onReceive(notesVM.mutated) { Task { await vm.load() } }
        .onReceive(photosVM.mutated) { Task { await vm.load() } }
        .onReceive(vm.left) { dismiss() }
    }

    private var contractRequest: Binding<WorkContractRequest?> {
        Binding(
            get: { vm.contractRequest },
            set: { if $0 == nil { vm.dismissContract() } }
        )
    }

    @ViewBuilder
    private func content(now: Date) -> some View {
        if let removal = vm.removal {
            RemovalPage(
                page: removal,
                now: now,
                onCheckAgain: { Task { await vm.checkAgain() } },
                onOpenJob: onOpenJob,
                onBrowseJobs: onBrowseJobs
            )
        } else {
            switch vm.state {
            case .loading:
                if let row = vm.rowDetail {
                    page(row, now: now, rest: .loading)
                } else {
                    NoRowSkeleton()
                }
            case .error:
                if let row = vm.rowDetail {
                    page(row, now: now, rest: .failed)
                } else {
                    NoRowFailure(
                        isGone: vm.isGone,
                        now: now,
                        ownRows: vm.storedOwnRows,
                        openRows: vm.storedOpenRows,
                        openTotal: vm.storedOpenTotal,
                        onRetry: { Task { await vm.load() } },
                        onOpenJob: onOpenJob,
                        onBrowseJobs: onBrowseJobs
                    )
                }
            case let .loaded(order):
                page(order, now: now, rest: .loaded)
            }
        }
    }

    private func page(_ order: OrderDetail, now: Date, rest: OrderDetailContent.Rest) -> some View {
        let loaded = rest == .loaded
        let rowMove = OrderPrimaryAction.fromRow(order, loading: rest == .loading, now: now)
        return OrderDetailContent(
            order: order,
            now: now,
            photos: photosVM.state.loadedValue ?? [],
            myEmployeeId: vm.myEmployeeId,
            band: vm.band,
            ownRows: vm.ownRows?.loadedValue ?? vm.storedOwnRows,
            openRows: vm.storedOpenRows,
            openTotal: vm.storedOpenTotal,
            hasBoardRow: vm.boardRow != nil,
            clash: vm.clashingJob,
            rest: rest,
            primaryAction: loaded ? vm.primaryAction : rowMove ?? .none,
            holdsMoveSlot: !loaded && rowMove == nil,
            offersCashNotPaid: loaded && vm.offersCashNotPaid,
            offersStartFromConfirmed: loaded && vm.offersStartFromConfirmed,
            inFlightAction: vm.inFlightAction,
            preferredOffer: vm.preferredOffer,
            refusal: vm.refusal,
            contractStanding: loaded ? vm.contractStanding : .none,
            canDropOrder: loaded && vm.canDropOrder,
            onConfirm: { action in Task { await vm.dispatch(action) } },
            onOpenContract: vm.openContract,
            onDeclineOffer: { Task { await vm.declinePreferredOffer() } },
            onDismissRefusal: vm.dismissActionError,
            onReportLockout: { note in Task { await vm.reportLockout(note) } },
            onReportCashNotPaid: { Task { await vm.reportCashNotPaid() } },
            onDropOrder: { Task { await vm.dropOrder() } },
            onRetry: { Task { await vm.load() } },
            onOpenJob: onOpenJob,
            onBrowseJobs: onBrowseJobs,
            onOpenEarnings: onOpenEarnings,
            checklistVM: checklistVM,
            notesVM: notesVM,
            photosVM: photosVM
        )
    }
}
