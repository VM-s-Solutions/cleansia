import CleansiaCore
import SwiftUI

struct PersonalSectionView: View {
    @StateObject private var vm: PersonalSectionViewModel
    @ObservedObject private var chainVM: OnboardingChainViewModel
    /// Held plainly rather than observed: `ProfileAvatarField` is the only thing that reads it, and it
    /// observes the model itself, so a photo edit redraws the card without redrawing the form.
    ///
    /// Still optional, but no longer nil on the onboarding route: the photo is shown during
    /// onboarding as it is on Android, without being a fifth step — nothing about it gates the
    /// account unlock, and a cleaner who skips it still completes the chain. Owner ruling
    /// 2026-08-27, reversing the decision that kept it off.
    private let avatar: ProfileAvatarViewModel?
    private let avatarCache: RemoteImageCache?
    private let onboarding: Bool
    private let onSaved: () -> Void

    init(
        client: PartnerProfileClient,
        snackbar: SnackbarController,
        chainVM: OnboardingChainViewModel,
        onboarding: Bool,
        avatar: ProfileAvatarViewModel? = nil,
        avatarCache: RemoteImageCache? = nil,
        onSaved: @escaping () -> Void
    ) {
        _vm = StateObject(wrappedValue: PersonalSectionViewModel(client: client, snackbar: snackbar))
        self.chainVM = chainVM
        self.avatar = avatar
        self.avatarCache = avatarCache
        self.onboarding = onboarding
        self.onSaved = onSaved
    }

    private var isError: Bool {
        if case .error = vm.state { return true }
        return false
    }

    var body: some View {
        SectionScaffold(
            title: L10n.Profile.personal,
            isLoading: vm.state.isLoading,
            isError: isError,
            onRetry: { Task { await vm.load() } },
            header: {
                if onboarding {
                    OnboardingChainHeader(
                        currentSection: .personal,
                        state: chainVM.state,
                        onSelect: { chainVM.requestJump(to: $0) }
                    )
                }
            },
            form: {
                // Above the name, because the photo is the first of the person's own details and not an
                // afterthought under the save button.
                if let avatar, let avatarCache {
                    ProfileAvatarField(
                        avatar: avatar,
                        cache: avatarCache,
                        showsPendingBar: !onboarding
                    )
                }
                CleansiaTextField(
                    value: $vm.form.firstName,
                    label: L10n.Profile.firstName,
                    errorText: vm.form.firstNameError
                )
                CleansiaTextField(
                    value: $vm.form.lastName,
                    label: L10n.Profile.lastName,
                    errorText: vm.form.lastNameError
                )
                BirthDateField(
                    birthDate: $vm.form.birthDate,
                    label: L10n.Profile.birthDate,
                    placeholder: L10n.Profile.birthDatePlaceholder,
                    errorText: vm.form.birthDateError
                )
                CleansiaPhoneInput(
                    value: $vm.form.phone,
                    label: L10n.Profile.phone
                )
                CleansiaTextField(
                    value: $vm.form.email,
                    label: L10n.Profile.email,
                    helper: L10n.Profile.emailReadonlyHelper,
                    keyboardType: .emailAddress,
                    enabled: false
                )
                SaveSectionButton(
                    onboarding: onboarding,
                    isSubmitting: vm.action.isSubmitting,
                    action: {
                        Task {
                            // On the chain the photo has no Save of its own, so this button
                            // owns both writes. save() is a no-op when nothing was picked.
                            if onboarding { await avatar?.save() }
                            await vm.save()
                        }
                    }
                )
            }
        )
        .task { await vm.load() }
        .onReceive(vm.saved) { onSaved() }
    }
}
