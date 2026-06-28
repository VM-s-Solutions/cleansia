import CleansiaCore
import SwiftUI

@MainActor
final class CustomerShellModel: ViewModel {
    @Published var selection: CustomerShellTab = .home

    @Published var isBookingPresented = false

    func book() {
        isBookingPresented = true
    }
}

struct CustomerShellView: View {
    @StateObject private var model = CustomerShellModel()
    private let onSignedOut: () -> Void

    init(onSignedOut: @escaping () -> Void) {
        self.onSignedOut = onSignedOut
    }

    var body: some View {
        tabs
            .overlay(alignment: .bottom) {
                BookFab(action: model.book)
                    .offset(y: -28)
            }
            .sheet(isPresented: $model.isBookingPresented) {
                BookingSheetView(onDismiss: { model.isBookingPresented = false })
            }
    }

    private var tabs: some View {
        TabView(selection: $model.selection) {
            PlaceholderTabView(
                systemImage: CustomerShellTab.home.systemImage,
                title: L10n.Shell.placeholderComingSoon(CustomerShellTab.home.label)
            )
            .tabItem { Label(CustomerShellTab.home.label, systemImage: CustomerShellTab.home.systemImage) }
            .tag(CustomerShellTab.home)

            PlaceholderTabView(
                systemImage: CustomerShellTab.orders.systemImage,
                title: L10n.Shell.placeholderComingSoon(CustomerShellTab.orders.label)
            )
            .tabItem { Label(CustomerShellTab.orders.label, systemImage: CustomerShellTab.orders.systemImage) }
            .tag(CustomerShellTab.orders)

            PlaceholderTabView(
                systemImage: CustomerShellTab.rewards.systemImage,
                title: L10n.Shell.placeholderComingSoon(CustomerShellTab.rewards.label)
            )
            .tabItem { Label(CustomerShellTab.rewards.label, systemImage: CustomerShellTab.rewards.systemImage) }
            .tag(CustomerShellTab.rewards)

            ProfilePlaceholderView(onSignedOut: onSignedOut)
                .tabItem { Label(CustomerShellTab.profile.label, systemImage: CustomerShellTab.profile.systemImage) }
                .tag(CustomerShellTab.profile)
        }
        .tint(CleansiaColors.primary)
    }
}

private struct BookFab: View {
    let action: () -> Void

    var body: some View {
        Button(action: action) {
            Image(systemName: "sparkles")
                .font(.system(size: 30, weight: .semibold))
                .foregroundColor(CleansiaColors.onPrimary)
                .frame(width: 64, height: 64)
                .background(Circle().fill(CleansiaColors.primary))
                .overlay(Circle().stroke(CleansiaColors.background, lineWidth: 4))
        }
        .accessibilityLabel(Text(verbatim: L10n.Shell.book))
    }
}

private struct ProfilePlaceholderView: View {
    let onSignedOut: () -> Void

    var body: some View {
        VStack(spacing: Spacing.l) {
            Spacer()
            Image(systemName: CustomerShellTab.profile.systemImage)
                .font(.system(size: 48))
                .foregroundColor(CleansiaColors.primary)
            Text(verbatim: L10n.Shell.placeholderComingSoon(CustomerShellTab.profile.label))
                .font(CleansiaTypography.titleMedium)
                .foregroundColor(CleansiaColors.onBackground)
                .multilineTextAlignment(.center)
            Spacer()
            Button(role: .destructive, action: onSignedOut) {
                Text(verbatim: L10n.signOut)
            }
            .padding(.bottom, Spacing.xxl)
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .background(CleansiaColors.background.ignoresSafeArea())
    }
}

#if DEBUG
    struct CustomerShellView_Previews: PreviewProvider {
        static var previews: some View {
            CustomerShellView(onSignedOut: {})
        }
    }
#endif
