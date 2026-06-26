import CleansiaCore
import SwiftUI

private struct OnboardingPage: Identifiable {
    let id = UUID()
    let title: String
    let body: String
}

struct OnboardingView: View {
    @StateObject private var vm: OnboardingViewModel
    let onFinished: () -> Void

    init(settings: AppSettingsStore, onFinished: @escaping () -> Void) {
        _vm = StateObject(wrappedValue: OnboardingViewModel(settings: settings))
        self.onFinished = onFinished
    }

    var body: some View {
        OnboardingContent(onFinish: vm.finish)
            .onReceive(vm.finished) { onFinished() }
    }
}

private struct OnboardingContent: View {
    let onFinish: () -> Void

    @State private var currentPage = 0

    private let pages: [OnboardingPage] = [
        OnboardingPage(title: L10n.Onboarding.welcomeTitle, body: L10n.Onboarding.welcomeBody),
        OnboardingPage(title: L10n.Onboarding.readyTitle, body: L10n.Onboarding.readyBody)
    ]

    private var isLastPage: Bool {
        currentPage == pages.count - 1
    }

    var body: some View {
        VStack(spacing: 0) {
            HStack {
                Spacer()
                CleansiaTextLink(L10n.Onboarding.skip, action: onFinish)
            }
            .padding(Spacing.m)

            TabView(selection: $currentPage) {
                ForEach(Array(pages.enumerated()), id: \.element.id) { index, page in
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
            OnboardingContent(onFinish: {})
        }
    }
#endif
