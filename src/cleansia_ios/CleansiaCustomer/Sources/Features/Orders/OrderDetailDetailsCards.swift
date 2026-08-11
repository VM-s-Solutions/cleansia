import CleansiaCore
import CleansiaCustomerApi
import SwiftUI

struct CleaningDetailsCard: View {
    @Environment(\.locale) private var locale
    let order: OrderItem

    private var activeExtras: [String] {
        (order.extras ?? [:]).filter(\.value).keys.sorted()
    }

    var body: some View {
        OrderCardSurface {
            OrderSectionHeaderRow(title: L10n.OrderDetail.sectionDetails)
            OrderInfoRow(
                label: L10n.OrderDetail.rooms,
                value: L10n.OrderDetail.roomsBathrooms(order.rooms ?? 0, order.bathrooms ?? 0)
            )
            OrderInfoRow(
                label: L10n.OrderDetail.estimated,
                value: (order.estimatedTime ?? 0) > 0 ? L10n.OrderDetail.durationMinutes(order.estimatedTime ?? 0) : "—"
            )
            if let completedAt = order.completedAt {
                OrderInfoRow(
                    label: L10n.OrderDetail.completedAt,
                    value: OrdersFormat.dateTime(completedAt, locale: locale)
                )
            }
            if !activeExtras.isEmpty {
                Text(L10n.OrderDetail.extras)
                    .font(CleansiaTypography.labelMedium)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
                ExtrasFlow(keys: activeExtras)
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
    let services: [ServiceDetails]

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
                    if (service.estimatedTime ?? 0) > 0 {
                        TimeChip(minutes: service.estimatedTime ?? 0)
                    }
                }
            }
        }
    }
}

struct OrderPackagesCard: View {
    @Environment(\.locale) private var locale
    let packages: [PackageDetails]

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
                        if let included = package.includedServices, !included.isEmpty {
                            Text(included.joined(separator: ", "))
                                .font(CleansiaTypography.labelSmall)
                                .foregroundColor(CleansiaColors.onSurfaceVariant)
                        }
                    }
                    Spacer()
                    Text(OrdersFormat.price(package.price ?? 0, currencyCode: package.currencyCode))
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

    static func plainBlocks(_ order: OrderItem) -> [Block] {
        var result: [Block] = []
        if let value = order.specialInstructions, !value.isBlank {
            result.append(Block(label: L10n.OrderDetail.specialInstructions, text: value))
        }
        if let value = order.notes, !value.isBlank {
            result.append(Block(label: L10n.OrderDetail.notes, text: value))
        }
        return result
    }

    static func secret(_ order: OrderItem) -> String? {
        guard let value = order.accessInstructions, !value.isBlank else { return nil }
        return value
    }

    static func hasAnything(_ order: OrderItem) -> Bool {
        !plainBlocks(order).isEmpty || secret(order) != nil
    }
}

struct OrderInstructionsCard: View {
    let order: OrderItem

    var body: some View {
        if OrderInstructions.hasAnything(order) {
            OrderCardSurface {
                OrderSectionHeaderRow(title: L10n.OrderDetail.instructions)
                let blocks = OrderInstructions.plainBlocks(order)
                ForEach(Array(blocks.enumerated()), id: \.offset) { index, block in
                    if index > 0 { Divider().background(CleansiaColors.outlineVariant) }
                    VStack(alignment: .leading, spacing: Spacing.hair) {
                        Text(block.label)
                            .font(CleansiaTypography.labelMedium)
                            .foregroundColor(CleansiaColors.onSurfaceVariant)
                        Text(block.text)
                            .font(CleansiaTypography.bodyMedium)
                            .foregroundColor(CleansiaColors.onSurface)
                    }
                }
                if let secret = OrderInstructions.secret(order) {
                    CleansiaRevealPanel(title: L10n.OrderDetail.accessInstructions) {
                        Text(secret)
                            .font(CleansiaTypography.bodyMedium)
                            .foregroundColor(CleansiaColors.onSurface)
                            .textSelection(.enabled)
                    }
                }
            }
        }
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
