import CleansiaCore
import SwiftUI

struct RecurringBookingsScreen: View {
    @StateObject private var vm: RecurringBookingsViewModel
    let onCreateNew: () -> Void
    let onSubscribePlus: () -> Void

    @State private var pendingDeleteId: String?

    init(
        repository: RecurringBookingRepository,
        membershipRepository: MembershipRepository,
        snackbar: SnackbarController,
        onCreateNew: @escaping () -> Void,
        onSubscribePlus: @escaping () -> Void
    ) {
        _vm = StateObject(wrappedValue: RecurringBookingsViewModel(
            repository: repository,
            membershipRepository: membershipRepository,
            snackbar: snackbar
        ))
        self.onCreateNew = onCreateNew
        self.onSubscribePlus = onSubscribePlus
    }

    var body: some View {
        content
            .navigationTitle(L10n.Recurring.bookingsTitle)
            .navigationBarTitleDisplayMode(.inline)
            .background(CleansiaColors.background.ignoresSafeArea())
            .task { await vm.load() }
            .overlay(alignment: .bottom) {
                if vm.isPlusMember, !vm.templates.isEmpty {
                    CleansiaPrimaryButton(L10n.Recurring.createFab, leadingIcon: "plus", action: onCreateNew)
                        .padding(Spacing.ml)
                }
            }
            .overlay { deleteDialog }
    }

    @ViewBuilder
    private var content: some View {
        if !vm.isPlusMember {
            PlusGate(onSubscribe: onSubscribePlus)
        } else if vm.templates.isEmpty {
            RecurringEmptyState(onCreateNew: onCreateNew)
        } else {
            ScrollView {
                VStack(spacing: Spacing.m) {
                    ForEach(vm.templates) { template in
                        TemplateCard(
                            template: template,
                            isMutating: vm.mutatingId == template.id,
                            onToggle: {
                                Task {
                                    await vm.toggleActive(templateId: template.id, currentlyActive: template.isActive)
                                }
                            },
                            onDelete: { pendingDeleteId = template.id }
                        )
                    }
                    Color.clear.frame(height: Spacing.xxl)
                }
                .padding(.horizontal, Spacing.ml)
                .padding(.top, Spacing.m)
            }
            .refreshable { await vm.load() }
        }
    }

    @ViewBuilder
    private var deleteDialog: some View {
        if let id = pendingDeleteId {
            CleansiaDialog(
                title: L10n.Recurring.deleteDialogTitle,
                confirmLabel: L10n.Recurring.deleteDialogConfirm,
                onConfirm: {
                    pendingDeleteId = nil
                    Task { await vm.delete(templateId: id) }
                },
                onDismiss: { pendingDeleteId = nil },
                message: L10n.Recurring.deleteDialogWhatStops + "\n\n"
                    + L10n.Recurring.deleteDialogWhatStays + "\n\n"
                    + L10n.Recurring.deleteDialogPauseHint,
                dismissLabel: L10n.Recurring.back,
                destructive: true
            )
        }
    }
}

private struct PlusGate: View {
    let onSubscribe: () -> Void

    var body: some View {
        VStack(spacing: Spacing.m) {
            Spacer()
            Image(systemName: "repeat.circle")
                .font(.system(size: 56))
                .foregroundColor(CleansiaColors.primary)
            Text(L10n.Recurring.plusGateTitle)
                .font(CleansiaTypography.titleLarge)
                .foregroundColor(CleansiaColors.onBackground)
                .multilineTextAlignment(.center)
            Text(L10n.Recurring.plusGateSubtitle)
                .font(CleansiaTypography.bodyMedium)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
                .multilineTextAlignment(.center)
            CleansiaPrimaryButton(L10n.Recurring.plusGateCta, leadingIcon: "crown", action: onSubscribe)
                .fixedSize()
            Spacer()
        }
        .padding(Spacing.xl)
        .frame(maxWidth: .infinity, maxHeight: .infinity)
    }
}

private struct RecurringEmptyState: View {
    let onCreateNew: () -> Void

    var body: some View {
        VStack(spacing: Spacing.m) {
            Spacer()
            Image(systemName: "calendar.badge.clock")
                .font(.system(size: 56))
                .foregroundColor(CleansiaColors.onSurfaceVariant)
            Text(L10n.Recurring.emptyTitle)
                .font(CleansiaTypography.titleLarge)
                .foregroundColor(CleansiaColors.onBackground)
            Text(L10n.Recurring.emptySubtitle)
                .font(CleansiaTypography.bodyMedium)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
                .multilineTextAlignment(.center)
            CleansiaPrimaryButton(L10n.Recurring.emptyCta, leadingIcon: "plus", action: onCreateNew)
                .fixedSize()
            Spacer()
        }
        .padding(Spacing.xl)
        .frame(maxWidth: .infinity, maxHeight: .infinity)
    }
}

private struct TemplateCard: View {
    let template: RecurringTemplate
    let isMutating: Bool
    let onToggle: () -> Void
    let onDelete: () -> Void

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.s) {
            HStack {
                Text(L10n.Recurring.cadence(template.frequency))
                    .font(CleansiaTypography.titleMedium)
                    .foregroundColor(CleansiaColors.onSurface)
                Spacer()
                if !template.isActive {
                    Text(L10n.Recurring.pausedBadge)
                        .font(CleansiaTypography.labelSmall)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                        .padding(.horizontal, Spacing.s)
                        .padding(.vertical, 3)
                        .background(CleansiaColors.surfaceVariant, in: Capsule())
                }
            }
            Text(L10n.Recurring.dayAtTime(RecurringWeekday.label(template.dayOfWeek), template.timeOfDay))
                .font(CleansiaTypography.bodyMedium)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
            if let addressLine = template.addressLine, !addressLine.isEmpty {
                Text(addressLine)
                    .font(CleansiaTypography.bodyMedium)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
            }
            HStack(spacing: Spacing.m) {
                Button(action: onToggle) {
                    Label(
                        template.isActive ? L10n.Recurring.pause : L10n.Recurring.resume,
                        systemImage: template.isActive ? "pause.circle" : "play.circle"
                    )
                    .font(CleansiaTypography.labelLarge)
                    .foregroundColor(CleansiaColors.primary)
                }
                .disabled(isMutating)
                Spacer()
                Button(action: onDelete) {
                    Label(L10n.Recurring.delete, systemImage: "trash")
                        .font(CleansiaTypography.labelLarge)
                        .foregroundColor(CleansiaColors.error)
                }
                .disabled(isMutating)
            }
            .padding(.top, Spacing.xs)
        }
        .padding(Spacing.m)
        .frame(maxWidth: .infinity, alignment: .leading)
        .background(CleansiaColors.surface, in: RoundedRectangle(cornerRadius: CornerRadius.medium))
        .overlay(
            RoundedRectangle(cornerRadius: CornerRadius.medium)
                .stroke(CleansiaColors.outlineVariant, lineWidth: 1)
        )
    }
}

enum RecurringWeekday {
    static func label(_ dotNetDay: Int) -> String {
        var components = DateComponents()
        components.weekday = (dotNetDay % 7) + 1
        let calendar = Calendar.current
        guard let date = calendar.nextDate(after: Date(), matching: components, matchingPolicy: .nextTime) else {
            return ""
        }
        let formatter = DateFormatter()
        formatter.dateFormat = "EEEE"
        return formatter.string(from: date)
    }
}
