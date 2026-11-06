import CleansiaCore
import SwiftUI

struct ChecklistItem: Identifiable, Equatable {
    let id: String
    let label: String
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
            ChecklistItem(id: "service:\(svc.id ?? svc.name)", label: svc.localizedName(for: locale))
        }
        let packages = order.packages.map { pkg in
            ChecklistItem(id: "package:\(pkg.id ?? pkg.name)", label: pkg.localizedName(for: locale))
        }
        let extras = order.extras.map { slug in
            ChecklistItem(id: "extra:\(slug)", label: OrderExtras.name(slug))
        }
        return ChecklistGroups(services: services, packages: packages, extras: extras)
    }
}

/// The cleaner's tick-list, in the Cleaning step: the package with its rooms and baths, then each extra by its
/// catalogue name. Ticks live on this phone only, and the list says so; it is no gate on completing.
struct CleaningChecklistView: View {
    @Environment(\.locale) private var locale
    let order: OrderDetail
    let checkedIds: Set<String>
    let interactive: Bool
    let onToggle: (String, Bool) -> Void

    private var items: [ChecklistItem] {
        ChecklistBuilder.items(for: order, locale: locale).all
    }

    var body: some View {
        let items = items
        if !items.isEmpty {
            VStack(alignment: .leading, spacing: Spacing.xs) {
                HStack(alignment: .firstTextBaseline, spacing: Spacing.xs) {
                    DetailEyebrow(text: L10n.Orders.checklistSectionTitle)
                    Spacer(minLength: Spacing.xs)
                    Text(L10n.Orders.checklistProgress(items.filter { checkedIds.contains($0.id) }.count, items.count))
                        .font(CleansiaTypography.labelMedium)
                        .monospacedDigit()
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                        .fixedSize()
                }
                VStack(alignment: .leading, spacing: 0) {
                    ForEach(Array(items.enumerated()), id: \.element.id) { index, item in
                        ChecklistRow(
                            labels: index == 0 ? [item.label] + roomsAndBaths : [item.label],
                            checked: checkedIds.contains(item.id),
                            interactive: interactive,
                            onToggle: { onToggle(item.id, $0) }
                        )
                    }
                }
                OrdersCaption(text: L10n.Orders.checklistPhoneOnly)
            }
            .id(locale.identifier)
        }
    }

    private var roomsAndBaths: [String] {
        [
            order.rooms > 0 ? L10n.Orders.rooms(order.rooms) : nil,
            order.bathrooms > 0 ? L10n.Orders.baths(order.bathrooms) : nil
        ].compactMap { $0 }
    }
}

/// One line of the list: the Core box on the first line of its words, 44 high, no rule under it.
private struct ChecklistRow: View {
    let labels: [String]
    let checked: Bool
    let interactive: Bool
    let onToggle: (Bool) -> Void

    var body: some View {
        CleansiaCheckbox(
            checked: Binding(get: { checked }, set: { onToggle($0) }),
            enabled: interactive
        ) {
            OrdersDotFlow(items: labels.map { Text($0) }, style: CleansiaTypography.scale.bodyMedium, tiesLast: true)
                .frame(maxWidth: .infinity, alignment: .leading)
        }
        .frame(minHeight: 44)
        .accessibilityElement(children: .ignore)
        .accessibilityLabel(labels.joined(separator: ", "))
        .accessibilityAddTraits(checked ? [.isButton, .isSelected] : .isButton)
    }
}
