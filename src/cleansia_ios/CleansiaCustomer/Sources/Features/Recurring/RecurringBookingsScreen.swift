import CleansiaCore
import SwiftUI

struct RecurringBookingsScreen: View {
    @StateObject private var vm: RecurringBookingsViewModel
    let onCreateNew: () -> Void
    let onEdit: (RecurringTemplate) -> Void
    let onSubscribePlus: () -> Void

    @State private var pendingDeleteId: String?

    init(
        repository: RecurringBookingRepository,
        membershipRepository: MembershipRepository,
        snackbar: SnackbarController,
        onCreateNew: @escaping () -> Void,
        onEdit: @escaping (RecurringTemplate) -> Void,
        onSubscribePlus: @escaping () -> Void
    ) {
        _vm = StateObject(wrappedValue: RecurringBookingsViewModel(
            repository: repository,
            membershipRepository: membershipRepository,
            snackbar: snackbar
        ))
        self.onCreateNew = onCreateNew
        self.onEdit = onEdit
        self.onSubscribePlus = onSubscribePlus
    }

    var body: some View {
        content
            .navigationTitle(L10n.Recurring.bookingsTitle)
            .navigationBarTitleDisplayMode(.inline)
            .background(CleansiaColors.background.ignoresSafeArea())
            .task { await vm.load() }
            .overlay(alignment: .bottom) {
                if vm.affordances.showCreateAction {
                    CleansiaPrimaryButton(L10n.Recurring.createFab, leadingIcon: "plus", action: onCreateNew)
                        .padding(Spacing.ml)
                }
            }
            .overlay { deleteDialog }
    }

    @ViewBuilder
    private var content: some View {
        if vm.affordances.showPlusUpsell {
            PlusGate(onSubscribe: onSubscribePlus)
        } else if vm.templates.isEmpty {
            RecurringEmptyState(onCreateNew: onCreateNew)
        } else {
            TemplateList(
                templates: vm.templates,
                mutatingId: vm.mutatingId,
                showLapsedNotice: vm.affordances.showLapsedNotice,
                showEdit: vm.affordances.showEdit,
                onEdit: onEdit,
                onToggle: { template in
                    Task {
                        await vm.toggleActive(templateId: template.id, currentlyActive: template.isActive)
                    }
                },
                onDelete: { pendingDeleteId = $0.id },
                onSubscribe: onSubscribePlus
            )
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

private struct TemplateList: View {
    let templates: [RecurringTemplate]
    let mutatingId: String?
    let showLapsedNotice: Bool
    let showEdit: Bool
    let onEdit: (RecurringTemplate) -> Void
    let onToggle: (RecurringTemplate) -> Void
    let onDelete: (RecurringTemplate) -> Void
    let onSubscribe: () -> Void

    var body: some View {
        ScrollView {
            VStack(spacing: Spacing.m) {
                if showLapsedNotice {
                    LapsedPlusNotice(onSubscribe: onSubscribe)
                }
                ForEach(templates) { template in
                    TemplateCard(
                        template: template,
                        isMutating: mutatingId == template.id,
                        showEdit: showEdit,
                        onEdit: { onEdit(template) },
                        onToggle: { onToggle(template) },
                        onDelete: { onDelete(template) }
                    )
                }
                Color.clear.frame(height: Spacing.xxl)
            }
            .padding(.horizontal, Spacing.ml)
            .padding(.top, Spacing.m)
        }
    }
}

/// The disclosure a lapsed subscriber is owed: the schedules below keep running and keep
/// being booked at the full non-member price, and every card's pause and delete still work.
private struct LapsedPlusNotice: View {
    let onSubscribe: () -> Void

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.s) {
            Text(L10n.Recurring.lapsedNoticeTitle)
                .font(CleansiaTypography.titleMedium)
                .foregroundColor(CleansiaColors.onSurface)
            Text(L10n.Recurring.lapsedNoticeBody)
                .font(CleansiaTypography.bodyMedium)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
            CleansiaTextLink(L10n.Recurring.plusGateCta, action: onSubscribe)
        }
        .padding(Spacing.m)
        .frame(maxWidth: .infinity, alignment: .leading)
        .background(CleansiaColors.surfaceVariant, in: RoundedRectangle(cornerRadius: CornerRadius.medium))
    }
}

/// What a customer without Plus gets *instead of* the create affordance. It replaces the
/// empty state only — a customer who already has schedules gets `LapsedPlusNotice` above
/// the live list, never in place of it.
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
    @Environment(\.locale) private var locale
    let template: RecurringTemplate
    let isMutating: Bool
    let showEdit: Bool
    let onEdit: () -> Void
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
            Text(L10n.Recurring.dayAtTime(
                RecurringWeekday.label(template.dayOfWeek, locale: locale),
                template.timeOfDay
            ))
            .font(CleansiaTypography.bodyMedium)
            .foregroundColor(CleansiaColors.onSurfaceVariant)
            if let addressLine = template.addressLine, !addressLine.isEmpty {
                Text(addressLine)
                    .font(CleansiaTypography.bodyMedium)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
            }
            // A flow, not an HStack: three labelled actions in cs/sk/uk/ru overflow a 375pt card and
            // would otherwise truncate rather than wrap.
            ChipFlow(spacing: Spacing.m) {
                if showEdit {
                    CardAction(
                        label: L10n.Recurring.edit,
                        systemImage: "square.and.pencil",
                        tint: CleansiaColors.primary,
                        disabled: isMutating,
                        action: onEdit
                    )
                }
                CardAction(
                    label: template.isActive ? L10n.Recurring.pause : L10n.Recurring.resume,
                    systemImage: template.isActive ? "pause.circle" : "play.circle",
                    tint: CleansiaColors.primary,
                    disabled: isMutating,
                    action: onToggle
                )
                CardAction(
                    label: L10n.Recurring.delete,
                    systemImage: "trash",
                    tint: CleansiaColors.error,
                    disabled: isMutating,
                    action: onDelete
                )
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

private struct CardAction: View {
    let label: String
    let systemImage: String
    let tint: Color
    let disabled: Bool
    let action: () -> Void

    var body: some View {
        Button(action: action) {
            Label(label, systemImage: systemImage)
                .font(CleansiaTypography.labelLarge)
                .foregroundColor(tint)
        }
        .buttonStyle(.plain)
        .disabled(disabled)
    }
}

/// Backend `dayOfWeek` follows .NET `DayOfWeek` (Sun=0..Sat=6), which is exactly the index order of
/// Foundation's `weekdaySymbols`. Symbols come from the passed locale, not the device's, so a Ukrainian
/// user on an English phone still reads Ukrainian day names.
enum RecurringWeekday {
    static func label(_ dotNetDay: Int, locale: Locale) -> String {
        symbols(locale, \.weekdaySymbols)[safe: dotNetDay] ?? ""
    }

    static func shortLabel(_ dotNetDay: Int, locale: Locale) -> String {
        symbols(locale, \.shortWeekdaySymbols)[safe: dotNetDay] ?? ""
    }

    private static func symbols(_ locale: Locale, _ keyPath: KeyPath<DateFormatter, [String]>) -> [String] {
        let formatter = DateFormatter()
        formatter.locale = locale
        return formatter[keyPath: keyPath]
    }
}

private extension Array {
    subscript(safe index: Int) -> Element? {
        indices.contains(index) ? self[index] : nil
    }
}
