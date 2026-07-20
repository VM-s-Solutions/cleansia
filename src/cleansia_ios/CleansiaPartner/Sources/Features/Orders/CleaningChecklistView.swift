import CleansiaCore
import SwiftUI

struct ChecklistItem: Identifiable, Equatable {
    let id: String
    let label: String
    let glyph: String?
}

struct ChecklistGroups: Equatable {
    let services: [ChecklistItem]
    let packages: [ChecklistItem]
    let extras: [ChecklistItem]

    var all: [ChecklistItem] {
        services + packages + extras
    }
}

enum ChecklistBuilder {
    static func items(for order: OrderDetail, locale: Locale) -> ChecklistGroups {
        // Keyed by the stable backend id (Android parity — CleaningChecklist.kt keys
        // by service.id/package.id) so persisted ticks survive a list reorder; the
        // name fallback for an id-less row stays the RAW snapshot name (never the
        // localized one) so ticks also survive a language switch.
        let services = order.services.map { svc in
            ChecklistItem(id: "service:\(svc.id ?? svc.name)", label: svc.localizedName(for: locale), glyph: nil)
        }
        let packages = order.packages.map { pkg in
            ChecklistItem(id: "package:\(pkg.id ?? pkg.name)", label: pkg.localizedName(for: locale), glyph: nil)
        }
        let extras = order.extras.map { slug in
            ChecklistItem(id: "extra:\(slug)", label: OrderExtras.name(slug), glyph: OrderExtras.emoji(slug))
        }
        return ChecklistGroups(services: services, packages: packages, extras: extras)
    }
}

/// The cleaner's tick-list. Rendered for any status the parent allows (mine &
/// Confirmed/OnTheWay/InProgress) but only INTERACTIVE once the job is actually
/// InProgress — preventing pre-checking before the work is real (the
/// `CleaningChecklist.kt` parity).
struct CleaningChecklistView: View {
    @Environment(\.locale) private var locale
    let order: OrderDetail
    let checkedIds: Set<String>
    let interactive: Bool
    let onToggle: (String, Bool) -> Void

    private var groups: ChecklistGroups {
        ChecklistBuilder.items(for: order, locale: locale)
    }

    private var allItems: [ChecklistItem] {
        groups.all
    }

    private var doneCount: Int {
        allItems.filter { checkedIds.contains($0.id) }.count
    }

    var body: some View {
        if !allItems.isEmpty {
            OrderSectionCard(title: L10n.Orders.checklistSectionTitle, systemImage: "checklist") {
                VStack(alignment: .leading, spacing: Spacing.s) {
                    progressRow
                    group(L10n.Orders.checklistServicesLabel, groups.services)
                    group(L10n.Orders.checklistPackagesLabel, groups.packages)
                    group(L10n.Orders.checklistExtrasLabel, groups.extras)
                }
            }
            .id(locale.identifier)
        }
    }

    private var allDone: Bool {
        doneCount == allItems.count
    }

    @ViewBuilder
    private var progressRow: some View {
        let progress = allItems.isEmpty ? 0 : Double(doneCount) / Double(allItems.count)
        VStack(alignment: .leading, spacing: Spacing.xs) {
            HStack {
                Text(L10n.Orders.checklistProgress(doneCount, allItems.count))
                    .font(CleansiaTypography.titleMedium)
                    .foregroundColor(allDone ? CleansiaColors.primary : CleansiaColors.onSurface)
                Spacer()
                if allDone {
                    Image(systemName: "checkmark.circle.fill")
                        .foregroundColor(CleansiaColors.primary)
                }
            }
            ProgressView(value: progress)
                .tint(CleansiaColors.primary)
            if !interactive {
                Text(L10n.Orders.checklistLockedHint)
                    .font(CleansiaTypography.labelSmall)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
            } else if allDone {
                Text(L10n.Orders.checklistAllDoneHint)
                    .font(CleansiaTypography.labelSmall)
                    .foregroundColor(CleansiaColors.primary)
            }
        }
    }

    @ViewBuilder
    private func group(_ label: String, _ items: [ChecklistItem]) -> some View {
        if !items.isEmpty {
            VStack(alignment: .leading, spacing: Spacing.xxs) {
                Text(label)
                    .font(CleansiaTypography.labelSmall)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
                ForEach(items) { item in
                    ChecklistRow(
                        item: item,
                        checked: checkedIds.contains(item.id),
                        interactive: interactive,
                        onToggle: { onToggle(item.id, $0) }
                    )
                }
            }
        }
    }
}

private struct ChecklistRow: View {
    let item: ChecklistItem
    let checked: Bool
    let interactive: Bool
    let onToggle: (Bool) -> Void

    var body: some View {
        Button { onToggle(!checked) } label: {
            HStack(spacing: Spacing.xs) {
                Image(systemName: checked ? "checkmark.square.fill" : "square")
                    .font(.system(size: 20))
                    .foregroundColor(checked ? CleansiaColors.primary : CleansiaColors.outline)
                if let glyph = item.glyph {
                    Text(glyph)
                }
                Text(item.label)
                    .font(CleansiaTypography.bodyMedium)
                    .foregroundColor(checked ? CleansiaColors.onSurfaceVariant : CleansiaColors.onSurface)
                    .strikethrough(checked)
                Spacer()
            }
            .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
        .disabled(!interactive)
    }
}
