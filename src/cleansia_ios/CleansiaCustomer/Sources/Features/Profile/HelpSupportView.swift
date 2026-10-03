import CleansiaCore
import MessageUI
import SwiftUI

struct HelpSupportView: View {
    /// The line the customer web footer prints and Android's Help dials. The address is
    /// `CleansiaWeb.contactEmail`, the one support contact (owner ruling 2026-10-02).
    static let supportPhone = "+420739788108"

    @Environment(\.openURL) private var openURL
    @Environment(\.snackbarController) private var snackbar
    private let faqs: [(question: String, answer: String)]

    /// `insurance` is the chosen market's ceiling; nil renders the answer without a figure.
    init(insurance: MarketMoney?) {
        faqs = [
            (L10n.Help.faqQ1, L10n.Help.faqA1),
            (L10n.Help.faqQ2, L10n.Help.faqA2),
            (L10n.Help.faqQ3, InsuranceCopy.faqAnswer(insurance)),
            (L10n.Help.faqQ4, L10n.Help.faqA4),
            (L10n.Help.faqQ5, L10n.Help.faqA5)
        ]
    }

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
                contactRow(icon: "envelope", title: L10n.Help.email, subtitle: CleansiaWeb.contactEmail) {
                    open(
                        "mailto:\(CleansiaWeb.contactEmail)",
                        orCopy: CleansiaWeb.contactEmail,
                        notice: L10n.Help.emailUnavailable,
                        copyFirst: !MFMailComposeViewController.canSendMail()
                    )
                }
                Divider().padding(.leading, Spacing.xl)
                contactRow(icon: "phone", title: L10n.Help.call, subtitle: L10n.Help.callDesc) {
                    open("tel:\(Self.supportPhone)", orCopy: Self.supportPhone, notice: L10n.Help.callUnavailable)
                }
            }
            .background(CleansiaColors.surface)
            .clipShape(RoundedRectangle(cornerRadius: CornerRadius.large))
        }
    }

    private func contactRow(
        icon: String,
        title: String,
        subtitle: String,
        action: @escaping () -> Void
    ) -> some View {
        Button(action: action) {
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
                Image(systemName: "chevron.right")
                    .font(.system(size: 13, weight: .semibold))
                    .foregroundColor(CleansiaColors.onSurfaceVariant)
            }
            .padding(Spacing.m)
            .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
    }

    /// Hands the link to the system. When nothing on the device takes it — no mail app, no phone — the
    /// value is copied and the customer told, as Android's Help does. With `copyFirst` it is copied before
    /// the link goes out: Mail with no account set up still takes a mailto: link, on its setup screen.
    private func open(_ link: String, orCopy value: String, notice: String, copyFirst: Bool = false) {
        guard let url = URL(string: link) else { return }
        let copy = {
            UIPasteboard.general.string = value
            snackbar.showInfo(notice)
        }
        if copyFirst { copy() }
        openURL(url) { accepted in
            if !accepted, !copyFirst { copy() }
        }
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
