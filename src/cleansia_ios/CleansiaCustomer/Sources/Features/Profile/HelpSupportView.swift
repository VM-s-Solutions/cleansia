import CleansiaCore
import SwiftUI

struct HelpSupportView: View {
    private let faqs: [(question: String, answer: String)] = [
        (L10n.Help.faqQ1, L10n.Help.faqA1),
        (L10n.Help.faqQ2, L10n.Help.faqA2),
        (L10n.Help.faqQ3, L10n.Help.faqA3),
        (L10n.Help.faqQ4, L10n.Help.faqA4),
        (L10n.Help.faqQ5, L10n.Help.faqA5)
    ]

    var body: some View {
        ZStack {
            CleansiaColors.background.ignoresSafeArea()
            ScrollView {
                VStack(alignment: .leading, spacing: Spacing.l) {
                    contactSection
                    faqSection
                }
                .padding(Spacing.m)
            }
        }
        .navigationTitle(L10n.Help.title)
        .navigationBarTitleDisplayMode(.inline)
    }

    private var contactSection: some View {
        VStack(alignment: .leading, spacing: Spacing.s) {
            Text(L10n.Help.contactTitle.uppercased())
                .font(CleansiaTypography.labelSmall)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
            VStack(spacing: 0) {
                contactRow(icon: "envelope", title: L10n.Help.email, subtitle: L10n.Help.emailDesc)
                Divider().padding(.leading, Spacing.xl)
                contactRow(icon: "phone", title: L10n.Help.call, subtitle: L10n.Help.callDesc)
            }
            .background(CleansiaColors.surface)
            .clipShape(RoundedRectangle(cornerRadius: CornerRadius.large))
        }
    }

    private func contactRow(icon: String, title: String, subtitle: String) -> some View {
        HStack(spacing: Spacing.m) {
            Image(systemName: icon)
                .foregroundColor(CleansiaColors.primary)
                .frame(width: 24)
            VStack(alignment: .leading, spacing: 2) {
                Text(title)
                    .font(CleansiaTypography.bodyLarge)
                    .foregroundColor(CleansiaColors.onSurface)
                Text(subtitle)
                    .font(CleansiaTypography.labelSmall)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
            }
            Spacer()
        }
        .padding(Spacing.m)
    }

    private var faqSection: some View {
        VStack(alignment: .leading, spacing: Spacing.s) {
            Text(L10n.Help.faqTitle.uppercased())
                .font(CleansiaTypography.labelSmall)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
            VStack(spacing: Spacing.s) {
                ForEach(faqs.indices, id: \.self) { index in
                    FaqRow(question: faqs[index].question, answer: faqs[index].answer)
                }
            }
        }
    }
}

private struct FaqRow: View {
    let question: String
    let answer: String
    @State private var expanded = false

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.xs) {
            Button {
                withAnimation { expanded.toggle() }
            } label: {
                HStack {
                    Text(question)
                        .font(CleansiaTypography.titleMedium)
                        .foregroundColor(CleansiaColors.onSurface)
                        .multilineTextAlignment(.leading)
                    Spacer()
                    Image(systemName: expanded ? "chevron.up" : "chevron.down")
                        .font(.system(size: 13, weight: .semibold))
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                }
                .contentShape(Rectangle())
            }
            .buttonStyle(.plain)
            if expanded {
                Text(answer)
                    .font(CleansiaTypography.bodyMedium)
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
            }
        }
        .padding(Spacing.m)
        .frame(maxWidth: .infinity, alignment: .leading)
        .background(CleansiaColors.surface)
        .clipShape(RoundedRectangle(cornerRadius: CornerRadius.large))
    }
}
