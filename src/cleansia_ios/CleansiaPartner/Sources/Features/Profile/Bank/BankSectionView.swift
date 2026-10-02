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

    /// Drawn in fours as statements print it, stored as the server keeps it: the form strips the
    /// grouping on every write.
    private var groupedIban: Binding<String> {
        Binding(
            get: { BankForm.groupedIban(vm.state.loadedValue?.iban ?? "") },
            set: { vm.update(\.iban, to: $0) }
        )
    }

    private var bankCountry: Binding<String?> {
        Binding(
            get: { vm.state.loadedValue?.bankCountryId },
            set: { vm.update(\.bankCountryId, to: $0) }
        )
    }

    /// Back is offered only inside the chain, and only when a previous step exists. Reuses the same
    /// jump the step dots added — replace, never push — so the two ways back behave identically.
    private var onboardingBack: (() -> Void)? {
        guard onboarding, let previous = ProfileSection.bank.previous else { return nil }
        return { chainVM.requestJump(to: previous) }
    }

    var body: some View {
        SectionScaffold(
            title: L10n.Profile.bankDetails,
            isLoading: vm.state.isLoading,
            isError: isError,
            onRetry: { Task { await vm.load() } },
            header: {
                if onboarding {
                    OnboardingChainHeader(
                        currentSection: .bank,
                        state: chainVM.state,
                        onSelect: { chainVM.requestJump(to: $0) }
                    )
                }
            },
            form: {
                BankFormFields(
                    countryOptions: vm.countryOptions,
                    bankCountryId: bankCountry,
                    accountPrefix: field(\.accountPrefix),
                    accountNumber: field(\.accountNumber),
                    bankCode: field(\.bankCode),
                    iban: groupedIban,
                    swift: field(\.swift),
                    bankName: field(\.bankName),
                    holderName: field(\.holderName),
                    usesDomesticAccount: form.usesDomesticAccount,
                    ibanError: form.ibanChecked ? form.ibanProblem.map(Self.message) : nil,
                    enabled: !vm.action.isSubmitting
                )
                // Bank is the last step of the onboarding chain, so "Save and continue"
                // would promise a step that does not exist.
                SaveSectionButton(
                    onboarding: false,
                    isSubmitting: vm.action.isSubmitting,
                    enabled: form.canSubmit,
                    onBack: onboardingBack,
                    action: { Task { await vm.save() } }
                )
            }
        )
        .task { await vm.load() }
        .onReceive(vm.saved) { onSaved() }
    }

    /// The server's own copy for the two refusals it names; the length one is the client's, because the
    /// server folds it into `invalid_iban` and a cleaner can act on the number.
    private static func message(_ problem: BankForm.IbanProblem) -> String {
        switch problem {
        case .invalid: ApiErrorLocalizer().message(for: ApiError(code: "validation.payout.invalid_iban"))
        case .otherCountry: ApiErrorLocalizer().message(for: ApiError(code: "validation.payout.iban_country_mismatch"))
        case let .wrongLength(expected): L10n.Profile.ibanWrongLength(expected)
        }
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
    /// A CZ or SK bank takes the three parts; any other takes one IBAN instead.
    var usesDomesticAccount = true
    var ibanError: String?
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
            if usesDomesticAccount {
                // One control, three segments — the account is a single thing to the cleaner typing it.
                CleansiaBankAccountField(
                    prefix: $accountPrefix,
                    number: $accountNumber,
                    bankCode: $bankCode,
                    label: L10n.Profile.bankAccount,
                    prefixPlaceholder: L10n.Profile.bankAccountPrefixPlaceholder,
                    numberPlaceholder: L10n.Profile.bankAccountNumberPlaceholder,
                    bankCodePlaceholder: L10n.Profile.bankCodePlaceholder,
                    helper: L10n.Profile.bankAccountHelper,
                    enabled: enabled
                )
            } else {
                // A bank outside CZ and SK is one IBAN, drawn in groups of four as statements print it.
                // Regrouping rewrites the text on any edit before the last group, so the field keeps
                // the caret on the character being fixed.
                CleansiaTextField(
                    value: $iban,
                    label: L10n.Profile.iban,
                    helper: L10n.Profile.ibanHelper,
                    errorText: ibanError,
                    keyboardType: .asciiCapable,
                    enabled: enabled,
                    keepsCaretWhenRegrouped: true
                )
            }
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
