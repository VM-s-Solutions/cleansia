import CleansiaCore
import CleansiaPartnerApi
import Combine
import Foundation

struct OnboardingChainState: Equatable {
    var isLoading: Bool = true
    var completionBySection: [Int: Bool] = Dictionary(
        uniqueKeysWithValues: ProfileSection.allCases.indices.map { ($0, false) }
    )

    var totalSteps: Int {
        ProfileSection.allCases.count
    }

    var completedSteps: Int {
        completionBySection.values.filter { $0 }.count
    }

    func isComplete(_ section: ProfileSection) -> Bool {
        guard let index = ProfileSection.allCases.firstIndex(of: section) else { return false }
        return completionBySection[index] == true
    }
}

enum OnboardingChainStep: Equatable {
    case next(ProfileRoute)
    case finished
}

@MainActor
final class OnboardingChainViewModel: ViewModel {
    @Published private(set) var state = OnboardingChainState()

    let advanced = PassthroughSubject<OnboardingChainStep, Never>()

    private let client: PartnerProfileClient

    init(client: PartnerProfileClient) {
        self.client = client
    }

    func load() async {
        state.isLoading = true
        let status = await (client.checkCurrentEmployee()).valueOrNil
        state = OnboardingChainState(
            isLoading: false,
            completionBySection: Self.perSectionCompletion(status)
        )
    }

    func advanceOrFinish() async {
        let status = await (client.checkCurrentEmployee()).valueOrNil
        state = OnboardingChainState(
            isLoading: false,
            completionBySection: Self.perSectionCompletion(status)
        )
        if let next = Self.nextDestination(status) {
            advanced.send(.next(next))
        } else {
            advanced.send(.finished)
        }
    }

    static func perSectionCompletion(
        _ status: RegistrationCompletionStatus?
    ) -> [Int: Bool] {
        guard let status else {
            return Dictionary(uniqueKeysWithValues: ProfileSection.allCases.indices.map { ($0, false) })
        }
        if status.hasCompletedProfile == true {
            return Dictionary(uniqueKeysWithValues: ProfileSection.allCases.indices.map { ($0, true) })
        }
        let missing = Set(status.missingFields ?? [])
        return Dictionary(uniqueKeysWithValues: ProfileSection.allCases.enumerated().map { index, section in
            (index, section.ownedFields.isDisjoint(with: missing))
        })
    }

    static func nextDestination(_ status: RegistrationCompletionStatus?) -> ProfileRoute? {
        guard let status, status.hasCompletedProfile != true else { return nil }
        let missing = Set(status.missingFields ?? [])
        for section in ProfileSection.allCases where !section.ownedFields.isDisjoint(with: missing) {
            return section.route(onboarding: true)
        }
        return nil
    }
}

private extension ApiResult {
    var valueOrNil: Success? {
        try? get()
    }
}
