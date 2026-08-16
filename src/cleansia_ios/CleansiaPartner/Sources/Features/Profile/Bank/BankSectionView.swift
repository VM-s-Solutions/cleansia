import CleansiaCore
import SwiftUI

struct BankSectionView: View {
    @StateObject private var vm: BankSectionViewModel
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
        _vm = StateObject(wrappedValue: BankSectionViewModel(client: client, snackbar: snackbar))
        self.chainVM = chainVM
        self.onboarding = onboarding
        self.onSaved = onSaved
    }

    private var form: BankForm {
        vm.state.loadedValue ?? BankForm()
    }

    private var isError: Bool {
        if case .error = vm.state { return true }
        return false
    }

    private func field(_ keyPath: WritableKeyPath<BankForm, String>) -> Binding<String> {
        Binding(
            get: { vm.state.loadedValue?[keyPath: keyPath] ?? "" },
            set: { vm.update(keyPath, to: $0) }
        )
    }

    private var bankCountry: Binding<String?> {
        Binding(
            get: { vm.state.loadedValue?.bankCountryId },
            set: { vm.update(\.bankCountryId, to: $0) }
        )
    }

    var body: some View {
        SectionScaffold(
            title: L10n.Profile.bankDetails,
            isLoading: vm.state.isLoading,
            isError: isError,
            onRetry: { Task { await vm.load() } },
            header: {
                if onboarding {
                    OnboardingChainHeader(currentSection: .bank, state: chainVM.state)
                }
            },
            form: {
                BankFormFields(
                    countryOptions: vm.countryOptions,
                    bankCountryId: bankCountry,
                    accountPrefix: field(\.accountPrefix),
                    accountNumber: field(\.accountNumber),
                    bankCode: field(\.bankCode),
                    iban: field(\.iban),
                    swift: field(\.swift),
                    bankName: field(\.bankName),
                    holderName: field(\.holderName),
                    enabled: !vm.action.isSubmitting
                )
                // Bank is the last step of the onboarding chain, so "Save and continue"
                // would promise a step that does not exist.
                SaveSectionButton(
                    onboarding: false,
                    isSubmitting: vm.action.isSubmitting,
                    enabled: form.canSubmit,
                    action: { Task { await vm.save() } }
                )
            }
        )
        .task { await vm.load() }
        .onReceive(vm.saved) { onSaved() }
    }
}

struct BankFormFields: View {
    let countryOptions: [CleansiaDropdownOption]
    @Binding var bankCountryId: String?
    @Binding var accountPrefix: String
    @Binding var accountNumber: String
    @Binding var bankCode: String
    @Binding var iban: String
    @Binding var swift: String
    @Binding var bankName: String
    @Binding var holderName: String
    var enabled: Bool = true

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.m) {
            CleansiaDropdown(
                selectedId: $bankCountryId,
                options: countryOptions,
                label: L10n.Profile.bankCountry,
                placeholder: L10n.Profile.noData,
                enabled: enabled,
                searchable: true
            )
            // One control, three segments — the account is a single thing to the cleaner typing it.
            CleansiaBankAccountField(
                prefix: $accountPrefix,
                number: $accountNumber,
                bankCode: $bankCode,
                label: L10n.Profile.bankAccount,
                helper: L10n.Profile.bankAccountHelper,
                enabled: enabled
            )
            CleansiaTextField(
                value: $iban,
                label: L10n.Profile.iban,
                helper: L10n.Profile.ibanHelper,
                enabled: enabled
            )
            CleansiaTextField(
                value: $swift,
                label: L10n.Profile.swiftCode,
                helper: L10n.Profile.swiftCodeHelper,
                enabled: enabled
            )
            CleansiaTextField(
                value: $bankName,
                label: L10n.Profile.bankName,
                enabled: enabled
            )
            CleansiaTextField(
                value: $holderName,
                label: L10n.Profile.bankAccountHolder,
                helper: L10n.Profile.bankAccountHolderHelper,
                enabled: enabled
            )
        }
    }
}

#if DEBUG
    private struct BankFormFieldsPreviewHost: View {
        @State var form = BankForm(
            bankCountryId: "country-cz",
            accountPrefix: "19",
            accountNumber: "2000145399",
            bankCode: "0800"
        )

        var body: some View {
            ScrollView {
                BankFormFields(
                    countryOptions: [CleansiaDropdownOption(id: "country-cz", label: "Czech Republic")],
                    bankCountryId: $form.bankCountryId,
                    accountPrefix: $form.accountPrefix,
                    accountNumber: $form.accountNumber,
                    bankCode: $form.bankCode,
                    iban: $form.iban,
                    swift: $form.swift,
                    bankName: $form.bankName,
                    holderName: $form.holderName
                )
                .padding(Spacing.m)
            }
            .background(CleansiaColors.background)
        }
    }

    struct BankSectionView_Previews: PreviewProvider {
        static var previews: some View {
            Group {
                BankFormFieldsPreviewHost().previewDisplayName("Content")
                SectionScaffold(title: "Bank details", isLoading: true) { EmptyView() }
                    .previewDisplayName("Loading")
                SectionScaffold(
                    title: "Bank details",
                    isLoading: false,
                    isError: true,
                    onRetry: {},
                    form: { EmptyView() }
                )
                .previewDisplayName("Error")
            }
        }
    }
#endif
