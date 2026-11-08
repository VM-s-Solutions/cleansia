/// Each supported language in its own name, never translated, so a reader lost in a language they cannot read
/// still finds theirs; in the pickers' order.
public enum LanguageLabels {
    public static let languages: [(tag: String, label: String)] = [
        ("en", "English"),
        ("cs", "Čeština"),
        ("sk", "Slovenčina"),
        ("uk", "Українська"),
        ("ru", "Русский")
    ]

    public static func label(_ tag: String) -> String {
        languages.first { $0.tag == tag }?.label ?? tag
    }
}
