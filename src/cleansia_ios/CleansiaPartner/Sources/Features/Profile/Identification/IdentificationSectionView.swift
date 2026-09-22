import CleansiaCore
import CleansiaPartnerApi
import SwiftUI

struct IdentificationSectionView: View {
    @StateObject private var vm: IdentificationSectionViewModel
    @ObservedObject private var chainVM: OnboardingChainViewModel
    private let onboarding: Bool
    private let onSaved: () -> Void

    init(
        client: PartnerProfileClient,
        snackbar: SnackbarController,
        chainVM: OnboardingChainViewModel,
        onboarding: Bool,
        onSaved: @escaping () -> Void
    ) {
        _vm = StateObject(wrappedValue: IdentificationSectionViewModel(client: client, snackbar: snackbar))
        self.chainVM = chainVM
        self.onboarding = onboarding
        self.onSaved = onSaved
    }

    private var isError: Bool {
        if case .error = vm.state { return true }
        return false
    }

    /// Back is offered only inside the chain, and only when a previous step exists. Reuses the same
    /// jump the step dots added — replace, never push — so the two ways back behave identically.
    private var onboardingBack: (() -> Void)? {
        guard onboarding, let previous = ProfileSection.identification.previous else { return nil }
        return { chainVM.requestJump(to: previous) }
    }

    var body: some View {
        SectionScaffold(
            title: L10n.Profile.identification,
            isLoading: vm.state.isLoading,
            isError: isError,
            onRetry: { Task { await vm.load() } },
            header: {
                if onboarding {
                    OnboardingChainHeader(
                        currentSection: .identification,
                        state: chainVM.state,
                        onSelect: { chainVM.requestJump(to: $0) }
                    )
                }
            },
            form: {
                CleansiaDropdown(
                    selectedId: $vm.form.nationalityId,
                    options: vm.countryOptions,
                    label: L10n.Profile.nationality,
                    placeholder: L10n.Profile.noData,
                    searchable: true
                )
                CleansiaTextField(
                    value: $vm.form.passportId,
                    label: L10n.Profile.passport
                )
                CleansiaDropdown(
                    selectedId: $vm.form.businessCountryId,
                    options: vm.countryOptions,
                    label: L10n.Profile.businessCountry,
                    placeholder: L10n.Profile.noData,
                    searchable: true
                )
                CleansiaTextField(
                    value: $vm.form.registrationNumber,
                    // The country's own word when it has one, our neutral wording when it does not.
                    // "Registration number" is correct everywhere and precise nowhere, which is
                    // exactly what a fallback should be — flattening every country to it would have
                    // cost CZ and SK the term their own registries use.
                    label: vm.fieldLabels?.registrationNumberLabel ?? L10n.Profile.registrationNumber
                )
                // A row an operator onboarded as a company shows its stored legal name locked, the
                // way the personal section shows the email: the server holds it and this surface
                // must not hide it, but nothing typed here could ever be saved.
                if let storedLegalEntityName = vm.form.storedLegalEntityName {
                    CleansiaTextField(
                        value: .constant(storedLegalEntityName),
                        label: L10n.Profile.legalEntityName,
                        enabled: false
                    )
                }
                SaveSectionButton(
                    onboarding: onboarding,
                    isSubmitting: vm.action.isSubmitting,
                    onBack: onboardingBack,
                    action: { Task { await vm.save() } }
                )
            }
        )
        .task { await vm.load() }
        // The labels belong to the BUSINESS country, so they follow the picker rather than the
        // load. Android does the same off onBusinessCountrySelected.
        .onChange(of: vm.form.businessCountryId) { countryId in
            Task { await vm.loadFieldLabels(for: countryId) }
        }
        .onReceive(vm.saved) { onSaved() }
    }
}
