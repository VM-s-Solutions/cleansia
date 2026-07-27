import CleansiaCore
import SwiftUI

private struct OnboardingPage {
    let title: String
    let body: String
}

struct OnboardingView: View {
    @StateObject private var vm: OnboardingViewModel
    @ObservedObject private var preferences: PreferencesModel
    let onFinished: () -> Void

    init(settings: AppSettingsStore, preferences: PreferencesModel, onFinished: @escaping () -> Void) {
        _vm = StateObject(wrappedValue: OnboardingViewModel(settings: settings))
        self.preferences = preferences
        self.onFinished = onFinished
    }

    var body: some View {
        OnboardingContent(
            languageSummary: PreferencesLabels.languageSummary(
                isFollowingSystem: preferences.isFollowingSystemLanguage,
                tag: preferences.languageTag
            ),
            selectedLanguageId: preferences.isFollowingSystemLanguage
                ? PreferencesLabels.systemLanguageId
                : preferences.languageTag,
            onSelectLanguage: { preferences.selectLanguage(id: $0) },
            onFinish: vm.finish
        )
        .onReceive(vm.finished) { onFinished() }
    }
}

private struct OnboardingContent: View {
    let languageSummary: String
    let selectedLanguageId: String
    let onSelectLanguage: (String) -> Void
    let onFinish: () -> Void

    @State private var currentPage = 0

    private var pages: [OnboardingPage] {
        [
            OnboardingPage(title: L10n.Onboarding.welcomeTitle, body: L10n.Onboarding.welcomeBody),
            OnboardingPage(title: L10n.Onboarding.readyTitle, body: L10n.Onboarding.readyBody)
        ]
    }

    private var isLastPage: Bool {
        currentPage == pages.count - 1
    }

    var body: some View {
        VStack(spacing: 0) {
            HStack {
                // Language sits opposite Skip in the intro header rather than in
                // the post-signup RegistrationLock chain: that chain only renders
                // after RegisterEmployee has already stamped PreferredLanguageCode
                // and queued the confirmation email, so a choice made there would
                // arrive too late to affect the one mail that matters — and its
                // "Step X of 4" progress maths counts profile sections, which a
                // display preference is not.
                OnboardingLanguageMenu(
                    summary: languageSummary,
                    selectedId: selectedLanguageId,
                    onSelect: onSelectLanguage
                )
                Spacer()
                CleansiaTextLink(L10n.Onboarding.skip, action: onFinish)
            }
            .padding(Spacing.m)

            TabView(selection: $currentPage) {
                // Keyed by position, not by a per-instance UUID: the page titles
                // are re-read from L10n on every language change, so identity has
                // to survive rebuilding the array or switching language would
                // reset the pager to page one.
                ForEach(Array(pages.enumerated()), id: \.offset) { index, page in
                    OnboardingPageView(page: page)
                        .tag(index)
                }
            }
            .tabViewStyle(.page(indexDisplayMode: .never))

            PageIndicator(currentPage: currentPage, pageCount: pages.count)

            Spacer().frame(height: Spacing.s)

            CleansiaPrimaryButton(
                isLastPage ? L10n.Onboarding.getStarted : L10n.Onboarding.next,
                action: advance
            )
            .padding(.horizontal, Spacing.m)

            Spacer().frame(height: Spacing.l)
        }
        .background(CleansiaColors.background.ignoresSafeArea())
    }

    private func advance() {
        if isLastPage {
            onFinish()
        } else {
            withAnimation { currentPage += 1 }
        }
    }
}

/// Language chooser for the pre-auth intro.
///
/// A `Menu`, not a pushed route: `PartnerRootView` drives the pre-auth flow
/// with a bare `ZStack` over a `Route` enum and has no `NavigationStack`, so
/// there is nothing to push onto and nothing to pop back from.
///
/// The trigger shows the language the app is in *right now* — the native name,
/// or the translated "System" label when following the device — because the
/// person who needs this control most is the one who cannot read the language
/// currently on screen.
private struct OnboardingLanguageMenu: View {
    let summary: String
    let selectedId: String
    let onSelect: (String) -> Void

    private var options: [PreferenceOption] {
        [PreferenceOption(id: PreferencesLabels.systemLanguageId, label: L10n.Profile.languageSystem)]
            + PreferencesLabels.languages.map { PreferenceOption(id: $0.tag, label: $0.label) }
    }

    var body: some View {
        Menu {
            // A Picker inside the Menu rather than plain Buttons: it marks the
            // active row, which is the only cue that "System" is a state and not
            // a reset action.
            Picker(
                L10n.Profile.language,
                selection: Binding(get: { selectedId }, set: onSelect)
            ) {
                ForEach(options) { option in
                    Text(option.label).tag(option.id)
                }
            }
        } label: {
            HStack(spacing: Spacing.xs) {
                Image(systemName: "globe")
                Text(summary)
                    .font(CleansiaTypography.labelLarge)
                Image(systemName: "chevron.down")
                    .font(.system(size: 11, weight: .semibold))
            }
            .foregroundColor(CleansiaColors.primary)
            .contentShape(Rectangle())
        }
        .accessibilityLabel(L10n.Profile.language)
    }
}

private struct OnboardingPageView: View {
    let page: OnboardingPage

    var body: some View {
        VStack(spacing: 0) {
            Spacer()
            Mascot.waving.image
                .resizable()
                .scaledToFit()
                .frame(width: 180, height: 180)

            Spacer().frame(height: Spacing.l)

            Text(page.title)
                .font(CleansiaTypography.displayMedium)
                .foregroundColor(CleansiaColors.onBackground)
                .multilineTextAlignment(.center)

            Spacer().frame(height: Spacing.m)

            Text(page.body)
                .font(CleansiaTypography.bodyLarge)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
                .multilineTextAlignment(.center)
            Spacer()
        }
        .padding(.horizontal, Spacing.l)
        .frame(maxWidth: .infinity)
    }
}

private struct PageIndicator: View {
    let currentPage: Int
    let pageCount: Int

    var body: some View {
        HStack(spacing: Spacing.xs) {
            ForEach(0 ..< pageCount, id: \.self) { index in
                Circle()
                    .fill(index == currentPage ? CleansiaColors.primary : CleansiaColors.outline)
                    .frame(width: 8, height: 8)
            }
        }
        .padding(.vertical, Spacing.m)
    }
}

#if DEBUG
    struct OnboardingView_Previews: PreviewProvider {
        static var previews: some View {
            OnboardingContent(
                languageSummary: "Čeština",
                selectedLanguageId: "cs",
                onSelectLanguage: { _ in },
                onFinish: {}
            )
        }
    }
#endif
