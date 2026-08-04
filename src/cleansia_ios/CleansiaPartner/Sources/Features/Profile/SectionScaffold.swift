import CleansiaCore
import SwiftUI

struct SectionScaffold<Header: View, Form: View>: View {
    let title: String
    let isLoading: Bool
    var isError: Bool = false
    var onRetry: (() -> Void)?
    @ViewBuilder let header: () -> Header
    @ViewBuilder let form: () -> Form

    init(
        title: String,
        isLoading: Bool,
        isError: Bool = false,
        onRetry: (() -> Void)? = nil,
        @ViewBuilder header: @escaping () -> Header = { EmptyView() },
        @ViewBuilder form: @escaping () -> Form
    ) {
        self.title = title
        self.isLoading = isLoading
        self.isError = isError
        self.onRetry = onRetry
        self.header = header
        self.form = form
    }

    var body: some View {
        Group {
            if isLoading {
                VStack {
                    Spacer().frame(height: 80)
                    ProgressView()
                    Spacer()
                }
                .frame(maxWidth: .infinity)
            } else if isError, let onRetry {
                SectionErrorRetry(onRetry: onRetry)
            } else {
                ScrollView {
                    VStack(alignment: .leading, spacing: Spacing.m) {
                        header()
                        form()
                    }
                    .padding(.horizontal, Spacing.m)
                    .padding(.bottom, Spacing.l)
                }
            }
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .background(CleansiaColors.background.ignoresSafeArea())
        .navigationTitle(title)
        .navigationBarTitleDisplayMode(.inline)
    }
}

private struct SectionErrorRetry: View {
    let onRetry: () -> Void

    var body: some View {
        VStack(spacing: Spacing.l) {
            Text(L10n.Profile.errorGeneric)
                .font(CleansiaTypography.bodyMedium)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
                .multilineTextAlignment(.center)
            CleansiaPrimaryButton(L10n.retry, action: onRetry)
                .fixedSize()
        }
        .padding(Spacing.xl)
        .frame(maxWidth: .infinity, maxHeight: .infinity)
    }
}
