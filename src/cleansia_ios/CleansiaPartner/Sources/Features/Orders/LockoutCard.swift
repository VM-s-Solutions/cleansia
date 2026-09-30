import CleansiaCore
import CleansiaPartnerApi
import SwiftUI

/// Mirrors `ReportOrderLockout`: the crew of a Confirmed, on-the-way or in-progress job may report it
/// once, from `waitMinutes` past the booked start.
enum LockoutStanding: Equatable {
    case hidden
    case notYet(opensAt: Date)
    case open
    case reported(reportedAt: Date, callAttempts: String?)

    /// `BookingPolicy.LockoutWaitMinutes`.
    static let waitMinutes = 15

    /// `Task.sleep` runs on the uptime clock, which stops while the device sleeps, so the wait is walked in steps.
    static func clockStep(now: Date, opensAt: Date) -> TimeInterval {
        min(max(opensAt.timeIntervalSince(now), 0), 30)
    }
}

extension OrderDetail {
    func lockoutStanding(now: Date) -> LockoutStanding {
        guard showsWorkSections else { return .hidden }
        if let reportedAt = lockoutReportedAt {
            return .reported(reportedAt: reportedAt, callAttempts: lockoutCallAttempts)
        }
        guard let start = cleaningDateTime else { return .hidden }
        let opensAt = start.addingTimeInterval(TimeInterval(LockoutStanding.waitMinutes * 60))
        return now < opensAt ? .notYet(opensAt: opensAt) : .open
    }
}

struct LockoutCard: View {
    static let icon = "door.left.hand.closed"

    @Environment(\.locale) private var locale
    @Environment(\.scenePhase) private var scenePhase
    let order: OrderDetail
    let isReporting: Bool
    let actionsEnabled: Bool
    let photosVM: OrderPhotosViewModel
    let onReport: (String) -> Void

    @State private var now = Date()

    var body: some View {
        Group { standingCard }
            .onChange(of: scenePhase) { phase in
                if phase == .active { now = Date() }
            }
    }

    @ViewBuilder
    private var standingCard: some View {
        switch order.lockoutStanding(now: now) {
        case .hidden:
            EmptyView()
        case let .notYet(opensAt):
            OrderSectionCard(title: L10n.Orders.lockoutCardTitle, systemImage: Self.icon) {
                Text(L10n.Orders.lockoutCardNotYet(
                    OrdersFormat.timeOnly(opensAt, locale: locale),
                    LockoutStanding.waitMinutes
                ))
                .font(CleansiaTypography.bodyMedium)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
            }
            .task(id: opensAt) { await reopen(at: opensAt) }
        case .open:
            OpenLockoutCard(
                isReporting: isReporting,
                actionsEnabled: actionsEnabled,
                photosVM: photosVM,
                onReport: onReport
            )
        case let .reported(reportedAt, callAttempts):
            OrderSectionCard(title: L10n.Orders.lockoutReportedTitle, systemImage: Self.icon) {
                VStack(alignment: .leading, spacing: Spacing.s) {
                    Text(L10n.Orders.lockoutReportedBody(OrdersFormat.timeOnly(reportedAt, locale: locale)))
                        .font(CleansiaTypography.bodyMedium)
                        .foregroundColor(CleansiaColors.onSurface)
                    if let callAttempts, !callAttempts.isBlank {
                        Text(L10n.Orders.lockoutReportedCalls(callAttempts))
                            .font(CleansiaTypography.labelSmall)
                            .foregroundColor(CleansiaColors.onSurfaceVariant)
                    }
                }
            }
        }
    }

    @MainActor
    private func reopen(at opensAt: Date) async {
        while Date() < opensAt {
            let step = LockoutStanding.clockStep(now: Date(), opensAt: opensAt)
            try? await Task.sleep(nanoseconds: UInt64(step * 1_000_000_000))
            if Task.isCancelled { return }
        }
        now = Date()
    }
}

private struct OpenLockoutCard: View {
    let isReporting: Bool
    let actionsEnabled: Bool
    @ObservedObject var photosVM: OrderPhotosViewModel
    let onReport: (String) -> Void

    @StateObject private var imageCache = RemoteImageCache()
    @State private var sheetOpen = false
    @State private var note = ""

    private var entrancePhotos: [OrderPhoto] {
        (photosVM.state.loadedValue ?? []).filter { $0.photoType == ._3 }
    }

    var body: some View {
        OrderSectionCard(title: L10n.Orders.lockoutCardTitle, systemImage: LockoutCard.icon) {
            VStack(alignment: .leading, spacing: Spacing.m) {
                Text(L10n.Orders.lockoutCardBody)
                    .font(CleansiaTypography.bodyMedium)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
                PhotoRail(
                    title: L10n.Orders.lockoutEntrancePhoto,
                    type: ._3,
                    photos: entrancePhotos,
                    isReadOnly: false,
                    mutation: photosVM.mutation,
                    imageCache: imageCache,
                    onPick: { type, image in Task { await photosVM.upload(type: type, image: image) } },
                    onDelete: { id in Task { await photosVM.delete(photoId: id) } }
                )
                if entrancePhotos.isEmpty {
                    Text(L10n.Orders.lockoutPhotoNeeded)
                        .font(CleansiaTypography.labelSmall)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                }
                CleansiaOutlinedButton(
                    L10n.Orders.lockoutReportAction,
                    size: .medium,
                    leadingIcon: LockoutCard.icon,
                    enabled: !entrancePhotos.isEmpty && actionsEnabled && !isReporting
                ) {
                    sheetOpen = true
                }
            }
        }
        .sheet(isPresented: $sheetOpen) {
            LockoutReportSheet(
                note: $note,
                isReporting: isReporting,
                onClose: { sheetOpen = false },
                onConfirm: { text in
                    sheetOpen = false
                    onReport(text)
                }
            )
        }
    }
}

private struct LockoutReportSheet: View {
    @Binding var note: String
    let isReporting: Bool
    let onClose: () -> Void
    let onConfirm: (String) -> Void

    var body: some View {
        NavigationStack {
            VStack(spacing: Spacing.m) {
                Text(L10n.Orders.lockoutSheetDescription)
                    .font(CleansiaTypography.bodyMedium)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
                    .multilineTextAlignment(.center)
                CleansiaTextArea(
                    value: $note,
                    label: L10n.Orders.lockoutCallAttemptsLabel,
                    minHeight: 120,
                    enabled: !isReporting
                )
                CleansiaPrimaryButton(
                    L10n.Orders.save,
                    loading: isReporting,
                    enabled: !note.isBlank && !isReporting
                ) {
                    onConfirm(note)
                }
                Spacer()
            }
            .padding(Spacing.l)
            .navigationTitle(L10n.Orders.lockoutSheetTitle)
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .cancellationAction) {
                    Button(L10n.cancel, action: onClose)
                }
            }
            .background(CleansiaColors.background.ignoresSafeArea())
        }
        .presentationDetents([.medium, .large])
    }
}
