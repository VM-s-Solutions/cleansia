import CleansiaCore
import SwiftUI

struct CreateRecurringScreen: View {
    @StateObject private var vm: CreateRecurringViewModel
    @State private var showAddressManager = false
    let onCreated: () -> Void

    private let savedAddressRepository: SavedAddressRepository
    private let geocoding: GeocodingService
    private let mapProvider: MapProvider
    private let serviceArea: ServiceAreaProvider?
    private let snackbar: SnackbarController

    init(
        sourceOrderId: String?,
        editing: RecurringTemplate? = nil,
        repository: RecurringBookingRepository,
        savedAddressRepository: SavedAddressRepository,
        geocoding: GeocodingService,
        mapProvider: MapProvider,
        serviceArea: ServiceAreaProvider? = nil,
        snackbar: SnackbarController,
        onCreated: @escaping () -> Void
    ) {
        _vm = StateObject(wrappedValue: CreateRecurringViewModel(
            sourceOrderId: sourceOrderId,
            editing: editing,
            repository: repository,
            catalogClient: LiveCatalogClient(),
            addressClient: LiveRecurringSavedAddressClient(),
            orderClient: LiveOrderClient(),
            snackbar: snackbar
        ))
        self.savedAddressRepository = savedAddressRepository
        self.geocoding = geocoding
        self.mapProvider = mapProvider
        self.serviceArea = serviceArea
        self.snackbar = snackbar
        self.onCreated = onCreated
    }

    private var title: String {
        if vm.isEditing { return L10n.Recurring.editTitle }
        return vm.sourceOrderId == nil ? L10n.Recurring.createTitleBlank : L10n.Recurring.createTitleFromOrder
    }

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: Spacing.l) {
                FrequencySection(selected: vm.formState.frequency, onSelect: vm.setFrequency)
                DayOfWeekSection(selected: vm.formState.dayOfWeek, onSelect: vm.setDayOfWeek)
                TimeSection(time: vm.formState.timeOfDay, onChange: vm.setTimeOfDay)
                AddressSection(
                    addresses: vm.savedAddresses,
                    selectedId: vm.formState.savedAddressId,
                    onSelect: vm.setSavedAddressId,
                    onAddAddress: { showAddressManager = true }
                )
                ServicesSection(
                    catalog: vm.catalog,
                    selectedServiceIds: vm.formState.selectedServiceIds,
                    selectedPackageIds: vm.formState.selectedPackageIds,
                    onToggleService: vm.toggleService,
                    onTogglePackage: vm.togglePackage
                )
                PropertySizeSection(
                    rooms: vm.formState.rooms,
                    bathrooms: vm.formState.bathrooms,
                    onRoomsChange: vm.setRooms,
                    onBathroomsChange: vm.setBathrooms
                )
                PaymentSection(selected: vm.formState.paymentType, onSelect: vm.setPaymentType)
                StartsSection(
                    startsOn: vm.formState.startsOn,
                    earliest: vm.earliestStart,
                    onChange: vm.setStartsOn
                )

                if let appliesNotice = vm.appliesNotice {
                    AppliesNotice(text: appliesNotice)
                }

                CleansiaPrimaryButton(
                    vm.isEditing ? L10n.Recurring.editSubmit : L10n.Recurring.createSubmit,
                    loading: vm.submitState.isSubmitting,
                    enabled: vm.isValid && !vm.submitState.isSubmitting
                ) {
                    Task { if await vm.submit() { onCreated() } }
                }
                Color.clear.frame(height: Spacing.l)
            }
            .padding(.horizontal, Spacing.ml)
            .padding(.top, Spacing.m)
        }
        .navigationTitle(title)
        .navigationBarTitleDisplayMode(.inline)
        .background(CleansiaColors.background.ignoresSafeArea())
        .task { await vm.load() }
        .sheet(
            isPresented: $showAddressManager,
            onDismiss: { Task { await vm.reloadAddresses() } },
            content: { addressManager }
        )
    }

    /// The same surface the profile and the shell open, so an address created
    /// here is saved once, server-side, and every other screen sees it
    /// (Android's inline `AddressManagerSheet` on the wizard's Where step).
    private var addressManager: some View {
        AddressManagerView(
            repository: savedAddressRepository,
            geocoding: geocoding,
            mapProvider: mapProvider,
            serviceArea: serviceArea,
            snackbar: snackbar,
            onBack: { showAddressManager = false },
            onSelected: { address in
                vm.setSavedAddressId(address.id)
                showAddressManager = false
            }
        )
        .snackbarHost(snackbar, bottomInset: Spacing.m)
    }
}

private struct SectionLabel: View {
    let text: String

    var body: some View {
        Text(text)
            .font(CleansiaTypography.titleMedium)
            .foregroundColor(CleansiaColors.onBackground)
    }
}

private struct AppliesNotice: View {
    let text: String

    var body: some View {
        HStack(alignment: .top, spacing: Spacing.s) {
            Image(systemName: "info.circle")
                .foregroundColor(CleansiaColors.onSurfaceVariant)
            Text(text)
                .font(CleansiaTypography.bodyMedium)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
                .fixedSize(horizontal: false, vertical: true)
        }
        .padding(Spacing.m)
        .frame(maxWidth: .infinity, alignment: .leading)
        .background(CleansiaColors.surfaceVariant, in: RoundedRectangle(cornerRadius: CornerRadius.medium))
    }
}

private struct FrequencySection: View {
    let selected: RecurrenceFrequency
    let onSelect: (RecurrenceFrequency) -> Void

    private func label(_ frequency: RecurrenceFrequency) -> String {
        switch frequency {
        case .weekly: L10n.Recurring.freqWeeklyLabel
        case .biweekly: L10n.Recurring.freqBiweeklyLabel
        case .monthly: L10n.Recurring.freqMonthlyLabel
        }
    }

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.s) {
            SectionLabel(text: L10n.Recurring.createFrequencyLabel)
            ForEach(RecurrenceFrequency.allCases, id: \.rawValue) { frequency in
                SelectableRow(text: label(frequency), selected: frequency == selected) {
                    onSelect(frequency)
                }
            }
        }
    }
}

private struct DayOfWeekSection: View {
    @Environment(\.locale) private var locale
    let selected: Int
    let onSelect: (Int) -> Void

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.s) {
            SectionLabel(text: L10n.Recurring.createDayLabel)
            ChipFlow(spacing: Spacing.xs) {
                ForEach(0 ..< 7, id: \.self) { day in
                    DayChip(
                        label: RecurringWeekday.shortLabel(day, locale: locale),
                        selected: day == selected
                    ) {
                        onSelect(day)
                    }
                }
            }
        }
    }
}

private struct DayChip: View {
    let label: String
    let selected: Bool
    let onTap: () -> Void

    var body: some View {
        Button(action: onTap) {
            Text(label)
                .font(CleansiaTypography.labelLarge)
                .foregroundColor(selected ? CleansiaColors.onPrimary : CleansiaColors.onSurface)
                .padding(.horizontal, Spacing.m)
                .padding(.vertical, Spacing.s)
                .background(selected ? CleansiaColors.primary : CleansiaColors.surface, in: Capsule())
                .overlay(
                    Capsule().stroke(
                        selected ? CleansiaColors.primary : CleansiaColors.outlineVariant,
                        lineWidth: 1
                    )
                )
        }
        .buttonStyle(.plain)
        .accessibilityAddTraits(selected ? [.isSelected] : [])
    }
}

private struct TimeSection: View {
    let time: String
    let onChange: (String) -> Void

    private var binding: Binding<Date> {
        Binding(
            get: { RecurringTimeParse.date(from: time) },
            set: { onChange(RecurringTime.format($0)) }
        )
    }

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.s) {
            SectionLabel(text: L10n.Recurring.createTimeLabel)
            DatePicker("", selection: binding, displayedComponents: .hourAndMinute)
                .labelsHidden()
                .datePickerStyle(.wheel)
                .frame(maxWidth: .infinity)
        }
    }
}

private struct AddressSection: View {
    let addresses: [RecurringSavedAddress]
    let selectedId: String
    let onSelect: (String) -> Void
    let onAddAddress: () -> Void

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.s) {
            SectionLabel(text: L10n.Recurring.createAddressLabel)
            ForEach(addresses) { address in
                SelectableRow(
                    text: address.displayLine,
                    badge: address.isDefault ? L10n.Recurring.createAddressDefault : nil,
                    selected: address.id == selectedId
                ) {
                    onSelect(address.id)
                }
            }
            // Always offered, not just on an empty list: a saved address is
            // required to submit, so a customer with none had no way forward.
            AddAddressRow(onTap: onAddAddress)
        }
    }
}

private struct AddAddressRow: View {
    let onTap: () -> Void

    var body: some View {
        Button(action: onTap) {
            HStack(spacing: Spacing.s) {
                Image(systemName: "plus")
                Text(L10n.Recurring.createAddressAddNew)
                    .font(CleansiaTypography.bodyLarge)
                Spacer()
            }
            .foregroundColor(CleansiaColors.primary)
            .padding(Spacing.m)
            .overlay(
                RoundedRectangle(cornerRadius: CornerRadius.small)
                    .stroke(CleansiaColors.primary.opacity(0.4), lineWidth: 1)
            )
        }
        .buttonStyle(.plain)
    }
}

private struct PropertySizeSection: View {
    let rooms: Int
    let bathrooms: Int
    let onRoomsChange: (Int) -> Void
    let onBathroomsChange: (Int) -> Void

    var body: some View {
        HStack(alignment: .top, spacing: Spacing.s) {
            counter(label: L10n.Recurring.createRoomsLabel, value: rooms, onChange: onRoomsChange)
            counter(label: L10n.Recurring.createBathroomsLabel, value: bathrooms, onChange: onBathroomsChange)
        }
    }

    private func counter(label: String, value: Int, onChange: @escaping (Int) -> Void) -> some View {
        VStack(alignment: .leading, spacing: Spacing.s) {
            SectionLabel(text: label)
            PropertyStepper(label: "\(value)", value: value, minimum: 0, onChange: onChange)
        }
        .frame(maxWidth: .infinity, alignment: .leading)
    }
}

private struct ServicesSection: View {
    @Environment(\.locale) private var locale
    let catalog: Catalog
    let selectedServiceIds: Set<String>
    let selectedPackageIds: Set<String>
    let onToggleService: (String) -> Void
    let onTogglePackage: (String) -> Void

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.s) {
            SectionLabel(text: L10n.Recurring.createServicesLabel)
            if !catalog.packages.isEmpty {
                Text(L10n.Recurring.createSectionPackages)
                    .font(CleansiaTypography.labelLarge)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
                ForEach(catalog.packages) { package in
                    SelectableRow(
                        text: package.localizedName(for: locale),
                        selected: selectedPackageIds.contains(package.id)
                    ) {
                        onTogglePackage(package.id)
                    }
                }
            }
            if !catalog.services.isEmpty {
                Text(L10n.Recurring.createSectionServices)
                    .font(CleansiaTypography.labelLarge)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
                ForEach(catalog.services) { service in
                    SelectableRow(
                        text: service.localizedName(for: locale),
                        selected: selectedServiceIds.contains(service.id)
                    ) {
                        onToggleService(service.id)
                    }
                }
            }
        }
    }
}

private struct PaymentSection: View {
    let selected: Int
    let onSelect: (Int) -> Void

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.s) {
            SectionLabel(text: L10n.Recurring.createPaymentLabel)
            SelectableRow(text: L10n.Recurring.createPayCash, selected: selected == 1) { onSelect(1) }
            SelectableRow(text: L10n.Recurring.createPayCard, selected: selected == 2) { onSelect(2) }
        }
    }
}

private struct StartsSection: View {
    let startsOn: Date?
    let earliest: Date
    let onChange: (Date) -> Void

    private var binding: Binding<Date> {
        Binding(
            get: { startsOn ?? earliest },
            set: onChange
        )
    }

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.s) {
            SectionLabel(text: L10n.Recurring.createStartsLabel)
            DatePicker("", selection: binding, in: earliest..., displayedComponents: .date)
                .labelsHidden()
                .frame(maxWidth: .infinity, alignment: .leading)
        }
    }
}

private struct SelectableRow: View {
    let text: String
    var badge: String?
    let selected: Bool
    let onTap: () -> Void

    var body: some View {
        Button(action: onTap) {
            HStack(spacing: Spacing.s) {
                Image(systemName: selected ? "checkmark.circle.fill" : "circle")
                    .foregroundColor(selected ? CleansiaColors.primary : CleansiaColors.onSurfaceVariant)
                Text(text)
                    .font(CleansiaTypography.bodyLarge)
                    .foregroundColor(CleansiaColors.onSurface)
                if let badge {
                    Text(badge)
                        .font(CleansiaTypography.labelSmall)
                        .foregroundColor(CleansiaColors.primary)
                        .padding(.horizontal, Spacing.xs)
                        .padding(.vertical, 2)
                        .background(CleansiaColors.primaryContainer.opacity(0.4), in: Capsule())
                }
                Spacer()
            }
            .padding(Spacing.m)
            .frame(maxWidth: .infinity, alignment: .leading)
            .background(CleansiaColors.surface, in: RoundedRectangle(cornerRadius: CornerRadius.small))
            .overlay(
                RoundedRectangle(cornerRadius: CornerRadius.small)
                    .stroke(selected ? CleansiaColors.primary : CleansiaColors.outlineVariant, lineWidth: 1)
            )
        }
        .buttonStyle(.plain)
    }
}

enum RecurringTimeParse {
    static func date(from hhmm: String) -> Date {
        let parts = hhmm.split(separator: ":")
        var components = DateComponents()
        components.hour = parts.first.flatMap { Int($0) } ?? 10
        components.minute = parts.count > 1 ? Int(parts[1]) ?? 0 : 0
        return Calendar.current.date(from: components) ?? Date()
    }
}
