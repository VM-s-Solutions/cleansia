import CleansiaCore
import SwiftUI

struct SectionScaffold<Header: View, Form: View>: View {
    let title: String
    let isLoading: Bool
    @ViewBuilder let header: () -> Header
    @ViewBuilder let form: () -> Form

    init(
        title: String,
        isLoading: Bool,
        @ViewBuilder header: @escaping () -> Header = { EmptyView() },
        @ViewBuilder form: @escaping () -> Form
    ) {
        self.title = title
        self.isLoading = isLoading
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
