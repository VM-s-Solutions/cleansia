import CleansiaCore
import Combine
import Foundation

@MainActor
final class OnboardingViewModel: ViewModel {
    let finished = PassthroughSubject<Void, Never>()

    private let settings: AppSettingsStore

    init(settings: AppSettingsStore) {
        self.settings = settings
    }

    func finish() {
        settings.markOnboardingSeen()
        finished.send(())
    }
}
