import CleansiaCore
import SwiftUI

struct ServicesStep: View {
    @ObservedObject var viewModel: BookingViewModel

    var body: some View {
        Group {
            switch viewModel.catalogState {
            case .loading:
                CatalogMessageView(
                    systemImage: "arrow.triangle.2.circlepath",
                    message: L10n.Booking.catalogLoading,
                    showsSpinner: true
                )
            case .error:
                CatalogMessageView(
                    systemImage: "arrow.clockwise",
                    message: L10n.Booking.catalogError,
                    retryTitle: L10n.Booking.catalogRetry,
                    onRetry: { Task { await viewModel.retryCatalog() } }
                )
            case let .loaded(catalog) where catalog.isEmpty:
                CatalogMessageView(
                    systemImage: "bubbles.and.sparkles",
                    message: L10n.Booking.catalogEmpty,
                    retryTitle: L10n.Booking.catalogRetry,
                    onRetry: { Task { await viewModel.retryCatalog() } }
                )
            case let .loaded(catalog):
                CatalogContentView(
                    catalog: catalog,
                    state: viewModel.state,
                    onUpdate: { transform in viewModel.update(transform) }
                )
            }
        }
        .task { await viewModel.loadCatalog() }
    }
}

private struct CatalogContentView: View {
    let catalog: Catalog
    let state: BookingState
    let onUpdate: ((BookingState) -> BookingState) -> Void

    @State private var activeCategorySlug: String?

    private var categories: [CatalogCategory] {
        var seen = Set<String>()
        return catalog.services
            .map(\.category)
            .filter { seen.insert($0.slug).inserted }
            .sorted { $0.displayOrder < $1.displayOrder }
    }

    private var filteredServices: [CatalogService] {
        guard let activeCategorySlug else { return catalog.services }
        return catalog.services.filter { $0.category.slug == activeCategorySlug }
    }

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: Spacing.ml) {
                PropertyRow(
                    rooms: state.rooms,
                    bathrooms: state.bathrooms,
                    onRoomsChange: { value in onUpdate { mutate($0) { $0.rooms = max(value, 1) } } },
                    onBathroomsChange: { value in onUpdate { mutate($0) { $0.bathrooms = max(value, 1) } } }
                )
                .padding(.horizontal, Spacing.ml)

                if !catalog.packages.isEmpty {
                    packagesSection
                }

                servicesSection
            }
            .padding(.vertical, Spacing.s)
        }
    }

    private var packagesSection: some View {
        VStack(alignment: .leading, spacing: Spacing.xs) {
            SectionHeader(L10n.Booking.packagesFeatured)
                .padding(.horizontal, Spacing.ml)
            ScrollView(.horizontal, showsIndicators: false) {
                HStack(spacing: Spacing.s) {
                    ForEach(catalog.packages) { pkg in
                        PackageCard(
                            pkg: pkg,
                            selected: state.selectedPackageIds.contains(pkg.id),
                            onToggle: { togglePackage(pkg.id) }
                        )
                    }
                }
                .padding(.horizontal, Spacing.ml)
            }
        }
    }

    private var servicesSection: some View {
        VStack(alignment: .leading, spacing: Spacing.xs) {
            SectionHeader(L10n.Booking.pickService)
                .padding(.horizontal, Spacing.ml)

            if categories.count > 1 {
                ScrollView(.horizontal, showsIndicators: false) {
                    HStack(spacing: Spacing.xs) {
                        CategoryChip(
                            label: L10n.Booking.catAll,
                            systemImage: "star",
                            tint: CategoryPalette.defaultTint,
                            selected: activeCategorySlug == nil
                        ) {
                            activeCategorySlug = nil
                        }
                        ForEach(categories) { category in
                            CategoryChip(
                                label: category.localizedName,
                                systemImage: CategoryPalette.symbol(for: category.slug),
                                tint: CategoryPalette.tint(for: category.slug),
                                selected: activeCategorySlug == category.slug
                            ) {
                                activeCategorySlug = category.slug
                            }
                        }
                    }
                    .padding(.horizontal, Spacing.ml)
                }
            }

            if filteredServices.isEmpty {
                EmptyResults()
            } else {
                ForEach(filteredServices) { service in
                    ServiceRow(
                        service: service,
                        selected: state.selectedServiceIds.contains(service.id),
                        onToggle: { toggleService(service.id) }
                    )
                    .padding(.horizontal, Spacing.ml)
                }
            }
        }
    }

    private func toggleService(_ id: String) {
        onUpdate { mutate($0) { state in
            if state.selectedServiceIds.contains(id) {
                state.selectedServiceIds.remove(id)
            } else {
                state.selectedServiceIds.insert(id)
            }
        } }
    }

    private func togglePackage(_ id: String) {
        onUpdate { mutate($0) { state in
            if state.selectedPackageIds.contains(id) {
                state.selectedPackageIds.remove(id)
            } else {
                state.selectedPackageIds.insert(id)
            }
        } }
    }

    private func mutate(_ state: BookingState, _ change: (inout BookingState) -> Void) -> BookingState {
        var next = state
        change(&next)
        return next
    }
}

private struct PropertyRow: View {
    let rooms: Int
    let bathrooms: Int
    let onRoomsChange: (Int) -> Void
    let onBathroomsChange: (Int) -> Void

    var body: some View {
        HStack(spacing: Spacing.xs) {
            Text(L10n.Booking.yourHome)
                .font(CleansiaTypography.labelLarge)
                .foregroundColor(CleansiaColors.primary)
                .frame(maxWidth: .infinity, alignment: .leading)
            PropertyStepper(label: L10n.Booking.roomsShort(rooms), value: rooms, onChange: onRoomsChange)
            PropertyStepper(label: L10n.Booking.bathShort(bathrooms), value: bathrooms, onChange: onBathroomsChange)
        }
        .padding(.horizontal, Spacing.s)
        .padding(.vertical, Spacing.xs)
        .background(CleansiaColors.primaryContainer.opacity(0.5))
        .clipShape(RoundedRectangle(cornerRadius: CornerRadius.medium))
    }
}

#if DEBUG
    struct ServicesStep_Previews: PreviewProvider {
        static var previews: some View {
            Group {
                CatalogContentPreview(catalog: CatalogFixturesPreview.populated)
                    .previewDisplayName("Loaded")
                CatalogMessageView(
                    systemImage: "arrow.triangle.2.circlepath",
                    message: L10n.Booking.catalogLoading,
                    showsSpinner: true
                )
                .previewDisplayName("Loading")
                CatalogMessageView(
                    systemImage: "arrow.clockwise",
                    message: L10n.Booking.catalogError,
                    retryTitle: L10n.Booking.catalogRetry,
                    onRetry: {}
                )
                .previewDisplayName("Error")
            }
        }
    }

    private struct CatalogContentPreview: View {
        let catalog: Catalog
        @State private var state = BookingState()

        var body: some View {
            CatalogContentView(catalog: catalog, state: state) { transform in
                state = transform(state)
            }
            .background(CleansiaColors.background)
        }
    }

    private enum CatalogFixturesPreview {
        static let populated = Catalog(
            services: [
                CatalogService(
                    id: "s-1",
                    name: "Standard cleaning",
                    description: "Everyday tidy-up",
                    basePrice: 500,
                    perRoomPrice: 100,
                    category: home,
                    translations: [:]
                ),
                CatalogService(
                    id: "s-2",
                    name: "Deep cleaning",
                    description: "Thorough clean",
                    basePrice: 900,
                    perRoomPrice: 150,
                    category: deep,
                    translations: [:]
                )
            ],
            packages: [
                CatalogPackage(
                    id: "p-1",
                    name: "Move-out",
                    description: "Top to bottom",
                    price: 2500,
                    translations: [:],
                    includedServices: []
                )
            ]
        )
        static let home = CatalogCategory(
            id: "c-home",
            slug: "home",
            name: "Home",
            description: nil,
            displayOrder: 0,
            translations: [:]
        )
        static let deep = CatalogCategory(
            id: "c-deep",
            slug: "deep",
            name: "Deep",
            description: nil,
            displayOrder: 1,
            translations: [:]
        )
    }
#endif
