import CleansiaCore
import CleansiaCustomerApi
import SwiftUI

struct HomeSectionTitle: View {
    let text: String

    var body: some View {
        Text(text)
            .font(CleansiaFont.poppins(.semibold, size: 16))
            .foregroundColor(CleansiaColors.onBackground)
    }
}

/// Insured / Vetted / Same-day (`TrustStrip`, `HomeTab.kt:626-668`) — the
/// fallback when there is no completed order to rebook.
struct TrustStrip: View {
    var body: some View {
        HStack(spacing: 0) {
            trustItem(icon: "shield", label: L10n.Home.trustInsured)
            divider
            trustItem(icon: "checkmark.shield", label: L10n.Home.trustVetted)
            divider
            trustItem(icon: "bolt", label: L10n.Home.trustSameDay)
        }
        .padding(.horizontal, Spacing.s)
        .padding(.vertical, Spacing.s)
        .background(CleansiaColors.surface, in: RoundedRectangle(cornerRadius: 14))
        .overlay(
            RoundedRectangle(cornerRadius: 14)
                .stroke(CleansiaColors.outlineVariant, lineWidth: 1)
        )
    }

    private var divider: some View {
        Rectangle()
            .fill(CleansiaColors.outlineVariant)
            .frame(width: 1, height: 28)
    }

    private func trustItem(icon: String, label: String) -> some View {
        VStack(spacing: Spacing.xxs) {
            Image(systemName: icon)
                .font(.system(size: 18))
                .foregroundColor(CleansiaColors.successText)
            Text(label)
                .font(CleansiaTypography.labelSmall)
                .foregroundColor(CleansiaColors.onSurface)
                .lineLimit(1)
        }
        .frame(maxWidth: .infinity)
    }
}

/// Single-tap rebook of the most recent Completed order (`OrderAgainCard`,
/// `HomeTab.kt:679-745`).
struct OrderAgainCard: View {
    @Environment(\.locale) private var locale
    let order: OrderListItem
    let onTap: () -> Void

    var body: some View {
        Button(action: onTap) {
            HStack(spacing: Spacing.s) {
                ZStack {
                    Circle()
                        .fill(CleansiaColors.primaryContainer)
                        .frame(width: 44, height: 44)
                    Image(systemName: "arrow.clockwise")
                        .font(.system(size: 20))
                        .foregroundColor(CleansiaColors.primary)
                }
                VStack(alignment: .leading, spacing: Spacing.hair) {
                    Text(L10n.Home.orderAgainTitle)
                        .font(CleansiaTypography.labelMedium)
                        .foregroundColor(CleansiaColors.primary)
                    Text(HomeSections.recentBookingTitle(
                        order,
                        fallback: L10n.Home.orderAgainFallbackTitle,
                        languageCode: CatalogLocalization.languageCode(for: locale)
                    ))
                    .font(CleansiaTypography.titleMedium)
                    .foregroundColor(CleansiaColors.onSurface)
                    .lineLimit(1)
                    if let when = HomeSections.orderAgainWhen(order.cleaningDateTime) {
                        Text(L10n.Home.orderAgainSubtitle(when))
                            .font(CleansiaTypography.bodyMedium)
                            .foregroundColor(CleansiaColors.onSurfaceVariant)
                            .lineLimit(1)
                    }
                }
                Spacer(minLength: Spacing.xs)
                Image(systemName: "arrow.right")
                    .font(.system(size: 18, weight: .semibold))
                    .foregroundColor(CleansiaColors.primary)
            }
            .padding(14)
            .frame(maxWidth: .infinity, alignment: .leading)
            .background(CleansiaColors.surface, in: RoundedRectangle(cornerRadius: CornerRadius.medium))
            .overlay(
                RoundedRectangle(cornerRadius: CornerRadius.medium)
                    .stroke(CleansiaColors.outlineVariant, lineWidth: 1)
            )
        }
        .buttonStyle(.plain)
    }
}

/// Active recurring schedules mini-list with a Manage link — Plus-only
/// (`RecurringSchedulesSection`, `HomeTab.kt:753-852`).
struct RecurringSchedulesSection: View {
    let templates: [RecurringTemplate]
    let onManage: () -> Void

    var body: some View {
        VStack(alignment: .leading, spacing: 10) {
            HStack {
                HomeSectionTitle(text: L10n.Home.recurringSectionTitle)
                Spacer()
                Button(action: onManage) {
                    Text(L10n.Home.recurringSectionManage)
                        .font(CleansiaTypography.labelLarge)
                        .foregroundColor(CleansiaColors.primary)
                }
            }
            VStack(spacing: Spacing.xs) {
                ForEach(templates) { template in
                    RecurringScheduleRow(template: template, onTap: onManage)
                }
            }
        }
    }
}

private struct RecurringScheduleRow: View {
    let template: RecurringTemplate
    let onTap: () -> Void

    var body: some View {
        Button(action: onTap) {
            HStack(spacing: Spacing.s) {
                ZStack {
                    Circle()
                        .fill(CleansiaColors.primary.opacity(0.12))
                        .frame(width: 40, height: 40)
                    Image(systemName: "sparkles")
                        .font(.system(size: 18))
                        .foregroundColor(CleansiaColors.primary)
                }
                VStack(alignment: .leading, spacing: Spacing.hair) {
                    Text(L10n.Recurring.cadence(template.frequency))
                        .font(CleansiaTypography.labelMedium)
                        .foregroundColor(CleansiaColors.primary)
                    Text(L10n.Recurring.dayAtTime(RecurringWeekday.label(template.dayOfWeek), template.timeOfDay))
                        .font(CleansiaFont.nunito(.semibold, size: 14))
                        .foregroundColor(CleansiaColors.onSurface)
                    if let addressLine = template.addressLine, !addressLine.isBlank {
                        Text(addressLine)
                            .font(CleansiaTypography.bodyMedium)
                            .foregroundColor(CleansiaColors.onSurfaceVariant)
                            .lineLimit(1)
                    }
                }
                Spacer(minLength: Spacing.xs)
                Image(systemName: "arrow.right")
                    .font(.system(size: 16, weight: .semibold))
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
            }
            .padding(Spacing.s)
            .frame(maxWidth: .infinity, alignment: .leading)
            .background(CleansiaColors.surface, in: RoundedRectangle(cornerRadius: 14))
            .overlay(
                RoundedRectangle(cornerRadius: 14)
                    .stroke(CleansiaColors.outlineVariant, lineWidth: 1)
            )
        }
        .buttonStyle(.plain)
    }
}

/// Top-3 catalog packages, single tap → booking sheet with the package seeded
/// (`PopularPackagesSection`, `HomeTab.kt:861-928`).
struct PopularPackagesSection: View {
    let packages: [CatalogPackage]
    let onPackageTap: (String) -> Void

    var body: some View {
        VStack(alignment: .leading, spacing: 10) {
            HomeSectionTitle(text: L10n.Home.popularPackagesTitle)
            HStack(alignment: .top, spacing: 10) {
                ForEach(packages) { package in
                    PopularPackageCard(package: package) { onPackageTap(package.id) }
                }
            }
            .fixedSize(horizontal: false, vertical: true)
        }
    }
}

private struct PopularPackageCard: View {
    let package: CatalogPackage
    let onTap: () -> Void

    var body: some View {
        Button(action: onTap) {
            VStack(alignment: .leading, spacing: 0) {
                ZStack {
                    Circle()
                        .fill(CleansiaColors.primaryContainer)
                        .frame(width: 40, height: 40)
                    Image(systemName: "bubbles.and.sparkles")
                        .font(.system(size: 18))
                        .foregroundColor(CleansiaColors.primary)
                }
                Text(package.name)
                    .font(CleansiaTypography.labelLarge)
                    .foregroundColor(CleansiaColors.onSurface)
                    .multilineTextAlignment(.leading)
                    .lineLimit(2)
                    .padding(.top, 10)
                Text(L10n.Home.popularPackagesAddCta)
                    .font(CleansiaTypography.labelSmall)
                    .foregroundColor(CleansiaColors.primary)
                    .padding(.top, Spacing.xxs)
            }
            .padding(14)
            .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topLeading)
            .background(CleansiaColors.surface, in: RoundedRectangle(cornerRadius: 18))
            .overlay(
                RoundedRectangle(cornerRadius: 18)
                    .stroke(CleansiaColors.outlineVariant, lineWidth: 1)
            )
        }
        .buttonStyle(.plain)
    }
}

#if DEBUG
    struct HomeSectionViews_Previews: PreviewProvider {
        static var sampleOrder: OrderListItem {
            OrderListItem(
                id: "o1",
                cleaningDateTime: Date(),
                totalPrice: 1290,
                orderStatus: Code(type: "OrderStatus", name: "Completed", value: 5),
                selectedPackages: [PackageListItem(id: "p1", name: "Standard cleaning")],
                currency: CurrencyListItem(code: "CZK"),
                selectedServices: [ServiceListItem(id: "s1", name: "Deep clean")]
            )
        }

        static var sampleTemplate: RecurringTemplate {
            RecurringTemplate(
                id: "t1",
                frequency: 1,
                dayOfWeek: 4,
                timeOfDay: "10:00",
                rooms: 2,
                bathrooms: 1,
                savedAddressId: "a1",
                addressLine: "Zenklova 6, Praha",
                selectedServiceIds: [],
                selectedPackageIds: [],
                paymentType: 1,
                startsOn: Date(),
                endsOn: nil,
                isActive: true
            )
        }

        static func samplePackage(_ id: String, _ name: String) -> CatalogPackage {
            CatalogPackage(
                id: id,
                name: name,
                description: nil,
                price: 1500,
                translations: [:],
                includedServices: []
            )
        }

        static var previews: some View {
            ScrollView {
                VStack(alignment: .leading, spacing: Spacing.l) {
                    TrustStrip()
                    OrderAgainCard(order: sampleOrder, onTap: {})
                    RecurringSchedulesSection(templates: [sampleTemplate], onManage: {})
                    PopularPackagesSection(
                        packages: [
                            samplePackage("p1", "Standard"),
                            samplePackage("p2", "Deep clean"),
                            samplePackage("p3", "Move-out")
                        ],
                        onPackageTap: { _ in }
                    )
                }
                .padding(Spacing.ml)
            }
            .background(CleansiaColors.background)
            .previewDisplayName("Trust + rebook + recurring + packages")
        }
    }
#endif
