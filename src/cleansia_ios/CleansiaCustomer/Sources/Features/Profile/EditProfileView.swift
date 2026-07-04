import CleansiaCore
import SwiftUI

struct EditProfileView: View {
    @ObservedObject var vm: ProfileViewModel
    var showBookingHint = false
    let onSaved: () -> Void

    @State private var firstName = ""
    @State private var lastName = ""
    @State private var phone = ""
    @State private var birthDate: Date?
    @State private var loadedFor: String?

    private var canSave: Bool {
        !firstName.isBlank && !lastName.isBlank && !vm.saveState.isSubmitting
    }

    private func missingFieldError(_ value: String, message: String) -> String? {
        showBookingHint && value.isBlank ? message : nil
    }

    var body: some View {
        ZStack {
            CleansiaColors.background.ignoresSafeArea()
            ScrollView {
                VStack(alignment: .leading, spacing: Spacing.m) {
                    if showBookingHint {
                        BookingHintBanner()
                    }
                    CleansiaTextField(
                        value: $firstName,
                        label: L10n.EditProfile.firstName,
                        errorText: missingFieldError(firstName, message: L10n.Auth.errorFirstNameRequired)
                    )
                    CleansiaTextField(
                        value: $lastName,
                        label: L10n.EditProfile.lastName,
                        errorText: missingFieldError(lastName, message: L10n.Auth.errorLastNameRequired)
                    )
                    CleansiaTextField(
                        value: .constant(vm.currentUser?.email ?? ""),
                        label: L10n.EditProfile.email,
                        helper: L10n.EditProfile.emailReadonly,
                        enabled: false
                    )
                    CleansiaTextField(
                        value: $phone,
                        label: L10n.EditProfile.phone,
                        errorText: missingFieldError(phone, message: L10n.EditProfile.phoneRequired),
                        keyboardType: .phonePad
                    )
                    BirthDateField(birthDate: $birthDate)

                    CleansiaPrimaryButton(
                        L10n.EditProfile.save,
                        loading: vm.saveState.isSubmitting,
                        enabled: canSave
                    ) {
                        Task {
                            await vm.save(
                                firstName: firstName,
                                lastName: lastName,
                                phoneNumber: phone,
                                birthDate: birthDate,
                                languageCode: vm.currentUser?.preferredLanguageCode
                            )
                        }
                    }
                    .padding(.top, Spacing.m)
                }
                .padding(Spacing.m)
            }
        }
        .navigationTitle(L10n.EditProfile.title)
        .navigationBarTitleDisplayMode(.inline)
        .task { await vm.refresh() }
        .onReceive(vm.repository.$currentUser) { user in seed(user) }
        .onReceive(vm.saved) { onSaved() }
    }

    private func seed(_ user: CurrentUserProfile?) {
        guard let user, loadedFor != user.email else { return }
        loadedFor = user.email
        firstName = user.firstName
        lastName = user.lastName
        phone = user.phoneNumber ?? ""
        birthDate = user.birthDate
    }
}

private struct BookingHintBanner: View {
    var body: some View {
        HStack(alignment: .top, spacing: Spacing.s) {
            Image(systemName: "info.circle.fill")
                .foregroundColor(CleansiaColors.primary)
            Text(L10n.EditProfile.bookingHint)
                .font(CleansiaTypography.bodyMedium)
                .foregroundColor(CleansiaColors.onPrimaryContainer)
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .padding(Spacing.m)
        .background(CleansiaColors.primaryContainer)
        .clipShape(RoundedRectangle(cornerRadius: CornerRadius.small))
    }
}

private struct BirthDateField: View {
    @Binding var birthDate: Date?
    @State private var showPicker = false

    private static let formatter: DateFormatter = {
        let formatter = DateFormatter()
        formatter.dateStyle = .medium
        return formatter
    }()

    var body: some View {
        VStack(alignment: .leading, spacing: Spacing.xs) {
            Text(L10n.EditProfile.birthDate)
                .font(CleansiaTypography.labelMedium)
                .foregroundColor(CleansiaColors.onSurfaceVariant)
            Button {
                showPicker = true
            } label: {
                HStack {
                    Text(birthDate.map { Self.formatter.string(from: $0) } ?? L10n.EditProfile.birthDatePlaceholder)
                        .font(CleansiaTypography.bodyLarge)
                        .foregroundColor(birthDate == nil ? CleansiaColors.onSurfaceVariant : CleansiaColors.onSurface)
                    Spacer()
                    Image(systemName: "calendar")
                        .foregroundColor(CleansiaColors.onSurfaceVariant)
                }
                .padding(Spacing.m)
                .background(CleansiaColors.surface)
                .overlay(
                    RoundedRectangle(cornerRadius: CornerRadius.small)
                        .stroke(CleansiaColors.outline, lineWidth: 1)
                )
                .clipShape(RoundedRectangle(cornerRadius: CornerRadius.small))
            }
            .buttonStyle(.plain)
        }
        .sheet(isPresented: $showPicker) {
            DatePicker(
                L10n.EditProfile.birthDate,
                selection: Binding(get: { birthDate ?? Date() }, set: { birthDate = $0 }),
                in: ...Date(),
                displayedComponents: .date
            )
            .datePickerStyle(.graphical)
            .padding()
            .presentationDetents([.medium])
        }
    }
}
