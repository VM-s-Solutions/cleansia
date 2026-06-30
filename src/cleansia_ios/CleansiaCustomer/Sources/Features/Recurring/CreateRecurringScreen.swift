import CleansiaCore
import SwiftUI

struct CreateRecurringScreen: View {
    @StateObject private var vm: CreateRecurringViewModel
    let onCreated: () -> Void

    init(
        sourceOrderId: String?,
        repository: RecurringBookingRepository,
        snackbar: SnackbarController,
        onCreated: @escaping () -> Void
    ) {
        _vm = StateObject(wrappedValue: CreateRecurringViewModel(
            sourceOrderId: sourceOrderId,
            repository: repository,
            catalogClient: LiveCatalogClient(),
            addressClient: LiveRecurringSavedAddressClient(),
            orderClient: LiveOrderClient(),
            snackbar: snackbar
        ))
        self.onCreated = onCreated
    }

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: Spacing.l) {
                FrequencySection(selected: vm.formState.frequency, onSelect: vm.setFrequency)
                TimeSection(time: vm.formState.timeOfDay, onChange: vm.setTimeOfDay)
                AddressSection(
                    addresses: vm.savedAddresses,
                    selectedId: vm.formState.savedAddressId,
                    onSelect: vm.setSavedAddressId
                )
                ServicesSection(
                    catalog: vm.catalog,
                    selectedServiceIds: vm.formState.selectedServiceIds,
                    selectedPackageIds: vm.formState.selectedPackageIds,
                    onToggleService: vm.toggleService,
                    onTogglePackage: vm.togglePackage
                )
                PaymentSection(selected: vm.formState.paymentType, onSelect: vm.setPaymentType)
                StartsSection(startsOn: vm.formState.startsOn, onChange: vm.setStartsOn)

                CleansiaPrimaryButton(
                    L10n.Recurring.createSubmit,
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
        .navigationTitle(vm.sourceOrderId == nil
            ? L10n.Recurring.createTitleBlank
            : L10n.Recurring.createTitleFromOrder)
        .navigationBarTitleDisplayMode(.inline)
        .background(CleansiaColors.background.ignoresSafeArea())
        .task { await vm.load() }
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

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.s) {
            SectionLabel(text: L10n.Recurring.createAddressLabel)
            if addresses.isEmpty {
                Text(L10n.Recurring.createAddressEmpty)
                    .font(CleansiaTypography.bodyMedium)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
            } else {
                ForEach(addresses) { address in
                    SelectableRow(
                        text: address.displayLine,
                        badge: address.isDefault ? L10n.Recurring.createAddressDefault : nil,
                        selected: address.id == selectedId
                    ) {
                        onSelect(address.id)
                    }
                }
            }
        }
    }
}

private struct ServicesSection: View {
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
                    SelectableRow(text: package.localizedName, selected: selectedPackageIds.contains(package.id)) {
                        onTogglePackage(package.id)
                    }
                }
            }
            if !catalog.services.isEmpty {
                Text(L10n.Recurring.createSectionServices)
                    .font(CleansiaTypography.labelLarge)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
                ForEach(catalog.services) { service in
                    SelectableRow(text: service.localizedName, selected: selectedServiceIds.contains(service.id)) {
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
    let onChange: (Date) -> Void

    private var binding: Binding<Date> {
        Binding(
            get: { startsOn ?? Date() },
            set: onChange
        )
    }

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.s) {
            SectionLabel(text: L10n.Recurring.createStartsLabel)
            DatePicker("", selection: binding, in: Date()..., displayedComponents: .date)
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
