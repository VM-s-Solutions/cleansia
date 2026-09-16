import CleansiaCore
import Foundation

final class FakeAppSettingsStore: AppSettingsStore {
    private(set) var answeredPrompts: Set<String> = []
    func hasAnsweredPrompt(_ prompt: String, userId: String) -> Bool {
        answeredPrompts.contains("\(prompt)/\(userId)")
    }

    func markPromptAnswered(_ prompt: String, userId: String) {
        answeredPrompts.insert("\(prompt)/\(userId)")
    }

    var hasSeenOnboarding = false
    var languageTag = "en"
    var persistedLanguageTag: String?
    var theme: Theme = .system

    func markOnboardingSeen() {
        hasSeenOnboarding = true
    }

    func setLanguage(_ tag: String) {
        languageTag = tag
        persistedLanguageTag = tag
    }

    func clearLanguage() {
        persistedLanguageTag = nil
    }

    func setTheme(_ theme: Theme) {
        self.theme = theme
    }
}
