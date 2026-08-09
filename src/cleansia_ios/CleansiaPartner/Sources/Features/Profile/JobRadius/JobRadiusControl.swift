import CleansiaCore
import SwiftUI

struct JobRadiusControl: View {
    let form: JobRadiusForm
    var interactive = true
    let onLimitedChange: (Bool) -> Void
    let onKilometresChange: (Double) -> Void

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.m) {
            VStack(alignment: .leading, spacing: Spacing.s) {
                Toggle(isOn: Binding(get: { form.isLimited }, set: onLimitedChange)) {
                    Text(L10n.JobRadius.limitLabel)
                        .font(CleansiaTypography.bodyLarge)
                        .foregroundColor(CleansiaColors.onSurface)
                }
                .tint(CleansiaColors.primary)
                .disabled(!interactive)

                if form.isLimited {
                    slider
                } else {
                    Text(L10n.JobRadius.limitOffHint)
                        .font(CleansiaTypography.bodyMedium)
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                        .fixedSize(horizontal: false, vertical: true)
                }
            }
            .padding(Spacing.m)
            .background(CleansiaColors.surface)
            .clipShape(RoundedRectangle(cornerRadius: CornerRadius.large))

            Text(L10n.JobRadius.explainer)
                .font(CleansiaTypography.labelSmall)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
                .fixedSize(horizontal: false, vertical: true)
        }
    }

    private var slider: some View {
        VStack(alignment: .leading, spacing: Spacing.xxs) {
            Text(L10n.JobRadius.value(form.kilometres))
                .font(CleansiaTypography.titleLarge)
                .foregroundColor(CleansiaColors.primary)
            Slider(
                value: Binding(get: { Double(form.kilometres) }, set: onKilometresChange),
                in: Double(JobRadiusBounds.minimumKm) ... Double(JobRadiusBounds.maximumKm),
                step: 1
            )
            .tint(CleansiaColors.primary)
            .disabled(!interactive)
            .accessibilityLabel(Text(L10n.JobRadius.limitLabel))
            .accessibilityValue(Text(L10n.JobRadius.value(form.kilometres)))
            HStack {
                Text(L10n.JobRadius.value(JobRadiusBounds.minimumKm))
                Spacer()
                Text(L10n.JobRadius.value(JobRadiusBounds.maximumKm))
            }
            .font(CleansiaTypography.labelSmall)
            .foregroundColor(CleansiaColors.onSurfaceVariant)
        }
    }
}

#if DEBUG
    struct JobRadiusControl_Previews: PreviewProvider {
        static var previews: some View {
            Group {
                JobRadiusControl(
                    form: JobRadiusForm(radiusKm: 40),
                    onLimitedChange: { _ in },
                    onKilometresChange: { _ in }
                )
                .previewDisplayName("Limited")

                JobRadiusControl(
                    form: JobRadiusForm(radiusKm: nil),
                    onLimitedChange: { _ in },
                    onKilometresChange: { _ in }
                )
                .previewDisplayName("Every job")
            }
            .padding(Spacing.m)
            .background(CleansiaColors.background)
            .previewLayout(.sizeThatFits)
        }
    }
#endif
