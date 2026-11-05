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

/// The lockout inside the Getting-in step: before it opens the one line saying when it will, with *paid in full*;
/// from start + 15 the entrance photo and *I cannot get in*; once reported, the report and the calls as written. It
/// opens by the detail's clock, the one the run sheet orders its step by, which redraws the page as time passes.
struct LockoutCard: View {
    @Environment(\.locale) private var locale
    let order: OrderDetail
    let now: Date
    let isReporting: Bool
    let actionsEnabled: Bool
    let photosVM: OrderPhotosViewModel
    let onReport: (String) -> Void
    var showsLine = true

    var body: some View {
        switch order.lockoutStanding(now: now) {
        case .hidden:
            EmptyView()
        case let .notYet(opensAt):
            if showsLine {
                (Text(L10n.Orders.lockoutCardTitle).foregroundColor(CleansiaColors.onSurface)
                    + Text(" " + L10n.tie(L10n.Orders.lockoutCardNotYet(
                        OrdersFormat.timeOnly(opensAt, locale: locale),
                        LockoutStanding.waitMinutes
                    ) + " " + L10n.Orders.lockoutPaidInFull)))
                    .font(CleansiaTypography.bodyMedium)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
                    .fixedSize(horizontal: false, vertical: true)
            }
        case .open:
            OpenLockoutCard(
                isReporting: isReporting,
                actionsEnabled: actionsEnabled,
                photosVM: photosVM,
                onReport: onReport
            )
        case let .reported(reportedAt, callAttempts):
            VStack(alignment: .leading, spacing: Spacing.xs) {
                DetailEyebrow(text: L10n.Orders.lockoutReportedTitle)
                Text(L10n.tie(L10n.Orders.lockoutReportedBody(OrdersFormat.timeOnly(reportedAt, locale: locale))))
                    .font(CleansiaTypography.bodyMedium)
                    .foregroundColor(CleansiaColors.onSurface)
                    .fixedSize(horizontal: false, vertical: true)
                if let callAttempts, !callAttempts.isBlank {
                    (Text(L10n.Orders.lockoutReportedCalls("")) + Text(CustomerText.spoken(callAttempts)))
                        .font(CleansiaTypography.bodyMedium)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                        .fixedSize(horizontal: false, vertical: true)
                }
                EntrancePhotos(photosVM: photosVM, canUpload: false)
                Text(L10n.tie(L10n.Orders.lockoutPaidInFull))
                    .font(CleansiaTypography.bodyMedium)
                    .foregroundColor(CleansiaColors.onSurface)
                    .fixedSize(horizontal: false, vertical: true)
            }
            .lockoutBlock()
        }
    }
}

private struct OpenLockoutCard: View {
    let isReporting: Bool
    let actionsEnabled: Bool
    @ObservedObject var photosVM: OrderPhotosViewModel
    let onReport: (String) -> Void

    @State private var sheetOpen = false
    @State private var note = ""

    private var entrancePhotos: [OrderPhoto] {
        (photosVM.state.loadedValue ?? []).filter { $0.photoType == ._3 }
    }

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.xs) {
            DetailEyebrow(text: L10n.Orders.lockoutCardTitle)
            Text(L10n.tie("\(L10n.Orders.lockoutCardBody) \(L10n.Orders.lockoutPaidInFull)"))
                .font(CleansiaTypography.bodyMedium)
                .foregroundColor(CleansiaColors.onSurface)
                .fixedSize(horizontal: false, vertical: true)
            EntrancePhotos(photosVM: photosVM, canUpload: true)
            if entrancePhotos.isEmpty {
                OrdersCaption(text: L10n.Orders.lockoutPhotoNeeded)
            }
            if isReporting {
                ProgressView()
                    .tint(CleansiaColors.primaryText)
                    .frame(maxWidth: .infinity, minHeight: 48)
            } else {
                CleansiaOutlinedButton(
                    L10n.Orders.lockoutReportAction,
                    size: .medium,
                    contentColor: CleansiaColors.primaryText,
                    borderColor: CleansiaColors.primary,
                    enabled: !entrancePhotos.isEmpty && actionsEnabled
                ) {
                    sheetOpen = true
                }
            }
        }
        .lockoutBlock()
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

/// The entrance photos a lockout report rests on.
private struct EntrancePhotos: View {
    @ObservedObject var photosVM: OrderPhotosViewModel
    let canUpload: Bool
    @StateObject private var imageCache = RemoteImageCache()

    var body: some View {
        PhotoColumn(
            title: L10n.Orders.lockoutEntrancePhoto,
            type: ._3,
            photos: (photosVM.state.loadedValue ?? []).filter { $0.photoType == ._3 },
            canUpload: canUpload,
            mutation: photosVM.mutation,
            imageCache: imageCache,
            request: .constant(false),
            onPick: { type, image in Task { await photosVM.upload(type: type, image: image) } },
            onDelete: { id in Task { await photosVM.delete(photoId: id) } }
        )
    }
}

private extension View {
    /// The lockout leads its step, ruled off from the access and the customer under it.
    func lockoutBlock() -> some View {
        padding(.bottom, Spacing.m)
            .frame(maxWidth: .infinity, alignment: .leading)
            .overlay(alignment: .bottom) { Divider().overlay(CleansiaColors.outlineVariant) }
    }
}
