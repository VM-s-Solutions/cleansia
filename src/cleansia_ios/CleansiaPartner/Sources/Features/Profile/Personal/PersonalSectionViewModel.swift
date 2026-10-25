import CleansiaCore
import CleansiaPartnerApi
import Combine
import Foundation

@MainActor
final class PersonalSectionViewModel: ViewModel {
    /// Each error clears when its own field changes, whichever binding wrote it. A write of the value
    /// already there fixes nothing, so it keeps the error.
    struct Loaded: Equatable {
        var employeeId: String
        var firstName: String {
            didSet { if firstName != oldValue { firstNameError = nil } }
        }

        var lastName: String {
            didSet { if lastName != oldValue { lastNameError = nil } }
        }

        var birthDate: Date? {
            didSet { if birthDate != oldValue { birthDateError = nil } }
        }

        var phone: String {
            didSet { if phone != oldValue { phoneError = nil } }
        }

        var email: String
        var firstNameError: String?
        var lastNameError: String?
        var birthDateError: String?
        var phoneError: String?
    }

    @Published private(set) var state: UiState<Void> = .loading
    @Published private(set) var action: ActionState = .idle
    @Published var form = Loaded(
        employeeId: "",
        firstName: "",
        lastName: "",
        birthDate: nil,
        phone: "",
        email: ""
    )

    let saved = PassthroughSubject<Void, Never>()

    private let client: PartnerProfileClient
    private let snackbar: SnackbarController
    private let localizer = ApiErrorLocalizer()

    init(client: PartnerProfileClient, snackbar: SnackbarController) {
        self.client = client
        self.snackbar = snackbar
    }

    func load() async {
        state = .loading
        switch await client.getCurrentEmployee() {
        case let .success(employee):
            form = Loaded(
                employeeId: employee.id ?? "",
                firstName: employee.firstName ?? "",
                lastName: employee.lastName ?? "",
                birthDate: employee.birthDate?.wrappedDate,
                phone: employee.phoneNumber ?? "",
                email: employee.email ?? ""
            )
            state = .loaded(())
        case let .failure(error):
            state = .error(error)
            snackbar.showError(localizer.message(for: error))
        }
    }

    func save() async {
        guard case .loaded = state, !action.isSubmitting else { return }
        form.firstNameError = Self.nameProblem(form.firstName, ifBlank: L10n.Profile.errorFirstNameRequired)
        form.lastNameError = Self.nameProblem(form.lastName, ifBlank: L10n.Profile.errorLastNameRequired)
        form.birthDateError = Self.birthDateProblem(form.birthDate, today: Date())
        form.phoneError = Self.phoneProblem(form.phone)
        let errors = [form.firstNameError, form.lastNameError, form.birthDateError, form.phoneError]
        guard errors.allSatisfy({ $0 == nil }) else { return }
        guard !form.employeeId.isBlank else {
            snackbar.showError(L10n.Profile.errorProfileNotLoaded)
            return
        }

        action = .submitting
        // No email: the command has no field for it. `form.email` is loaded for display only.
        let command = UpdatePersonalInfoCommand(
            employeeId: form.employeeId,
            firstName: form.firstName.trimmed,
            lastName: form.lastName.trimmed,
            birthDate: OpenAPIDateWithoutTime(day: form.birthDate),
            phone: form.phone.trimmed
        )
        switch await client.updatePersonalInfo(command) {
        case .success:
            action = .idle
            saved.send()
        case let .failure(error):
            if let refusal = Self.phoneRefusals.first(where: { error.carries($0) }) {
                form.phoneError = localizer.message(for: ApiError(code: refusal))
                action = .idle
            } else {
                action = .error(localizer.message(for: error))
                snackbar.showError(localizer.message(for: error))
            }
        }
    }

    /// Read by key, since the response's field names are not kept: the names are the only other fields this
    /// command can refuse as required, and the device never sends one blank.
    private static let phoneRefusals = ["validation.invalid_phone_number", "common.required"]

    /// The server's age bounds, in Gregorian years as its `DateOnly.AddYears` counts them, on the day in
    /// Greenwich that the birth date travels as: born at least 18 and at most 120 years before today.
    static func birthDateProblem(_ birthDate: Date?, today now: Date) -> String? {
        guard let birthDate else { return L10n.Profile.errorBirthDateRequired }
        var calendar = Calendar(identifier: .gregorian)
        calendar.timeZone = .gmt
        let today = calendar.startOfDay(for: now)
        guard let youngest = calendar.date(byAdding: .year, value: -18, to: today),
              let oldest = calendar.date(byAdding: .year, value: -120, to: today) else { return nil }
        let day = calendar.startOfDay(for: birthDate)
        if day > youngest { return L10n.Profile.errorMinAge }
        if day < oldest { return ApiErrorLocalizer().message(for: ApiError(code: "validation.invalid_age")) }
        return nil
    }

    /// The server caps a name at 50 UTF-16 code units, trimmed as it is sent.
    private static func nameProblem(_ name: String, ifBlank blankMessage: String) -> String? {
        if name.isBlank { return blankMessage }
        return name.trimmed.utf16.count > 50 ? L10n.Profile.errorNameTooLong : nil
    }

    private static func phoneProblem(_ phone: String) -> String? {
        if phone.isBlank { return L10n.Profile.errorPhoneRequired }
        return PhoneNumberKitDisplayFormatter.isPossibleNumber(phone.trimmed) ? nil : L10n.Profile.errorPhoneInvalid
    }
}
