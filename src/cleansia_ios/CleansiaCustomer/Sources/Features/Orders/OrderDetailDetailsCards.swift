import CleansiaCore
import CleansiaCustomerApi
import SwiftUI

struct CleaningDetailsCard: View {
    @Environment(\.locale) private var locale
    let order: CustomerOrderDetail

    var body: some View {
        OrderCardSurface {
            OrderSectionHeaderRow(title: L10n.OrderDetail.sectionDetails)
            OrderInfoRow(
                label: L10n.OrderDetail.rooms,
                value: L10n.OrderDetail.roomsBathrooms(order.rooms, order.bathrooms)
            )
            OrderInfoRow(
                label: L10n.OrderDetail.estimated,
                value: order.estimatedMinutes > 0
                    ? L10n.OrderDetail.durationMinutes(order.estimatedMinutes)
                    : "—"
            )
            if let completedAt = order.completedAt {
                OrderInfoRow(
                    label: L10n.OrderDetail.completedAt,
                    value: OrdersFormat.dateTime(completedAt, locale: locale)
                )
            }
            if !order.activeExtras.isEmpty {
                Text(L10n.OrderDetail.extras)
                    .font(CleansiaTypography.labelMedium)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
                ExtrasFlow(keys: order.activeExtras)
            }
        }
    }
}

private struct ExtrasFlow: View {
    let keys: [String]

    var body: some View {
        FlexibleChips(items: keys.map(OrdersFormat.prettifyExtraKey))
    }
}

struct OrderServicesCard: View {
    @Environment(\.locale) private var locale
    let services: [CustomerOrderService]

    var body: some View {
        OrderCardSurface {
            OrderSectionHeaderRow(title: L10n.OrderDetail.servicesHeader)
            ForEach(Array(services.enumerated()), id: \.offset) { index, service in
                if index > 0 { Divider().background(CleansiaColors.outlineVariant) }
                HStack(alignment: .top) {
                    VStack(alignment: .leading, spacing: Spacing.hair) {
                        Text(OrdersFormat.localizedCatalogName(
                            service.name,
                            translations: service.translations,
                            locale: locale
                        ))
                        .font(CleansiaTypography.titleMedium)
                        .foregroundColor(CleansiaColors.onSurface)
                        if let description = OrdersFormat.localizedCatalogDescription(
                            service.description,
                            translations: service.translations,
                            locale: locale
                        ) {
                            Text(description)
                                .font(CleansiaTypography.bodyMedium)
                                .foregroundColor(CleansiaColors.onSurfaceVariant)
                                .lineLimit(2)
                        }
                    }
                    Spacer()
                    if service.estimatedMinutes > 0 {
                        TimeChip(minutes: service.estimatedMinutes)
                    }
                }
            }
        }
    }
}

struct OrderPackagesCard: View {
    @Environment(\.locale) private var locale
    let packages: [CustomerOrderPackage]

    var body: some View {
        OrderCardSurface {
            OrderSectionHeaderRow(title: L10n.OrderDetail.packagesHeader)
            ForEach(Array(packages.enumerated()), id: \.offset) { index, package in
                if index > 0 { Divider().background(CleansiaColors.outlineVariant) }
                HStack(alignment: .top) {
                    VStack(alignment: .leading, spacing: Spacing.hair) {
                        Text(OrdersFormat.localizedCatalogName(
                            package.name,
                            translations: package.translations,
                            locale: locale
                        ))
                        .font(CleansiaTypography.titleMedium)
                        .foregroundColor(CleansiaColors.onSurface)
                        if let description = OrdersFormat.localizedCatalogDescription(
                            package.description,
                            translations: package.translations,
                            locale: locale
                        ) {
                            Text(description)
                                .font(CleansiaTypography.bodyMedium)
                                .foregroundColor(CleansiaColors.onSurfaceVariant)
                                .lineLimit(2)
                        }
                        if !package.includedServices.isEmpty {
                            Text(package.includedServices.joined(separator: ", "))
                                .font(CleansiaTypography.labelSmall)
                                .foregroundColor(CleansiaColors.onSurfaceVariant)
                        }
                    }
                    Spacer()
                    Text(OrdersFormat.price(package.price, currencyCode: package.currencyCode))
                        .font(CleansiaTypography.titleLarge)
                        .foregroundColor(CleansiaColors.onBackground)
                }
            }
        }
    }
}

/// Access instructions hold door codes, key-box combinations and alarm codes, so
/// they are split off from the freely-printed blocks and never travel with them.
enum OrderInstructions {
    struct Block: Equatable {
        let label: String
        let text: String
    }

    static func plainBlocks(_ order: CustomerOrderDetail) -> [Block] {
        var result: [Block] = []
        if let value = order.specialInstructions, !value.isBlank {
            result.append(Block(label: L10n.OrderDetail.specialInstructions, text: value))
        }
        if let value = order.notes, !value.isBlank {
            result.append(Block(label: L10n.OrderDetail.notes, text: value))
        }
        return result
    }

    static func secret(_ order: CustomerOrderDetail) -> String? {
        guard let value = order.accessInstructions, !value.isBlank else { return nil }
        return value
    }

    static func hasAnything(_ order: CustomerOrderDetail) -> Bool {
        !plainBlocks(order).isEmpty || secret(order) != nil
    }
}

struct OrderInstructionsCard: View {
    @Environment(\.snackbarController) private var snackbar
    let order: CustomerOrderDetail

    var body: some View {
        if OrderInstructions.hasAnything(order) {
            OrderCardSurface {
                OrderSectionHeaderRow(title: L10n.OrderDetail.instructions)
                let blocks = OrderInstructions.plainBlocks(order)
                ForEach(Array(blocks.enumerated()), id: \.offset) { index, block in
                    if index > 0 { Divider().background(CleansiaColors.outlineVariant) }
                    VStack(alignment: .leading, spacing: Spacing.hair) {
                        HStack(spacing: Spacing.xs) {
                            Text(block.label)
                                .font(CleansiaTypography.labelMedium)
                                .foregroundColor(CleansiaColors.onSurfaceVariant)
                            Spacer()
                            CopyInstructionButton(text: block.text, onCopy: copy)
                        }
                        Text(block.text)
                            .font(CleansiaTypography.bodyMedium)
                            .foregroundColor(CleansiaColors.onSurface)
                    }
                }
                if let secret = OrderInstructions.secret(order) {
                    CleansiaRevealPanel(title: L10n.OrderDetail.accessInstructions) {
                        VStack(alignment: .leading, spacing: Spacing.xs) {
                            Text(secret)
                                .font(CleansiaTypography.bodyMedium)
                                .foregroundColor(CleansiaColors.onSurface)
                                .textSelection(.enabled)
                            // Under the secret, never in the panel's header: the panel's contract is
                            // that the door code is not composed until it is revealed, and a copy
                            // control in the header would put it one tap from the clipboard while
                            // the panel still reads as concealed.
                            CopyInstructionButton(text: secret, showsTitle: true, onCopy: copy)
                        }
                    }
                    // The tinted panel is its own section, not one more block in the run above it.
                    // The card's uniform Spacing.xs is right between plain blocks and too tight here,
                    // so the break is doubled locally rather than by loosening the card for everyone.
                    .padding(.top, Spacing.xs)
                }
            }
        }
    }

    private func copy(_ text: String) {
        UIPasteboard.general.string = text
        snackbar.showSuccess(L10n.OrderDetail.copied)
    }
}

/// Label-weight on purpose — the instructions are read, not acted on, so the affordance sits
/// beside the label instead of competing with the card's own buttons.
private struct CopyInstructionButton: View {
    let text: String
    var showsTitle = false
    let onCopy: (String) -> Void

    var body: some View {
        Button { onCopy(text) } label: {
            HStack(spacing: Spacing.xxs) {
                Image(systemName: "doc.on.doc")
                    .font(.system(size: 14))
                if showsTitle {
                    Text(L10n.OrderDetail.copy)
                        .font(CleansiaTypography.labelMedium)
                }
            }
            .foregroundColor(CleansiaColors.primary)
            .frame(minWidth: 28, minHeight: 28, alignment: .leading)
            .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
        .accessibilityLabel(Text(L10n.OrderDetail.copy))
    }
}

struct AssignedCleanersCard: View {
    let employees: [AssignedEmployeeDto]

    var body: some View {
        OrderCardSurface {
            OrderSectionHeaderRow(title: L10n.OrderDetail.cleaners)
            ForEach(Array(employees.enumerated()), id: \.offset) { _, employee in
                CleanerRow(employee: employee)
            }
        }
    }
}

private struct CleanerRow: View {
    let employee: AssignedEmployeeDto

    private var displayName: String {
        if let name = employee.fullName, !name.isBlank { return name }
        return L10n.OrderDetail.cleanerFallback
    }

    var body: some View {
        HStack(spacing: Spacing.s) {
            Circle()
                .fill(CleansiaColors.primaryContainer)
                .frame(width: 40, height: 40)
                .overlay(
                    Text(String(displayName.prefix(1)).uppercased())
                        .font(CleansiaTypography.titleMedium)
                        .foregroundColor(CleansiaColors.primary)
                )
            VStack(alignment: .leading, spacing: 0) {
                Text(displayName)
                    .font(CleansiaTypography.titleMedium)
                    .foregroundColor(CleansiaColors.onSurface)
                if let phone = employee.phoneNumber, !phone.isBlank {
                    Text(phone)
                        .font(CleansiaTypography.bodyMedium)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                }
            }
            Spacer()
            if let phone = employee.phoneNumber, !phone.isBlank, let url = URL(string: "tel:\(phone)") {
                Link(destination: url) {
                    Image(systemName: "phone.fill")
                        .font(.system(size: 16))
                        .foregroundColor(CleansiaColors.primary)
                        .frame(width: 36, height: 36)
                        .background(CleansiaColors.primaryContainer, in: Circle())
                }
            }
        }
    }
}

private struct TimeChip: View {
    let minutes: Int

    var body: some View {
        Text(L10n.OrderDetail.durationMinutes(minutes))
            .font(CleansiaTypography.labelSmall)
            .foregroundColor(CleansiaColors.onSurfaceVariant)
            .padding(.horizontal, Spacing.xs)
            .padding(.vertical, Spacing.xxs)
            .background(CleansiaColors.surfaceVariant, in: Capsule())
    }
}

struct FlexibleChips: View {
    let items: [String]

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.xs) {
            ForEach(rows, id: \.self) { row in
                HStack(spacing: Spacing.xs) {
                    ForEach(row, id: \.self) { item in
                        Text(item)
                            .font(CleansiaTypography.labelSmall)
                            .foregroundColor(CleansiaColors.onSurfaceVariant)
                            .padding(.horizontal, Spacing.xs)
                            .padding(.vertical, Spacing.xxs)
                            .background(CleansiaColors.surfaceVariant, in: Capsule())
                    }
                }
            }
        }
    }

    private var rows: [[String]] {
        stride(from: 0, to: items.count, by: 2).map {
            Array(items[$0 ..< min($0 + 2, items.count)])
        }
    }
}
